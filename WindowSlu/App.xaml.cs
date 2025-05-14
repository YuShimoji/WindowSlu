using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace WindowSlu
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // アプリケーションの例外ハンドラを追加
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // 例外情報をコンソールに出力
            Console.WriteLine($"DispatcherUnhandledException: {e.Exception.Message}");
            Console.WriteLine($"StackTrace: {e.Exception.StackTrace}");

            // 例外を処理済みとしてマーク
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // 例外情報をコンソールに出力
            if (e.ExceptionObject is Exception ex)
            {
                Console.WriteLine($"UnhandledException: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }
            else
            {
                Console.WriteLine($"UnhandledException: {e.ExceptionObject}");
            }
        }
    }
}
