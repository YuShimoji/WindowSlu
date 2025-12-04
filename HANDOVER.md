# WindowSlu 開発申し送り

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
