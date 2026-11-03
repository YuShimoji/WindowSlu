# WindowSluの起動と画面確認

通常の入口はWindowsのスタートメニュー「WindowSlu」とデスクトップの「WindowSlu」。どちらも、このcheckoutの `WindowSlu/bin/Release/net10.0-windows/win-x64/publish/WindowSlu.exe` を開く。

| 目的 | 入口 | 確認すること |
|---|---|---|
| アプリを開く | スタートメニュー／デスクトップのWindowSlu | 最新の反映先が開き、保存した設定が読み込まれる |
| CLIから同じ入口を開く | [Start-WindowSlu.ps1](<C:/Users/thank/Storage/App Projects/WindowSlu/scripts/Start-WindowSlu.ps1>) | `-ShowPath`は起動先の確認のみ。同じexeが起動中なら追加起動しない |
| 検証済みpublishを通常入口へ反映 | [Update-LocalApp.ps1](<C:/Users/thank/Storage/App Projects/WindowSlu/scripts/Update-LocalApp.ps1>) | `-PublishDirectory <検証済みfolder>`。起動中の通常版は終了せずに停止を通知する。旧exe・設定・リンクはartifacts内へ退避し、設定のbyte非変更と新版hashを確認する |

通常版の起動と画面確認では、保存プリセット・レイアウトや他アプリへの適用を試験しない。変更の動作試験には専用データと専用ウィンドウを使う。

提出用スクリーンショットと通常起動の確認結果は、今回の追加対応が完了した時点でこの案内へ追記する。旧版の画面を追加対応後の画面として扱わない。
