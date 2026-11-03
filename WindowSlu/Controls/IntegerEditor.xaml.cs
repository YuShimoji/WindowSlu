using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace WindowSlu.Controls;

public partial class IntegerEditor : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(int), typeof(IntegerEditor), new PropertyMetadata(100, Changed));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(int), typeof(IntegerEditor), new PropertyMetadata(0, Changed));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(int), typeof(IntegerEditor), new PropertyMetadata(100, Changed));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(IntegerEditor), new PropertyMetadata("%", Changed));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(IntegerEditor), new PropertyMetadata("Opacity", Changed));
    public static readonly DependencyProperty IsSettingProperty = DependencyProperty.Register(nameof(IsSetting), typeof(bool), typeof(IntegerEditor), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty IsDeferredProperty = DependencyProperty.Register(nameof(IsDeferred), typeof(bool), typeof(IntegerEditor), new PropertyMetadata(false, Changed));
    public bool IsDeferred { get => (bool)GetValue(IsDeferredProperty); set => SetValue(IsDeferredProperty, value); }
    public int Value { get => (int)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int Minimum { get => (int)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public int Maximum { get => (int)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsSetting { get => (bool)GetValue(IsSettingProperty); set => SetValue(IsSettingProperty, value); }
    public event EventHandler<IntegerValueRequest>? ValueRequested;
    private bool _syncing, _dirty, _dragging, _requesting;
    private int _step = 1;

    public IntegerEditor()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh(true);
    }

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((IntegerEditor)d).Refresh(false);
    private void Refresh(bool options)
    {
        if (NumberBox == null || Maximum < Minimum) return;
        _syncing = true;
        try
        {
            Caption.Text = Label;
            NumberBox.ToolTip = $"Whole number {Minimum}–{Maximum} {Unit}. " + (IsDeferred ? "Enter / Use selects a value; Apply changes windows." : "Enter / Set applies.") + " Escape restores the current value.";
            CurrentCaption.Text = ValueCaption(Value);
            SetButton.Content = IsDeferred ? "Use" : "Set";
            if (!_dirty && !_requesting) NumberBox.Text = Value.ToString(CultureInfo.InvariantCulture);
            if (!_dragging) ConfigureRange(Value);
            else FineAdjustment.Value = Math.Clamp(Value, FineAdjustment.Minimum, FineAdjustment.Maximum);
            if (options || StepPicker.Items.Count == 0 || ((ComboBoxItem)StepPicker.Items[0]).Content?.ToString() != $"1 {Unit}")
            {
                StepPicker.Items.Clear();
                foreach (int step in Unit == "px" ? new[] { 1, 10, 50, 100 } : new[] { 1, 5, 10 })
                    StepPicker.Items.Add(new ComboBoxItem { Content = $"{step} {Unit}", Tag = step });
                StepPicker.SelectedIndex = 0; _step = 1;
            }
            string name = AutomationProperties.GetName(this);
            if (string.IsNullOrEmpty(name)) name = Label + " " + Unit;
            AutomationProperties.SetName(FineAdjustment, name);
            AutomationProperties.SetName(NumberBox, Label + " exact value " + Unit);
            AutomationProperties.SetName(StepPicker, Label + " adjustment step");
            AutomationProperties.SetName(SetButton, (IsDeferred ? "Use configured " : "Set ") + Label);
            AutomationProperties.SetName(MinusButton, "Decrease " + Label + " to previous step");
            AutomationProperties.SetName(PlusButton, "Increase " + Label + " to next step");
            AutomationProperties.SetName(ValuesButton, "Common " + Label + " values");
        }
        finally { _syncing = false; }
    }

    private void ConfigureRange(int center)
    {
        center = Math.Clamp(center, Minimum, Maximum);
        int radius = (Unit == "px" ? 50 : 10) * _step;
        // Fine by default; selecting a larger step deliberately widens the drag range.
        double minimum = Math.Max(Minimum, Math.Floor((center - radius) / (double)_step) * _step);
        double maximum = Math.Min(Maximum, Math.Ceiling((center + radius) / (double)_step) * _step);
        if (maximum <= minimum) maximum = minimum + 1;
        FineAdjustment.Minimum = Math.Min(minimum, FineAdjustment.Maximum - 1);
        FineAdjustment.Maximum = maximum;
        FineAdjustment.Minimum = minimum;
        FineAdjustment.SmallChange = _step; FineAdjustment.LargeChange = _step * 10; FineAdjustment.TickFrequency = _step;
        FineAdjustment.Ticks = new DoubleCollection(Enumerable.Range((int)Math.Ceiling(minimum / _step), Math.Max(0, (int)Math.Floor(maximum / _step) - (int)Math.Ceiling(minimum / _step) + 1)).Select(i => (double)i * _step));
        FineAdjustment.Value = Math.Clamp(center, minimum, maximum);
        RangeCaption.Text = $"Fine {minimum:0}–{maximum:0} {Unit}";
    }

    private void NumberChanged(object sender, TextChangedEventArgs e) { if (!_syncing) { _dirty = true; CurrentCaption.Text = ValueCaption(Value); } }
    private void NumberKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { CommitText(); e.Handled = true; }
        else if (e.Key == Key.Escape) { _dirty = false; ErrorCaption.Visibility = Visibility.Collapsed; Refresh(false); e.Handled = true; }
    }
    private void SetClicked(object sender, RoutedEventArgs e) => CommitText();
    private void CommitText()
    {
        if (!int.TryParse(NumberBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < Minimum || value > Maximum)
        { ShowError($"Enter a whole number from {Minimum} to {Maximum} {Unit}. Escape restores the current value."); return; }
        Request(value);
    }
    private void Request(int requested)
    {
        var request = new IntegerValueRequest(requested, Value);
        _requesting = true;
        try { ValueRequested?.Invoke(this, request); }
        finally { _requesting = false; }
        _dirty = false; _syncing = true;
        NumberBox.Text = request.ActualValue.ToString(CultureInfo.InvariantCulture);
        CurrentCaption.Text = ValueCaption(request.ActualValue);
        if (!_dragging) ConfigureRange(request.ActualValue);
        _syncing = false;
        if (!request.Success) ShowError(request.Message ?? "The operation failed; the actual value has been reloaded.");
        else ErrorCaption.Visibility = Visibility.Collapsed;
    }
    private string ValueCaption(int value) => $"{(IsDeferred ? "Configured" : IsSetting ? "Target" : "Actual")}: {value} {Unit}"
        + (_dirty ? " — pending: Enter / " + (IsDeferred ? "Use" : "Set") : IsDeferred ? " — Apply required" : "");
    private void ShowError(string message) { ErrorCaption.Text = message; ErrorCaption.Visibility = Visibility.Visible; }
    private void StepChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || StepPicker.SelectedItem is not ComboBoxItem { Tag: int step }) return;
        _step = step; _syncing = true; ConfigureRange(Value); _syncing = false;
    }
    private void MinusClicked(object sender, RoutedEventArgs e) => Request(Math.Max(Minimum, (int)(Math.Ceiling(Value / (double)_step) - 1) * _step));
    private void PlusClicked(object sender, RoutedEventArgs e) => Request(Math.Min(Maximum, (int)(Math.Floor(Value / (double)_step) + 1) * _step));
    private void SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || !(FineAdjustment.IsMouseCaptureWithin || FineAdjustment.IsKeyboardFocusWithin)) return;
        Request((int)Math.Round(e.NewValue));
    }
    private void DragStarted(object sender, DragStartedEventArgs e) => _dragging = true;
    private void DragCompleted(object sender, DragCompletedEventArgs e) { _dragging = false; _syncing = true; ConfigureRange(Value); _syncing = false; }
    private void OpenValues(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        int[] candidates = Unit != "px" ? new[] { 25, 50, 75, 80, 100 }
            : Label.Contains("Height") ? new[] { 480, 600, 720, 900, 1080, 1440 } : new[] { 640, 800, 1024, 1280, 1600, 1920, 2560 };
        foreach (int value in candidates.Where(v => v >= Minimum && v <= Maximum))
        {
            var item = new MenuItem { Header = $"{(IsDeferred ? "Use" : "Set")} {value} {Unit}", Tag = value };
            item.Click += (_, _) => Request(value); menu.Items.Add(item);
        }
        ValuesButton.ContextMenu = menu; menu.PlacementTarget = ValuesButton; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true;
    }
}

public sealed class IntegerValueRequest(int requestedValue, int actualValue) : EventArgs
{
    public int RequestedValue { get; } = requestedValue;
    public int ActualValue { get; set; } = actualValue;
    public bool Success { get; set; }
    public string? Message { get; set; }
}
