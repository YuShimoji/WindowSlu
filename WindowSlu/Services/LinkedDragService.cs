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
        private readonly object _lock = new object();
        private readonly Dictionary<IntPtr, Point> _lastPositions = new Dictionary<IntPtr, Point>();
        private readonly Timer _debounceTimer;
        private readonly Dictionary<IntPtr, (Point Delta, IntPtr LeaderHandle)> _pendingMoves = new Dictionary<IntPtr, (Point, IntPtr)>();
        private const int DEBOUNCE_DELAY_MS = 100; // デバウンス遅延（ミリ秒）

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

            // WinEventHook のセットアップ
            _eventHook = SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, WinEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);
            if (_eventHook == IntPtr.Zero)
            {
                LoggingService.LogError("Failed to set WinEventHook for location changes.");
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
            // ウィンドウの現在の位置を取得
            var rect = _windowService.GetWindowPosition(hwnd);
            if (rect.Width == 0 && rect.Height == 0) return; // 無効な位置

            var currentPos = new Point(rect.Left, rect.Top);

            lock (_lock)
            {
                // 前回の位置を取得
                if (_lastPositions.TryGetValue(hwnd, out var lastPos))
                {
                    var delta = new Point(currentPos.X - lastPos.X, currentPos.Y - lastPos.Y);

                    // 移動量が十分大きい場合のみ処理
                    if (Math.Abs(delta.X) > 1 || Math.Abs(delta.Y) > 1)
                    {
                        // グループを取得
                        var group = _groupingService.Groups.FirstOrDefault(g => g.Windows.Any(w => w.Handle == hwnd));
                        if (group != null && group.Windows.Count > 1)
                        {
                            // グループ内他のウィンドウを移動
                            foreach (var window in group.Windows.Where(w => w.Handle != hwnd))
                            {
                                var windowRect = _windowService.GetWindowPosition(window.Handle);
                                if (windowRect.Width > 0 && windowRect.Height > 0)
                                {
                                    // 保留中の移動を更新
                                    _pendingMoves[window.Handle] = (delta, hwnd);
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
            lock (_lock)
            {
                foreach (var kvp in _pendingMoves)
                {
                    var windowHandle = kvp.Key;
                    var (delta, leaderHandle) = kvp.Value;

                    var rect = _windowService.GetWindowPosition(windowHandle);
                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        var newX = rect.Left + (int)delta.X;
                        var newY = rect.Top + (int)delta.Y;

                        // ウィンドウを移動（サイズは維持）
                        _windowService.SetWindowPosition(windowHandle, newX, newY, rect.Width, rect.Height);
                    }
                }

                _pendingMoves.Clear();
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
