using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowSlu.Services;

namespace WindowSlu
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // サービス
        private WindowService? windowService;
        private NotifyIconService? notifyIconService;
        private ThemeService? themeService;

        // 選択されたウィンドウのハンドル
        private IntPtr selectedWindowHandle = IntPtr.Zero;

        public MainWindow()
        {
            try
            {
                // コンポーネントの初期化
                InitializeComponent();

                // サービスの初期化
                InitializeServices();

                // ウィンドウ一覧の初期化はロード後に行う
                this.Loaded += MainWindow_Loaded;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"初期化エラー: {ex.Message}");
            }
        }

        // ウィンドウ全体のマウスダウンイベント（ドラッグ用）
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // タイトルバーでのドラッグは TitleBar_MouseDown で処理されるため、ここでは重複しないようにする
            // もし e.Source が TitleBar またはその子要素であれば、何もしないという判定も可能だが、
            // DragMove() はウィンドウ全体に対する操作なので、タイトルバーでもここが呼ばれて問題ないことが多い。
            // ただし、特定のUI要素上でのドラッグを防ぎたい場合は、e.OriginalSource をチェックする必要がある。
            if (e.ButtonState == MouseButtonState.Pressed && e.ChangedButton == MouseButton.Left)
            {
                try
                {
                    this.DragMove();
                }
                catch (InvalidOperationException ex)
                {
                    // DragMoveはマウスがキャプチャされている場合などに InvalidOperationException をスローすることがある
                    Console.WriteLine($"ウィンドウドラッグエラー（全体）: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"予期せぬドラッグエラー: {ex.Message}");
                }
            }
        }

        // タイトルバーのマウスダウンイベント（ドラッグ用）
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    this.DragMove();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ドラッグエラー: {ex.Message}");
            }
        }

        // テーマ切り替えボタンのクリックイベント
        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // テーマを切り替え
                if (themeService != null)
                {
                    themeService.ToggleTheme();

                    // アイコンを変更
                    UpdateThemeIcon(themeService.GetCurrentTheme() == ThemeService.ThemeType.Dark);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"テーマ切り替えエラー: {ex.Message}");
            }
        }

        // テーマアイコンの更新
        private void UpdateThemeIcon(bool isDarkTheme)
        {
            try
            {
                if (FindName("ThemeToggleText") is TextBlock themeToggleText)
                {
                    themeToggleText.Text = isDarkTheme ? "🌓" : "🌞"; // 月または太陽のアイコン
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"テーマアイコン更新エラー: {ex.Message}");
            }
        }

        // タスクトレイ格納ボタンのクリックイベント
        private void MinimizeToTrayButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Hide();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"タスクトレイ格納エラー: {ex.Message}");
            }
        }

        // 最小化ボタンのクリックイベント
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"最小化エラー: {ex.Message}");
            }
        }

        // 最大化ボタンのクリックイベント
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (this.WindowState == WindowState.Maximized)
                {
                    this.WindowState = WindowState.Normal;
                    if (FindName("MaximizeButtonText") is TextBlock maximizeButtonText)
                    {
                        maximizeButtonText.Text = "□";
                    }
                }
                else
                {
                    this.WindowState = WindowState.Maximized;
                    if (FindName("MaximizeButtonText") is TextBlock maximizeButtonText)
                    {
                        maximizeButtonText.Text = "❐";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"最大化エラー: {ex.Message}");
            }
        }

        // 閉じるボタンのクリックイベント
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"閉じるエラー: {ex.Message}");
            }
        }

        // 透明度スライダーの値変更イベント
        private void TransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (windowService == null)
                {
                    Console.WriteLine("windowService is null in TransparencySlider_ValueChanged");
                    return;
                }

                if (sender is Slider slider)
                {
                    IntPtr targetWindow = IntPtr.Zero;

                    // 選択されているウィンドウがある場合はそれを対象に
                    if (selectedWindowHandle != IntPtr.Zero)
                    {
                        targetWindow = selectedWindowHandle;
                    }
                    else
                    {
                        // アクティブウィンドウを対象に
                        IntPtr activeWindow = windowService.GetActiveWindow();

                        // 自分自身のウィンドウは対象外
                        if (activeWindow == IntPtr.Zero || activeWindow == new System.Windows.Interop.WindowInteropHelper(this).Handle)
                            return;

                        targetWindow = activeWindow;
                        selectedWindowHandle = activeWindow;
                    }

                    // ウィンドウの透明度を設定
                    windowService.SetWindowTransparency(targetWindow, (byte)(e.NewValue * 2.55)); // 0-100 を 0-255 に変換
                }
            }
            catch (Exception ex)
            {
                // エラーログ
                Console.WriteLine($"透明度変更エラー: {ex.Message}");
            }
        }

        // ウィンドウ一覧の更新ボタンクリック
        private void RefreshWindowList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (windowService == null)
                {
                    Console.WriteLine("windowService is null in RefreshWindowList_Click");
                    return;
                }
                windowService.RefreshWindowList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ウィンドウ一覧更新エラー: {ex.Message}");
            }
        }

        // ウィンドウロード後の処理
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (windowService == null)
                {
                    Console.WriteLine("windowService is null in MainWindow_Loaded");
                    InitializeServices();
                    if (windowService == null) return;
                }

                // ウィンドウ一覧の初期化
                System.Windows.Controls.ListView? windowListView = FindName("WindowListView") as System.Windows.Controls.ListView;
                if (windowListView != null)
                {
                    windowListView.ItemsSource = windowService.GetWindowList();
                    windowService.RefreshWindowList();
                }

                // テーマアイコンの初期化
                if (themeService != null)
                {
                    UpdateThemeIcon(themeService.GetCurrentTheme() == ThemeService.ThemeType.Dark);
                }

                // イベントハンドラの登録
                if (FindName("TransparencySlider") is Slider transparencySlider)
                {
                    transparencySlider.ValueChanged += TransparencySlider_ValueChanged;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ウィンドウロードエラー: {ex.Message}");
            }
        }

        // ウィンドウ一覧の選択変更イベント
        private void WindowListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                System.Windows.Controls.ListView? listView = sender as System.Windows.Controls.ListView;
                if (listView != null && listView.SelectedItem is WindowInfo selectedWindow && selectedWindow != null)
                {
                    selectedWindowHandle = selectedWindow.Handle;

                    // 透明度スライダーの値を更新
                    TransparencySlider.ValueChanged -= TransparencySlider_ValueChanged;
                    TransparencySlider.Value = selectedWindow.Transparency;
                    TransparencySlider.ValueChanged += TransparencySlider_ValueChanged;

                    // 最前面化チェックボックスの状態を更新
                    if (TopMostCheckBox != null)
                    {
                        TopMostCheckBox.IsChecked = selectedWindow.IsTopMost;
                    }
                }
                else
                {
                    selectedWindowHandle = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ウィンドウ選択エラー: {ex.Message}");
            }
        }

        // ホイール感度スライダーの値変更イベント
        private void TransparencyStepSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                // ホイール感度の値を保存
                // ここでは単純に表示を更新するだけ
                // 実際のホイール処理は別のイベントで実装予定
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ホイール感度変更エラー: {ex.Message}");
            }
        }

        // 最前面化チェックボックスのクリックイベント
        private void TopMostCheckBox_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is CheckBox checkBox && WindowListView.SelectedItem is WindowInfo selectedWindow)
                {
                    bool isTopMost = checkBox.IsChecked ?? false;
                    windowService?.SetWindowTopMost(selectedWindow.Handle, isTopMost);

                    // ウィンドウリストを更新
                    windowService?.RefreshWindowList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"最前面化設定エラー: {ex.Message}");
            }
        }

        // 最前面化適用ボタンのクリックイベント
        private void ApplyTopMost_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WindowListView.SelectedItem is WindowInfo selectedWindow)
                {
                    bool isTopMost = TopMostCheckBox.IsChecked ?? false;
                    windowService?.SetWindowTopMost(selectedWindow.Handle, isTopMost);

                    // ウィンドウリストを更新
                    windowService?.RefreshWindowList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"最前面化適用エラー: {ex.Message}");
            }
        }

        // ショートカットキー適用ボタンクリック
        private void ApplyShortcuts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // ショートカットキーの適用処理はこれから実装
                System.Windows.MessageBox.Show("ショートカットキー設定はこれから実装します。", "WindowSlu", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ショートカットキー適用エラー: {ex.Message}");
            }
        }

        // 透明度変化量スライダーの値変更イベント
        // 注意: このメソッドは上記に移動しました
        private void TransparencyStepSlider_ValueChanged_Old(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                // 将来的にはここで設定を保存する処理を実装
                // 現時点では値の表示はバインディングで自動更新される
            }
            catch (Exception ex)
            {
                Console.WriteLine($"透明度変化量設定エラー: {ex.Message}");
            }
        }

        // ウィンドウが閉じられる際の処理 (オーバーライド)
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // イベントハンドラの解除
            if (FindName("TransparencySlider") is Slider transparencySlider)
            {
                transparencySlider.ValueChanged -= TransparencySlider_ValueChanged;
            }

            // NotifyIconの後片付け
            notifyIconService?.Dispose(); // CS8602 警告箇所かもしれないが、null条件演算子で対処
        }

        // サービス初期化メソッドを追加
        private void InitializeServices()
        {
            if (windowService == null)
            {
                windowService = new WindowService();
            }
            if (notifyIconService == null)
            {
                notifyIconService = new NotifyIconService(this);
            }
            if (themeService == null)
            {
                themeService = new ThemeService();
            }
        }
    }
}
