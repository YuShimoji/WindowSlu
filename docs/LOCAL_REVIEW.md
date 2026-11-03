# WindowSluの起動と画面確認

通常の入口はWindowsのスタートメニュー「WindowSlu」とデスクトップの「WindowSlu」。どちらも、このcheckoutの `WindowSlu/bin/Release/net10.0-windows/win-x64/publish/WindowSlu.exe` を開く。

| 目的 | 入口 | 確認すること |
|---|---|---|
| アプリを開く | スタートメニュー／デスクトップのWindowSlu | 最新の反映先が開き、保存した設定が読み込まれる |
| CLIから同じ入口を開く | [Start-WindowSlu.ps1](<C:/Users/thank/Storage/App Projects/WindowSlu/scripts/Start-WindowSlu.ps1>) | `-ShowPath`は起動先の確認のみ。同じexeが起動中なら追加起動しない |
| 検証済みpublishを通常入口へ反映 | [Update-LocalApp.ps1](<C:/Users/thank/Storage/App Projects/WindowSlu/scripts/Update-LocalApp.ps1>) | `-PublishDirectory <検証済みfolder>`。起動中の通常版は終了せずに停止を通知する。旧exe・設定・リンクはartifacts内へ退避し、設定のbyte非変更と新版hashを確認する |

通常版の起動と画面確認では、保存プリセット・レイアウトや他アプリへの適用を試験しない。変更の動作試験には専用データと専用ウィンドウを使う。

親が視認した追加対応後の画像は以下。これらは専用データのfixtureであり、通常プロファイルの起動確認とは区別する。

| 対象（リンク） | 状態 | 確認方法 |
|---|---|---|
| [525×700・単体操作](<C:/Users/thank/Storage/App Projects/WindowSlu/artifacts/ui-followup-20261104/worker/screens/525x700-Windows-fixture.png>) | 数値・単位・刻み・候補を既存画面内へ追加 | 横はみ出しなし。高さ・下の行は縦スクロール対象 |
| [525×700・グループ操作](<C:/Users/thank/Storage/App Projects/WindowSlu/artifacts/ui-followup-20261104/worker/screens/525x700-Group-mixed-fixture.png>) | 実値が混在するグループを明示 | Actual mixedと変更目標を別々に表示 |
| [Darkの入力状態](<C:/Users/thank/Storage/App Projects/WindowSlu/artifacts/ui-followup-20261104/worker/screens/palette-Dark-fixture.png>)・[Lightの入力状態](<C:/Users/thank/Storage/App Projects/WindowSlu/artifacts/ui-followup-20261104/worker/screens/palette-Light-fixture.png>) | 選択文字・通常・読取専用・無効状態が読める | 更新PNGで親も視認。選択文字の描画画素も検証 |
| [候補の選択状態](<C:/Users/thank/Storage/App Projects/WindowSlu/artifacts/ui-followup-20261104/worker/screens/values-highlight-Dark-fixture.png>) | よく使うサイズの候補を表示 | 選択中の640pxも読める |

操作の照合点は以下。通常プロファイルで試験のための適用を行う必要はない。

1. 数値は直接入力し、EnterまたはSetで確定する。入力途中・不正値では対象を変えず、実際の結果を再表示する。
2. Stepで刻みを選び、±または細かな範囲のスライダーで調整する。刻みの選択だけでは対象を変えない。Valuesから切りの良い候補値を選べる。
3. BulkとPresetはUseで設定値を確定し、Applyで実際の対象へ適用する。ConfiguredとActualを区別して読む。

通常入口への最新版反映は完了した。Startメニューの実ショートカットから起動し、Start-WindowSlu.ps1も同じPIDを返すこと、起動直後の応答低下後に応答が回復し表示・タブ移動できることを担当が確認した。親も以下の実画像を視認した。

| 対象（リンク） | 状態 | 確認方法 |
|---|---|---|
| [通常版のWindows画面](<C:/Users/thank/Storage/App Projects/WindowSlu/artifacts/ui-followup-20261104/worker/screens/normal-Windows-native.png>)・[Presets画面](<C:/Users/thank/Storage/App Projects/WindowSlu/artifacts/ui-followup-20261104/worker/screens/normal-Presets-native.png>) | 通常プロファイルで起動・表示を確認 | Startメニューの実リンクから起動。専用fixtureの画像と区別し、他アプリへの適用・保存操作は行わない |
| [反映とバックアップの記録](<C:/Users/thank/Storage/App Projects/WindowSlu/artifacts/ui-followup-20261104/launch/install-receipt.json>) | デスクトップとStartメニューのリンクは同じ最新版を参照 | 親も配置exeのSHA256と設定・presetの4ファイルの非変更を独立照合 |
| [配布版の同一性](<C:/Users/thank/Storage/App Projects/WindowSlu/artifacts/ui-followup-20261104/worker/publish-verification.json>) | 製品コミット02172db、自己完結single-file | ProductVersionに同コミットを含む。SHA256は0958A1E7DDC67278757F7AE51795F39BC59220FCE05F9EFAC1B6486F84F6100A |

実行ファイルは[通常のWindowSlu.exe](<C:/Users/thank/Storage/App Projects/WindowSlu/WindowSlu/bin/Release/net10.0-windows/win-x64/publish/WindowSlu.exe>)。通常のデスクトップ入口は[WindowSluショートカット](<C:/Users/thank/OneDrive/デスクトップ/WindowSlu.lnk>)。旧版と設定は反映記録に記載したバックアップ先へ保存した。
