# WindowSlu 開発申し送り

## 2026-11-02 上下・左右の分割配置（実装・ローカル検証完了）

本人がサジェスト「次は上下・左右の分割配置機能を実装・検証してください。Lunaで可能な部分はタスクを作成して任せてください。」を「分割配置の実装まで進める」と明示採用。以下の優先修正記録にある分割配置の「将来案」は、この今回の実装対象へ変更した。

- 成果: 選択グループ、または選択ウィンドウの所属グループの2枚以上を、先頭ウィンドウのモニター作業領域へ左右N列・上下N行で均等配置する。未選択・1枚だけでは動かさない。透明度・TopMost・ClickThroughは保持。
- 既存WindowService/GroupingServiceとWPFを利用。新しい画面や依存パッケージは追加しない。整数境界の計算と実座標の読戻しを追加し、対象アプリのサイズ制約は失敗件数と詳細へ返す。
- 見込み: ラフ5〜10分、操作確認・修正・pushまで30〜50分。アプリの最小サイズ制限と連動ドラッグ干渉が不確実な依存。見込み変更はこの節とチャットへ戻す。
- 提示済みラフ: 元チャットの525px幅ラフ。既存グループ編集欄の下に高さ約30pxの `Split group: [Side by Side] [Stacked]` を追加し、他の配置は維持。実画面をこのラフと照合する。

| 担当・経路 | チャットID / 起動指定 | 所有・完了条件 |
|---|---|---|
| Luna・独立チャット: 分割配置 | 01a19aba-f254-7fe0-96f8-52ba772e1b89 / gpt-6-luna, medium | SplitArrangementService、SplitDirection、WindowServiceのPrepareForArrangementと専用テスト。端数・失敗・実座標を検証 |
| Luna・独立チャット: 連動抑止 | 01a19aba-fd79-7421-bc1a-df9e0bf71922 / gpt-6-luna, medium | LinkedDragServiceと専用テスト。配置中/遅延イベントで追従せず、通常連動へ復帰 |
| 親・このチャット | 起動なし | UI/ViewModel接続・実画面/専用native window検証・レビュー統合・記録・通常commit/push |

作成・実行・終了のツール結果を確認済み。モデル/effortは指定値で、実行内部設定は未取得。分割配置10件、連動抑止4件の担当テストを受領し、差分をレビューしてUIへ統合。

### 到達点・検証

- `dotnet test WindowSlu.Tests/WindowSlu.Tests.csproj -c Release` は全49件成功、失敗0・スキップ0。従来33件も維持した。
- 上段一覧の行が描画されないテンプレートと選択イベント未接続を修正。上段一覧と下段ツリーの選択を排他にし、実コントロール間で切り替えて対象が変わることを確認した。
- UIテストで、0/1枚の無効化、左右/上下のクリック、選択ウィンドウから所属グループを対象にする動作、無選択時に全体へ適用しないこと、途中失敗の表示を確認。WPFの実描画を525×700でラフと照合し、追加部品の重なり・文字切れがないことを確認した。
- 親のnative統合テストは、他アプリに触れない専用HWNDと仮想の作業領域を使用。3枚の左右・上下分割と実WinEvent/タイマーによる遅延追従の抑止、終了後の手動移動への通常追従、最小化からの復元を確認した。
- モニター選択はキャッシュ値を使わず、操作時の所属を取得。primaryだけが不正な場合も有効なモニターへfallbackする。端数・負座標・サイズ制約による不一致・途中失敗は担当テストで確認した。
- ローカル証跡: `artifacts/split-test-results/` のTRX、`artifacts/split-ui/` のWPF描画PNG（いずれもGit管理外）。ラフは元チャットのコードブロック。親の最終レビューとRelease Publishは成功。共有先は従来のorigin/masterへ通常commit/pushし、CI結果は元チャットのrunリンクから確認する。公開用タグは作成しない。
- 見込み更新: 既存サービスとテスト用注入箇所を利用でき、実装・native確認が当初見込みより早く完了。残る確認・記録・共有は5〜10分程度の見込み。CIの実行待ちはGitHub上のrunで確認する。
- 制約: 実モニター着脱、混在DPI、任意の外部アプリ固有のサイズ制限を全て実機検証したものではない。アプリの制限で指定RECTと異なれば失敗として報告する。分割比率やモニター選択UIの追加は今回の対象外。


## 2026-11-02 優先修正（実装・ローカル検証完了）

本人依頼:「推奨した順序で、優先度の高い項目から実装・検証を進めてください。Lunaで可能なことはタスクを作成して投げてください。」

目的は既存の保存・復元と操作対象を信頼して使える状態にすること。手動グループと安定ID → レイアウト1対1復元 → プリセット対象統一 → ホットキー維持を優先し、関連する状態同期・失敗通知・モニター補正、.NET 10/テスト/CIを統合する。プラグインと分割配置は今回の優先修正に含めない。

| 担当 | 独立チャットID / 指定構成 | 所有範囲・終了条件 |
|---|---|---|
| Luna: 手動グループ復元と安定した識別 | 01a19a33-5d0f-73a0-9b50-ee5983970937 / gpt-6-luna, medium | GroupingService、WindowGroup、ManualGroupDataと専用テスト。再起動・refresh・D&Dで所属とIDを維持 |
| Luna: ホットキー再登録と競合通知 | 01a19a33-b89b-74d0-9a8e-db745b2b965f / gpt-6-luna, medium | HotkeyService、ネイティブ呼出し注入部と専用テスト。再読込でキー維持、競合時失敗通知 |
| 親: UI接続・復元・統合検証 | このチャット | 上記以外。各経路の適用条件統一、複数ウィンドウ復元、保存失敗表示、Release build/testと通常push |

独立チャットの作成結果と実行中状態は確認済み。モデル/effortは起動引数で指定した値であり、実行内部設定は未取得。進捗・検証結果はこの節に戻す。既存WPF・JSON保存・Win32操作を修正して使い、新たな実行基盤を導入しない。

### 到達点と確認

- 手動グループの作成・名前変更・削除・D&D保存復元をUIへ接続。自動IDをプロセス名から生成。実行中のタイトル変更は所属を維持し、同名メンバー数を保持する。
- レイアウトは完全一致を先に予約し、部分一致の競合も含め曖昧な対象をスキップ。使用済みウィンドウを再利用しない。消失モニターは現在の作業領域へ補正する。
- 画面・トレイ・ホットキーのプリセット適用を共通化。選択はProcess/Group条件を狭めるために使用し、条件を無視しない。Captureとグループ条件ClearもUIへ接続。
- ホットキー再読込はプリセットも再登録。競合時は旧割当てを維持して画面へ失敗表示。通常キーの表示とリセットも修正。
- 透明度を実状態へ同期し、操作失敗を成功件数へ含めない。最前面表示はAPIが成功を返しても実状態が異なれば失敗を返す。JSONは一時ファイルから置換し、旧版バックアップと破損ファイル退避を行う。
- .NET 10移行、PR/masterの回帰テストCI、タグ配布前テストを追加。Ctrl+ホイールの無効化、Help、READMEと旧ロードマップの参照先を整備。
- `dotnet test WindowSlu.Tests/WindowSlu.Tests.csproj -c Release` は **33件成功・失敗0・スキップ0**。Release buildは警告0・エラー0。`dotnet publish ... -c Release -r win-x64 --self-contained false` も成功。
- 実Windows APIで専用テストウィンドウの透明度・位置・サイズと最前面状態の成否通知、専用ホットキーの登録解除を確認。WPFのグループ操作・競合通知・選択切替を確認し、Windows/Presets/Layouts/Settings/Helpを既定幅525で描画した。
- GitHub Actionsでもcommit `9447d6d` のBuild and Test WindowSluが成功（run `41382971207`）。Node.js 20依存の警告を受け、公式リリースで確認したcheckout v7.0.1 / setup-dotnet v6.0.0 / upload-artifact v7.0.2へ更新。アプリの変更は無し。
- ローカル証跡: `artifacts/test-results/` のTRX、`artifacts/ui/` のPNG。配布確認用実行ファイル: `WindowSlu/bin/Release/net10.0-windows/win-x64/publish/WindowSlu.exe`。いずれもGit管理外。実装の共有先は従来通り `origin/master`、公開リリース用タグは作成しない。

### 既知の制約と後続候補

- 再起動前後でタイトルが変わった手動メンバー、旧版のランダム自動グループIDは推測で再割り当てしない。ウィンドウをD&DするかプリセットのGroupを選び直して保存する。
- 権限差のあるアプリ、実モニターの取り外しやDPI変更を伴う操作は今回の実機確認対象外。モニター補正は座標計算の回帰テストで確認した。
- 優先修正の承認範囲は実装・ローカル検証まで完了。分割配置とプラグイン連携は将来案として残す。公開リリースは未実施。

以下は以前の申し送りであり、現状と異なる箇所がある。

**最終更新**: 2024-12-04

## 現在の状態

### 完了済み機能（v2.3.1）

| 機能 | 状態 | 備考 |
|------|------|------|
| グループ化機能 | ✅ 完了 | プロセス名ベース自動グループ化 |
| プリセット機能 | ✅ 完了 | サイズ・透明度・位置設定の保存・適用 |
| カスケード配置 | ✅ 完了 | グループ内ウィンドウの整列配置 |
| 連動ドラッグ | ✅ 完了 | デフォルトOFF、相対位置維持 |
| グループ一括透明度 | ✅ 完了 | グループヘッダーのスライダーで一括変更 |
| グループ一括サイズ | ✅ 完了 | グループヘッダーで幅×高さ指定して一括適用 |
| ドラッグ&ドロップ | ✅ 完了 | ウィンドウをグループ間でドラッグ移動可能 |
| 条件付きプリセット適用 | ✅ 完了 | TargetProcessName / TargetGroupId フィルタ |
| プリセットホットキー | ✅ 完了 | ショートカットキーでプリセット即時適用 |
| レイアウト保存・復元 | ✅ 完了 | ウィンドウ配置の保存・ワンクリック復元 |

### 最新の変更（v2.2 → v2.3）

#### Phase 6: グループ編集UI強化
- **上下スプリットレイアウト**: Windowsタブを上ペイン（フラットなウィンドウ一覧）と下ペイン（グループTreeView）に分割
- **ウィンドウフィルタ**: タイトル/プロセス名でウィンドウを絞り込み検索
- **ドラッグ&ドロップ**: ウィンドウを別グループにドラッグ移動

#### Phase 7: 高度なプリセット機能
- **条件付き適用**: プリセットに`TargetProcessName`と`TargetGroupId`を設定可能
- **グループ選択UI改善**: TargetGroupIdをComboBoxで選択可能に変更
- **プリセットホットキー**: `HotKey`プロパティを追加、ショートカットキーでプリセット即時適用
  - HotKeyTextBox_PreviewKeyDownでキー入力をキャプチャ
  - HotkeyServiceにプリセット用ホットキー登録・解除機能を追加
  - アプリ起動時に既存プリセットのホットキーを自動登録

#### Phase 8: レイアウト保存・復元機能
- **WindowLayout / WindowLayoutEntry モデル**: レイアウト情報を保存するモデルを新規作成
- **LayoutService**: レイアウトのキャプチャ・復元・更新・削除を管理
  - プロセス名とタイトルパターンでウィンドウを特定し、位置・サイズ・透明度・TopMostを復元
  - JSONファイル（`%APPDATA%/WindowSlu/layouts.json`）に永続化
- **Layoutsタブ**: レイアウト管理UIを新規追加
  - Capture: 現在のウィンドウ配置を保存
  - Restore: 保存したレイアウトを復元
  - Update: 現在の配置でレイアウトを更新
  - Delete: レイアウトを削除

#### v2.3.1: UI/UX改善・バグ修正

- **アイコンボタンのスタイル統一**: すべてのトグルボタンにIsChecked時の背景色変化を統一適用
- **グループ100%リセットボタン追加**: グループヘッダーに透明度を100%に戻すボタンを追加
- **グループヘッダー強調**: Border/背景色で視覚的にグループを区別
- **スライダー禁止マーク修正**: D&D開始条件を改善し、スライダー/ボタン上ではD&Dを開始しないように修正

### 以前の修正（v2.1 → v2.2）

1. **PresetsタブUIの修正**
   - TextBoxにテーマ対応のBackground/Foregroundを追加（白地に白文字問題を修正）
   - CascadeDirection ComboBoxをenum値に正しくバインド

2. **プリセット機能の修正**
   - 新規プリセット作成時にWidth/Height/Opacityのデフォルト値を設定
   - サイズ変更が効かない問題を修正

3. **ドラッグ&ドロップ機能の追加**
   - ウィンドウをグループ間でドラッグ移動可能に
   - グループヘッダーにドロップターゲットを設定

---

## 新規作成ファイル一覧（v2.3）

| ファイル | 説明 |
|----------|------|
| `WindowSlu/Models/WindowLayout.cs` | レイアウトモデル（WindowLayout / WindowLayoutEntry / LayoutData） |
| `WindowSlu/Services/LayoutService.cs` | レイアウト管理サービス（保存・復元・更新・削除） |

## 変更ファイル一覧（v2.3）

| ファイル | 変更内容 |
|----------|----------|
| `MainWindow.xaml` | Windowsタブ上下スプリット、フィルタUI、Layoutsタブ追加 |
| `MainWindow.xaml.cs` | フィルタ/D&D/レイアウト/ホットキーイベントハンドラ追加 |
| `MainViewModel.cs` | FilteredWindows / WindowFilterText / LayoutService追加 |
| `WindowPreset.cs` | HotKey / HotKeyDisplay / TargetProcessName / TargetGroupId追加 |
| `HotkeyAction.cs` | ApplyPresetアクション追加 |
| `HotkeySetting.cs` | PresetIdプロパティ追加 |
| `HotkeyService.cs` | プリセットホットキー登録・解除・パース機能追加 |
| `PresetService.cs` | MatchesTargetFilter / ApplyPresetWithFilter追加 |
| `ROADMAP.md` | v2.3進捗反映 |
| `docs/DESIGN_v2.md` | Phase 6-8完了ステータス更新 |

---

## 配布物

| ファイル | 内容 |
|----------|------|
| `WindowSlu-v2.2-win-x64.zip` | v2.2リリース（UIバグ修正・ドラッグ&ドロップ対応） |
| `WindowSlu-v2.1-GroupBulkControl.zip` | グループ一括制御対応 |
| `WindowSlu-v2.0.1-win-x64.zip` | 連動ドラッグ修正版 |
| `WindowSlu-v2.0-win-x64.zip` | v2.0初期リリース |

> **注意**: v2.3はまだパッケージ化されていません。リリースする場合は以下のコマンドを実行してください。

```powershell
cd WindowSlu
dotnet publish -c Release -r win-x64 --self-contained false
# bin\Release\net9.0-windows\win-x64\publish フォルダをZIP化
```

---

## 今後の課題・検討事項

- [ ] UI/UX改善: ヘルプ/チュートリアル追加、視覚的フィードバック強化
- [ ] タスクトレイ機能の強化
- [ ] プラグインシステムによる拡張性向上
- [ ] GitHub Releasesへの正式リリース公開(v2.3)

---

## テスト手順

### プリセットホットキーのテスト
1. アプリを起動: `dotnet run`
2. Presetsタブでプリセットを選択
3. Hotkeyフィールドにフォーカスし、キーを押す（例: Ctrl+1）
4. ステータスバーに「Hotkey 'Ctrl+1' set for preset...」と表示されることを確認
5. 他のウィンドウで Ctrl+1 を押し、プリセットが適用されることを確認
6. Escキーでホットキーをクリア可能

### レイアウト機能のテスト
1. Layoutsタブを開く
2. Captureボタンで現在のウィンドウ配置を保存
3. ウィンドウを移動・リサイズ
4. 保存したレイアウトを選択してRestoreボタンを押す
5. ウィンドウが元の位置・サイズに復元されることを確認

### 条件付きプリセット適用のテスト
1. Presetsタブでプリセットを選択
2. Process Name または Group をフィルタとして設定
3. Applyボタンを押す
4. フィルタ条件に一致するウィンドウ/グループにのみプリセットが適用されることを確認

---

## 技術情報

- **フレームワーク**: .NET 9 (WPF)
- **アーキテクチャ**: MVVM
- **ビルド**: `dotnet publish -c Release -r win-x64 --self-contained false`
- **設定ファイル保存先**: `%APPDATA%/WindowSlu/`
  - `presets.json` - プリセット設定
  - `layouts.json` - レイアウト設定
  - `settings.json` - アプリ設定

## 関連ドキュメント

- `README.md` - プロジェクト概要・使い方
- `ROADMAP.md` - 開発ロードマップ
- `docs/DESIGN_v2.md` - 設計ドキュメント

---

## Git コミット履歴（v2.3）

```
d7b7625 feat(preset): add hotkey support for preset application [Phase 7]
2ef8977 feat(layout): add window layout save/restore feature [Phase 8]
```

## 作業再開のための情報

### 次回作業候補
1. **v2.3リリースパッケージ作成**: 上記ビルドコマンドでパッケージ化
2. **UI/UX改善**: ヘルプ画面/チュートリアル追加
3. **拡張性向上**: プラグインシステムの設計・実装

### 開発環境セットアップ
```powershell
git clone https://github.com/YuShimoji/WindowSlu.git
cd WindowSlu/WindowSlu
dotnet restore
dotnet run
```
