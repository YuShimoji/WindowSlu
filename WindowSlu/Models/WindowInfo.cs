using System;
using System.Diagnostics;

namespace WindowSlu
{
    /// <summary>
    /// ウィンドウ情報を格納するクラス
    /// </summary>
    public class WindowInfo
    {
        /// <summary>
        /// ウィンドウハンドル
        /// </summary>
        public IntPtr Handle { get; set; }

        /// <summary>
        /// ウィンドウタイトル
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// プロセス名
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// プロセスID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// 透明度（0-100）
        /// </summary>
        public int Transparency { get; set; } = 100;

        /// <summary>
        /// 最前面化状態
        /// </summary>
        public bool IsTopMost { get; set; } = false;

        /// <summary>
        /// 文字列表現
        /// </summary>
        public override string ToString()
        {
            return $"{Title} ({ProcessName})";
        }
    }
}
