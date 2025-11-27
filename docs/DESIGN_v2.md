# WindowSlu v2.0 設計ドキュメント

## 1. 概要

### 1.1 目的
WindowSluに以下の機能を追加し、複数ウィンドウの効率的な管理を実現する：
- ウィンドウのグループ化（プロセス名ベース＋カスタム）
- プリセット機能（サイズ・透明度・位置の一括適用）
- カスケード配置（重なりつつ整列）
- 連動ドラッグ（グループ内ウィンドウの同時移動）

### 1.2 主要ユースケース
```
UC-1: IDE複数ウィンドウの一括整形
1. 複数のVSCodeウィンドウを開いている
2. WindowSluでVSCodeグループを選択
3. プリセット「IDE作業用」を適用
   → 全ウィンドウが 1200x800, 透明度80%, カスケード配置になる
4. グループの先頭ウィンドウをドラッグ
   → 全ウィンドウが連動して移動
```

---

## 2. アーキテクチャ

### 2.1 レイヤー構成（MVVM）
```
┌─────────────────────────────────────────────────────────┐
│  View Layer (XAML)                                       │
│  - MainWindow.xaml (TreeView for grouped windows)        │
│  - PresetDialog.xaml (プリセット編集)                     │
├─────────────────────────────────────────────────────────┤
│  ViewModel Layer                                         │
│  - MainViewModel.cs (グループ化されたウィンドウリスト)      │
│  - PresetViewModel.cs (プリセット管理)                    │
├─────────────────────────────────────────────────────────┤
│  Service Layer                                           │
│  - WindowService.cs (Win32 API操作 - 既存拡張)           │
│  - GroupingService.cs (グループ化ロジック) [NEW]          │
│  - PresetService.cs (プリセット保存・読込) [NEW]          │
│  - ArrangementService.cs (カスケード配置) [NEW]          │
│  - LinkedDragService.cs (連動ドラッグ) [NEW]             │
├─────────────────────────────────────────────────────────┤
│  Model Layer                                             │
│  - WindowInfo.cs (既存拡張)                              │
│  - WindowGroup.cs [NEW]                                  │
│  - WindowPreset.cs [NEW]                                 │
│  - CascadeSettings.cs [NEW]                              │
└─────────────────────────────────────────────────────────┘
```

### 2.2 ファイル構成
```
WindowSlu/
├── Models/
│   ├── WindowInfo.cs          (既存・拡張)
│   ├── WindowGroup.cs         [NEW]
│   ├── WindowPreset.cs        [NEW]
│   └── CascadeSettings.cs     [NEW]
├── Services/
│   ├── WindowService.cs       (既存・拡張)
│   ├── GroupingService.cs     [NEW]
│   ├── PresetService.cs       [NEW]
│   ├── ArrangementService.cs  [NEW]
│   └── LinkedDragService.cs   [NEW]
├── ViewModels/
│   ├── MainViewModel.cs       (既存・拡張)
│   └── PresetViewModel.cs     [NEW]
└── Views/
    └── PresetDialog.xaml      [NEW]
```

---

## 3. データモデル

### 3.1 WindowInfo.cs（拡張）
```csharp
public class WindowInfo : INotifyPropertyChanged
{
    // 既存フィールド
    public IntPtr Handle { get; set; }
    public string Title { get; set; }
    public string ProcessName { get; set; }
    public int ProcessId { get; set; }
    public bool IsTopMost { get; set; }
    public int Opacity { get; set; }
    public bool IsClickThrough { get; set; }
    public ImageSource? Icon { get; set; }
    
    // 新規フィールド
    public string? GroupId { get; set; }           // 所属グループID
    public int Left { get; set; }                  // ウィンドウX座標
    public int Top { get; set; }                   // ウィンドウY座標
    public int Width { get; set; }                 // ウィンドウ幅
    public int Height { get; set; }                // ウィンドウ高さ
    public string MonitorId { get; set; }          // 所属モニター識別子
}
```

### 3.2 WindowGroup.cs（新規）
```csharp
public class WindowGroup : INotifyPropertyChanged
{
    public string Id { get; set; }                          // 一意識別子 (GUID)
    public string Name { get; set; }                        // 表示名
    public GroupType Type { get; set; }                     // Auto/Manual
    public string? ProcessNameFilter { get; set; }          // 自動グループの場合のフィルタ
    public ObservableCollection<WindowInfo> Windows { get; set; }
    public bool IsExpanded { get; set; } = true;            // TreeView展開状態
    public ImageSource? GroupIcon { get; set; }             // グループアイコン（先頭ウィンドウから取得）
}

public enum GroupType
{
    AutoByProcess,    // プロセス名による自動グループ
    Manual            // ユーザー定義グループ
}
```

### 3.3 WindowPreset.cs（新規）
```csharp
public class WindowPreset
{
    public string Id { get; set; }                          // 一意識別子 (GUID)
    public string Name { get; set; }                        // プリセット名
    public string? TargetProcessName { get; set; }          // 適用対象プロセス（null=任意）
    public string? TargetGroupId { get; set; }              // 適用対象グループ（null=任意）
    
    // ウィンドウ設定
    public int? Width { get; set; }                         // null=変更しない
    public int? Height { get; set; }
    public int? Opacity { get; set; }
    public bool? IsTopMost { get; set; }
    
    // カスケード配置設定
    public bool ApplyCascade { get; set; } = false;
    public CascadeSettings? CascadeSettings { get; set; }
}
```

### 3.4 CascadeSettings.cs（新規）
```csharp
public class CascadeSettings
{
    public int OffsetX { get; set; } = 30;                  // 水平オフセット（px）
    public int OffsetY { get; set; } = 30;                  // 垂直オフセット（px）
    public CascadeDirection Direction { get; set; } = CascadeDirection.TopLeftToBottomRight;
}

public enum CascadeDirection
{
    TopLeftToBottomRight,   // 左上→右下
    TopRightToBottomLeft,   // 右上→左下
    BottomLeftToTopRight,   // 左下→右上
    BottomRightToTopLeft    // 右下→左上
}
```

### 3.5 presets.json（保存形式）
```json
{
  "version": "1.0",
  "groups": [
    {
      "id": "guid-xxx",
      "name": "My IDE Group",
      "type": "Manual",
      "windowHandles": []
    }
  ],
  "presets": [
    {
      "id": "guid-yyy",
      "name": "IDE作業用",
      "targetProcessName": "Code",
      "width": 1200,
      "height": 800,
      "opacity": 80,
      "isTopMost": false,
      "applyCascade": true,
      "cascadeSettings": {
        "offsetX": 30,
        "offsetY": 30,
        "direction": "TopLeftToBottomRight"
      }
    }
  ],
  "cascadeDefaults": {
    "offsetX": 30,
    "offsetY": 30,
    "direction": "TopLeftToBottomRight"
  }
}
```

---

## 4. サービス設計

### 4.1 WindowService.cs（拡張）
```csharp
// 追加するメソッド
public RECT GetWindowRect(IntPtr hWnd);                     // ウィンドウ位置・サイズ取得
public void SetWindowRect(IntPtr hWnd, int x, int y, int w, int h);  // 位置・サイズ設定
public string GetMonitorId(IntPtr hWnd);                    // 所属モニター取得
public List<MonitorInfo> GetAllMonitors();                  // 全モニター情報取得
```

### 4.2 GroupingService.cs（新規）
```csharp
public class GroupingService
{
    // 自動グループ化（プロセス名ベース）
    public ObservableCollection<WindowGroup> GroupByProcess(
        ObservableCollection<WindowInfo> windows);
    
    // カスタムグループ作成
    public WindowGroup CreateManualGroup(string name, IEnumerable<WindowInfo> windows);
    
    // グループへウィンドウ追加/削除
    public void AddToGroup(WindowGroup group, WindowInfo window);
    public void RemoveFromGroup(WindowGroup group, WindowInfo window);
}
```

### 4.3 PresetService.cs（新規）
```csharp
public class PresetService
{
    private const string PresetsFileName = "presets.json";
    
    public List<WindowPreset> LoadPresets();
    public void SavePresets(List<WindowPreset> presets);
    
    // プリセット適用
    public void ApplyPreset(WindowPreset preset, IEnumerable<WindowInfo> windows);
    
    // 現在の状態からプリセット作成
    public WindowPreset CreatePresetFromWindow(WindowInfo window, string name);
    public WindowPreset CreatePresetFromGroup(WindowGroup group, string name);
}
```

### 4.4 ArrangementService.cs（新規）
```csharp
public class ArrangementService
{
    // カスケード配置
    public void ArrangeCascade(
        IEnumerable<WindowInfo> windows, 
        CascadeSettings settings,
        int startX, int startY);
    
    // 基準位置の計算（先頭ウィンドウから）
    public (int x, int y) GetCascadeStartPosition(
        WindowInfo leadWindow, 
        CascadeDirection direction);
}
```

### 4.5 LinkedDragService.cs（新規）
```csharp
public class LinkedDragService
{
    // 連動ドラッグの開始
    public void StartLinkedDrag(WindowGroup group, WindowInfo leadWindow);
    
    // ドラッグ中の位置更新（WinEventHook使用）
    public void OnLeadWindowMoved(int newX, int newY);
    
    // 連動ドラッグの終了
    public void EndLinkedDrag();
}
```

---

## 5. UI設計

### 5.1 メインウィンドウ変更点

**Before (ListView):**
```
[ ] Window 1 - App A     [====] 📌 👻
[ ] Window 2 - App A     [====] 📌 👻
[ ] Window 3 - App B     [====] 📌 👻
```

**After (TreeView):**
```
▼ App A (2 windows)              [Apply Preset ▼] [Cascade] [Link Drag]
    [ ] Window 1         [====] 📌 👻
    [ ] Window 2         [====] 📌 👻
▼ App B (1 window)
    [ ] Window 3         [====] 📌 👻
▶ Ungrouped (0 windows)
```

### 5.2 プリセットパネル/ダイアログ
```
┌─────────────────────────────────────────┐
│ Preset: [IDE作業用        ▼] [New] [Del]│
├─────────────────────────────────────────┤
│ Size:      [1200] x [800]  ☑ Apply      │
│ Opacity:   [====80%====]   ☑ Apply      │
│ TopMost:   [ ] Always on top            │
│ Cascade:   ☑ Enable                     │
│   Offset:  X [30] Y [30] px             │
│   Direction: [↘ Top-Left to Bottom-Right ▼]│
├─────────────────────────────────────────┤
│         [Apply to Selection] [Save]     │
└─────────────────────────────────────────┘
```

---

## 6. 技術的考慮事項

### 6.1 連動ドラッグの実装方式

**方式A: WinEventHook（推奨）**
- `SetWinEventHook`で`EVENT_OBJECT_LOCATIONCHANGE`を監視
- 先頭ウィンドウの移動を検知し、他ウィンドウを追従させる
- メリット: システムレベルで確実に検知可能
- デメリット: 若干の遅延が発生する可能性

**方式B: グローバルマウスフック**
- 既存の`GlobalMouseHookService`を拡張
- マウス移動中にウィンドウ位置を計算・更新
- メリット: リアルタイム性が高い
- デメリット: ウィンドウのドラッグ開始/終了の検知が複雑

**採用方針:** 
まず方式Aで実装し、遅延が許容範囲か検証する。

### 6.2 マルチモニター対応
- `System.Windows.Forms.Screen.AllScreens`または`EnumDisplayMonitors`でモニター情報取得
- 各モニターの座標系を考慮したウィンドウ配置
- カスケード配置時、モニター境界を超えないよう制限

### 6.3 パフォーマンス考慮
- グループ化処理: バックグラウンドスレッドで実行
- ウィンドウ位置更新: `SWP_ASYNCWINDOWPOS`フラグ使用
- 連動ドラッグ: デバウンス処理（16ms間隔）

---

## 7. 実装フェーズ

### Phase 1: グループ化機能
- [ ] `WindowGroup`モデル作成
- [ ] `GroupingService`実装
- [ ] TreeView UIへの変更
- [ ] グループ展開/折りたたみ機能

### Phase 2: プリセット機能
- [ ] `WindowPreset`モデル作成
- [ ] `PresetService`実装（保存・読込）
- [ ] プリセットUI（選択・適用）
- [ ] 現在状態からのプリセット作成

### Phase 3: ウィンドウリサイズ・位置機能
- [ ] `WindowService`拡張（GetWindowRect/SetWindowRect）
- [ ] `WindowInfo`拡張（座標・サイズプロパティ）
- [ ] マルチモニター情報取得

### Phase 4: カスケード配置
- [ ] `CascadeSettings`モデル作成
- [ ] `ArrangementService`実装
- [ ] カスケード方向選択UI
- [ ] オフセット設定UI

### Phase 5: 連動ドラッグ
- [ ] `LinkedDragService`実装
- [ ] WinEventHook統合
- [ ] 連動ドラッグ開始/終了UI
- [ ] パフォーマンス検証・調整

---

## 8. リスクと対策

| リスク | 影響度 | 対策 |
|--------|--------|------|
| 連動ドラッグの遅延 | 中 | デバウンス調整、非同期ウィンドウ移動 |
| 大量ウィンドウ時のパフォーマンス | 中 | 仮想化ListView、バッチ処理 |
| 特定アプリでのWin32 API失敗 | 低 | エラーハンドリング、スキップ処理 |
| マルチモニター座標計算 | 中 | 十分なテスト、境界チェック |

---

*Document Version: 1.0*
*Last Updated: 2024-XX-XX*
