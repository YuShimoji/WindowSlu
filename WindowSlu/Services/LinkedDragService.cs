using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using WindowSlu.Models;
using WindowSlu.Services;

namespace WindowSlu.Services
{
    /// <summary>
    /// グループ内ウィンドウの連動ドラッグを管理するサービス
    /// </summary>
    public class LinkedDragService : IDisposable
    {
        private readonly GroupingService _groupingService;
        private readonly WindowService _windowService;
        private IntPtr _eventHook;
        private readonly WinEventDelegate _winEventDelegate;
        private readonly object _lock = new object();
        private readonly Dictionary<IntPtr, Point> _lastPositions = new Dictionary<IntPtr, Point>();
        private readonly Timer _debounceTimer;
        private readonly Dictionary<IntPtr, (Point Delta, IntPtr LeaderHandle)> _pendingMoves = new Dictionary<IntPtr, (Point, IntPtr)>();
        private const int DEBOUNCE_DELAY_MS = 100; // デバウンス遅延（ミリ秒）
        private volatile bool _isProcessingMoves;
        private readonly Dictionary<IntPtr, DateTime> _suppressedUntil = new Dictionary<IntPtr, DateTime>();
        private const int SUPPRESS_AFTER_MOVE_MS = 200;

        // WinEventHook P/Invoke
        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint EVENT_OBJECT_DESTROY = 0x8001;

        public LinkedDragService(GroupingService groupingService, WindowService windowService)
        {
            _groupingService = groupingService ?? throw new ArgumentNullException(nameof(groupingService));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _debounceTimer = new Timer(ProcessPendingMoves, null, Timeout.Infinite, Timeout.Infinite);

            // WinEvent のコールバックデリゲートをフィールドに保持して GC されないようにする
            _winEventDelegate = WinEventProc;

            // WinEventHook を有効化（全プロセス対象、位置変更イベントのみ）
            _eventHook = SetWinEventHook(
                EVENT_OBJECT_LOCATIONCHANGE,
                EVENT_OBJECT_LOCATIONCHANGE,
                IntPtr.Zero,
                _winEventDelegate,
                0,
                0,
                WINEVENT_OUTOFCONTEXT);

            if (_eventHook == IntPtr.Zero)
            {
                LoggingService.LogError("LinkedDragService: Failed to set WinEventHook for linked drag.");
            }
            else
            {
                LoggingService.LogInfo("LinkedDragService: WinEventHook registered successfully for linked drag.");
            }
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType == EVENT_OBJECT_LOCATIONCHANGE && idObject == 0) // ウィンドウオブジェクト
            {
                try
                {
                    HandleWindowMove(hwnd);
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"Error handling window move: {ex.Message}");
                }
            }
        }

        private void HandleWindowMove(IntPtr hwnd)
        {
            // 自前の MoveWindow/SetWindowPosition 呼び出し中に発生したイベントは無視する
            if (_isProcessingMoves)
            {
                return;
            }

            // ウィンドウの現在の位置を取得
            var rect = _windowService.GetWindowPosition(hwnd);
            if (rect.Width == 0 && rect.Height == 0) return; // 無効な位置

            var currentPos = new Point(rect.Left, rect.Top);

            lock (_lock)
            {
                if (_suppressedUntil.TryGetValue(hwnd, out var until))
                {
                    if (DateTime.UtcNow <= until)
                    {
                        return;
                    }

                    _suppressedUntil.Remove(hwnd);
                }

                // 前回の位置を取得
                if (_lastPositions.TryGetValue(hwnd, out var lastPos))
                {
                    var delta = new Point(currentPos.X - lastPos.X, currentPos.Y - lastPos.Y);

                    // 移動量が十分大きい場合のみ処理
                    if (Math.Abs(delta.X) > 1 || Math.Abs(delta.Y) > 1)
                    {
                        // グループを取得
                        var group = _groupingService.Groups.FirstOrDefault(g => g.Windows.Any(w => w.Handle == hwnd));
                        // グループの連動ドラッグが無効な場合はスキップ
                        if (group != null && group.Windows.Count > 1 && group.LinkedDragEnabled)
                        {
                            // ドラッグ元ウィンドウの IncludeInLinkedDrag を確認
                            var leaderWindow = group.Windows.FirstOrDefault(w => w.Handle == hwnd);
                            if (leaderWindow == null || !leaderWindow.IncludeInLinkedDrag)
                            {
                                // ドラッグ元が連動対象外なら他のウィンドウは追従しない
                                _lastPositions[hwnd] = currentPos;
                                return;
                            }

                            // グループ内他のウィンドウを移動（連動対象のみ）
                            foreach (var window in group.Windows.Where(w => w.Handle != hwnd && w.IncludeInLinkedDrag))
                            {
                                var windowRect = _windowService.GetWindowPosition(window.Handle);
                                if (windowRect.Width > 0 && windowRect.Height > 0)
                                {
                                    // 保留中の移動を蓄積（上書きではなく加算）
                                    if (_pendingMoves.TryGetValue(window.Handle, out var existing))
                                    {
                                        var accumulatedDelta = new Point(existing.Delta.X + delta.X, existing.Delta.Y + delta.Y);
                                        _pendingMoves[window.Handle] = (accumulatedDelta, hwnd);
                                    }
                                    else
                                    {
                                        _pendingMoves[window.Handle] = (delta, hwnd);
                                    }
                                }
                            }

                            // デバウンスタイマーをリセット
                            _debounceTimer.Change(DEBOUNCE_DELAY_MS, Timeout.Infinite);
                        }
                    }
                }

                // 位置を更新
                _lastPositions[hwnd] = currentPos;
            }
        }

        private void ProcessPendingMoves(object? state)
        {
            _isProcessingMoves = true;
            try
            {
                lock (_lock)
                {
                    foreach (var kvp in _pendingMoves)
                    {
                        var windowHandle = kvp.Key;
                        var (delta, leaderHandle) = kvp.Value;

                        // 移動量が極端に大きい場合はスキップ（異常値の防止）
                        if (Math.Abs(delta.X) > 500 || Math.Abs(delta.Y) > 500)
                        {
                            LoggingService.LogInfo($"LinkedDragService: Skipping abnormally large delta ({delta.X}, {delta.Y}) for window {windowHandle}");
                            continue;
                        }

                        var rect = _windowService.GetWindowPosition(windowHandle);
                        if (rect.Width > 0 && rect.Height > 0)
                        {
                            var newX = rect.Left + (int)delta.X;
                            var newY = rect.Top + (int)delta.Y;

                            // ウィンドウを移動（サイズは維持）
                            _windowService.SetWindowPosition(windowHandle, newX, newY, rect.Width, rect.Height);

                            _lastPositions[windowHandle] = new Point(newX, newY);
                            _suppressedUntil[windowHandle] = DateTime.UtcNow.AddMilliseconds(SUPPRESS_AFTER_MOVE_MS);

                            // 対応する WindowInfo も更新（UIに反映するため）
                            var windowInfo = _groupingService.Groups
                                .SelectMany(g => g.Windows)
                                .FirstOrDefault(w => w.Handle == windowHandle);
                            if (windowInfo != null)
                            {
                                windowInfo.Left = newX;
                                windowInfo.Top = newY;
                                // Width/Height は rect から取得した値を使用
                                windowInfo.Width = rect.Width;
                                windowInfo.Height = rect.Height;
                            }
                        }
                        else
                        {
                            LoggingService.LogInfo($"LinkedDragService: Skipping window {windowHandle} with invalid size ({rect.Width}x{rect.Height})");
                        }
                    }

                    _pendingMoves.Clear();
                }
            }
            finally
            {
                _isProcessingMoves = false;
            }
        }

        public void Dispose()
        {
            if (_eventHook != IntPtr.Zero)
            {
                UnhookWinEvent(_eventHook);
                _eventHook = IntPtr.Zero;
            }

            _debounceTimer.Dispose();
        }
    }
}
