using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ChocoboColorCalculator.Core.Data;
using ChocoboColorCalculator.Core.Models;
using ChocoboColorCalculator.Core.Services;

namespace ChocoboColorCalculator.Desktop;

public partial class MainWindow : Window
{
    private readonly RouteCalculator calculator = new();
    private readonly List<ColorOption> allColors;
    private readonly DesktopState state;
    private bool isInitializing = true;

    public ObservableCollection<RouteStepItem> RouteSteps { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        allColors = ChocoboData.Colors
            .Select((color, index) => new ColorOption(index, color.Name, color.Rgb, BrushFor(color.Rgb)))
            .ToList();
        state = DesktopStateStore.Load();
        state.CurrentColorIndex = Math.Clamp(state.CurrentColorIndex, 0, allColors.Count - 1);
        state.TargetColorIndex = Math.Clamp(state.TargetColorIndex, 0, allColors.Count - 1);

        CurrentColorCombo.ItemsSource = allColors;
        TargetColorCombo.ItemsSource = allColors;
        CurrentColorCombo.SelectedItem = allColors[state.CurrentColorIndex];
        TargetColorCombo.SelectedItem = allColors[state.TargetColorIndex];
        UpdateSelectedColorCards();
        RebuildActiveRoute();
        isInitializing = false;
        Closing += (_, _) => SaveState();
    }

    private string ExportDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Chocobo Color Calculator",
        "Exports");

    private void CurrentSearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        FilterColors(CurrentColorCombo, CurrentSearchBox.Text, state.CurrentColorIndex);

    private void TargetSearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        FilterColors(TargetColorCombo, TargetSearchBox.Text, state.TargetColorIndex);

    private void FilterColors(ComboBox combo, string search, int selectedIndex)
    {
        if (allColors is null)
            return;
        var query = search.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? allColors
            : allColors.Where(color => color.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        combo.ItemsSource = filtered;
        var selected = filtered.FirstOrDefault(color => color.Index == selectedIndex);
        if (selected is not null)
            combo.SelectedItem = selected;
        if (filtered.Count > 0)
            combo.IsDropDownOpen = !string.IsNullOrWhiteSpace(query);
    }

    private void CurrentColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CurrentColorCombo.SelectedItem is not ColorOption selected)
            return;
        state.CurrentColorIndex = selected.Index;
        UpdateSelectedColorCards();
        if (!isInitializing)
            SaveState();
    }

    private void TargetColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TargetColorCombo.SelectedItem is not ColorOption selected)
            return;
        state.TargetColorIndex = selected.Index;
        UpdateSelectedColorCards();
        if (!isInitializing)
            SaveState();
    }

    private void UpdateSelectedColorCards()
    {
        var current = allColors[state.CurrentColorIndex];
        var target = allColors[state.TargetColorIndex];
        CurrentNameText.Text = current.Name;
        CurrentRgbText.Text = RgbLabel(current.Rgb);
        CurrentSwatch.Background = current.Brush;
        TargetNameText.Text = target.Name;
        TargetRgbText.Text = RgbLabel(target.Rgb);
        TargetSwatch.Background = target.Brush;
    }

    private void CalculateButton_Click(object sender, RoutedEventArgs e)
    {
        var start = ChocoboData.Colors[state.CurrentColorIndex];
        var target = ChocoboData.Colors[state.TargetColorIndex];
        var result = calculator.Calculate(start, target);
        state.ActiveRoute = new DesktopRouteState
        {
            StartName = start.Name,
            TargetName = target.Name,
            StartR = start.Rgb.R,
            StartG = start.Rgb.G,
            StartB = start.Rgb.B,
            TargetR = target.Rgb.R,
            TargetG = target.Rgb.G,
            TargetB = target.Rgb.B,
            AimR = result.AimPoint.R,
            AimG = result.AimPoint.G,
            AimB = result.AimPoint.B,
            EndR = result.Endpoint.R,
            EndG = result.Endpoint.G,
            EndB = result.Endpoint.B,
            PredictedColorName = result.PredictedColor.Name,
            ClassificationMargin = result.ClassificationMargin,
            Warning = result.Warning,
            CreatedAtUtc = DateTime.UtcNow,
            Steps = result.Steps.Select(kind => new DesktopStepState { FruitKind = (int)kind }).ToList(),
        };
        RebuildActiveRoute();
        SaveState();
        SetStatus($"Calculated {start.Name} to {target.Name}: {result.Steps.Count} ordered feeds.", false);
    }

    private void SwapButton_Click(object sender, RoutedEventArgs e)
    {
        (state.CurrentColorIndex, state.TargetColorIndex) = (state.TargetColorIndex, state.CurrentColorIndex);
        CurrentSearchBox.Text = string.Empty;
        TargetSearchBox.Text = string.Empty;
        CurrentColorCombo.ItemsSource = allColors;
        TargetColorCombo.ItemsSource = allColors;
        CurrentColorCombo.SelectedItem = allColors[state.CurrentColorIndex];
        TargetColorCombo.SelectedItem = allColors[state.TargetColorIndex];
        UpdateSelectedColorCards();
        SaveState();
    }

    private void RebuildActiveRoute()
    {
        RouteSteps.Clear();
        var route = state.ActiveRoute;
        if (route is null)
        {
            EmptyStatePanel.Visibility = Visibility.Visible;
            ActiveRoutePanel.Visibility = Visibility.Collapsed;
            return;
        }

        var rgb = new RgbColor(route.StartR, route.StartG, route.StartB);
        for (var index = 0; index < route.Steps.Count; index++)
        {
            var model = route.Steps[index];
            var fruit = (FruitKind)model.FruitKind;
            var definition = ChocoboData.Fruit(fruit);
            rgb = definition.Apply(rgb);
            RouteSteps.Add(new RouteStepItem(
                model,
                index + 1,
                definition.Name,
                EffectLabel(definition.Delta),
                RgbChannels(rgb),
                FruitBrush(fruit),
                BrushFor(rgb)));
        }

        EmptyStatePanel.Visibility = Visibility.Collapsed;
        ActiveRoutePanel.Visibility = Visibility.Visible;
        TotalFeedsText.Text = route.Steps.Count.ToString("N0");
        PredictedColorText.Text = route.PredictedColorName;
        EndpointRgbText.Text = RgbLabel(new RgbColor(route.EndR, route.EndG, route.EndB));
        MarginText.Text = route.ClassificationMargin.ToString("F2");
        RouteTitleText.Text = $"{route.StartName}  →  {route.TargetName}";
        ShoppingListItems.ItemsSource = route.Steps
            .GroupBy(step => (FruitKind)step.FruitKind)
            .Select(group => new ShoppingItem(
                ChocoboData.Fruit(group.Key).Name,
                $"×{group.Count()}",
                FruitBrush(group.Key)))
            .ToList();
        RefreshTrackingUi();
    }

    private void RefreshTrackingUi()
    {
        var route = state.ActiveRoute;
        if (route is null)
            return;
        var nextIndex = route.Steps.FindIndex(step => !step.IsComplete);
        for (var index = 0; index < RouteSteps.Count; index++)
        {
            RouteSteps[index].IsNext = index == nextIndex;
            RouteSteps[index].CanToggle = index == nextIndex || RouteSteps[index].IsComplete;
        }

        var completed = route.Steps.Count(step => step.IsComplete);
        ProgressText.Text = $"{completed} / {route.Steps.Count}";
        RouteProgressBar.Maximum = Math.Max(1, route.Steps.Count);
        RouteProgressBar.Value = completed;
        ConfirmNextButton.IsEnabled = nextIndex >= 0;
        if (nextIndex < 0)
        {
            NextStepLabel.Text = "ROUTE COMPLETE";
            NextFruitText.Text = "Wait six Earth hours";
            NextFruitInitial.Text = "✓";
            NextInstructionText.Text = "Leave your chocobo stabled; removing it early cancels the pending change.";
        }
        else
        {
            var fruit = ChocoboData.Fruit((FruitKind)route.Steps[nextIndex].FruitKind);
            NextStepLabel.Text = $"NEXT FEED  ·  STEP {nextIndex + 1} OF {route.Steps.Count}";
            NextFruitText.Text = fruit.Name;
            NextFruitInitial.Text = fruit.Name[..1].ToUpperInvariant();
            NextInstructionText.Text = "Feed exactly one, then confirm it below.";
        }
        RouteGrid.Items.Refresh();
    }

    private void StepCheckBox_Click(object sender, RoutedEventArgs e)
    {
        RefreshTrackingUi();
        SaveState();
    }

    private void ConfirmNextButton_Click(object sender, RoutedEventArgs e)
    {
        var route = state.ActiveRoute;
        var next = route?.Steps.FindIndex(step => !step.IsComplete) ?? -1;
        if (route is null || next < 0)
            return;
        route.Steps[next].IsComplete = true;
        RouteSteps[next].SyncFromModel();
        RefreshTrackingUi();
        SaveState();
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        var route = state.ActiveRoute;
        var previous = route?.Steps.FindLastIndex(step => step.IsComplete) ?? -1;
        if (route is null || previous < 0)
            return;
        route.Steps[previous].IsComplete = false;
        RouteSteps[previous].SyncFromModel();
        RefreshTrackingUi();
        SaveState();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        var route = state.ActiveRoute;
        if (route is null)
            return;
        foreach (var step in route.Steps)
            step.IsComplete = false;
        foreach (var step in RouteSteps)
            step.SyncFromModel();
        RefreshTrackingUi();
        SaveState();
        SetStatus("Route progress reset.", false);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        state.ActiveRoute = null;
        RebuildActiveRoute();
        SaveState();
        SetStatus("Route cleared.", false);
    }

    private void ExportPdfButton_Click(object sender, RoutedEventArgs e) => ExportRoute(RouteExportFormat.Pdf);
    private void ExportTextButton_Click(object sender, RoutedEventArgs e) => ExportRoute(RouteExportFormat.Text);
    private void ExportHtmlButton_Click(object sender, RoutedEventArgs e) => ExportRoute(RouteExportFormat.Html);

    private void ExportRoute(RouteExportFormat format)
    {
        try
        {
            var document = BuildExportDocument();
            var path = RouteExporter.Export(document, format, ExportDirectory);
            SetStatus($"{format.ToString().ToUpperInvariant()} exported: {path}", false);
        }
        catch (Exception exception)
        {
            SetStatus($"Export failed: {exception.Message}", true);
        }
    }

    private RouteExportDocument BuildExportDocument()
    {
        var route = state.ActiveRoute ?? throw new InvalidOperationException("Calculate a route before exporting it.");
        var rgb = new RgbColor(route.StartR, route.StartG, route.StartB);
        var steps = new List<RouteExportStep>(route.Steps.Count);
        for (var index = 0; index < route.Steps.Count; index++)
        {
            var fruit = (FruitKind)route.Steps[index].FruitKind;
            var definition = ChocoboData.Fruit(fruit);
            rgb = definition.Apply(rgb);
            steps.Add(new RouteExportStep(index + 1, fruit, definition.Name, rgb));
        }
        return new RouteExportDocument(
            route.StartName,
            new RgbColor(route.StartR, route.StartG, route.StartB),
            route.TargetName,
            new RgbColor(route.TargetR, route.TargetG, route.TargetB),
            route.PredictedColorName,
            new RgbColor(route.AimR, route.AimG, route.AimB),
            new RgbColor(route.EndR, route.EndG, route.EndB),
            route.ClassificationMargin,
            route.CreatedAtUtc,
            steps,
            route.Warning);
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(ExportDirectory);
            Process.Start(new ProcessStartInfo { FileName = ExportDirectory, UseShellExecute = true });
            SetStatus($"Opened {ExportDirectory}", false);
        }
        catch (Exception exception)
        {
            SetStatus($"Could not open export folder: {exception.Message}", true);
        }
    }

    private void SaveState()
    {
        try
        {
            DesktopStateStore.Save(state);
        }
        catch (Exception exception)
        {
            SetStatus($"Could not save progress: {exception.Message}", true);
        }
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? new SolidColorBrush(Color.FromRgb(255, 104, 112)) : new SolidColorBrush(Color.FromRgb(152, 167, 196));
    }

    private static string RgbLabel(RgbColor rgb) => $"RGB {rgb.R} / {rgb.G} / {rgb.B}  ·  {rgb.Hex}";
    private static string RgbChannels(RgbColor rgb) => $"{rgb.R}/{rgb.G}/{rgb.B}";
    private static string EffectLabel(RgbColor delta) => $"R{Signed(delta.R)}  G{Signed(delta.G)}  B{Signed(delta.B)}";
    private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();

    private static SolidColorBrush BrushFor(RgbColor rgb)
    {
        var brush = new SolidColorBrush(Color.FromRgb((byte)rgb.R, (byte)rgb.G, (byte)rgb.B));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush FruitBrush(FruitKind fruit) => fruit switch
    {
        FruitKind.XelphatolApple => FrozenBrush(235, 55, 64),
        FruitKind.MamookPear => FrozenBrush(60, 184, 90),
        FruitKind.OGhomoroBerries => FrozenBrush(58, 119, 234),
        FruitKind.DomanPlum => FrozenBrush(64, 191, 205),
        FruitKind.Valfruit => FrozenBrush(200, 78, 173),
        FruitKind.CieldalaesPineapple => FrozenBrush(241, 165, 34),
        _ => FrozenBrush(140, 150, 170),
    };

    private static SolidColorBrush FrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private sealed record ColorOption(int Index, string Name, RgbColor Rgb, SolidColorBrush Brush);
    private sealed record ShoppingItem(string Name, string CountText, SolidColorBrush Brush);
}

public sealed class RouteStepItem : INotifyPropertyChanged
{
    private readonly DesktopStepState model;
    private bool isNext;
    private bool canToggle;

    public RouteStepItem(
        DesktopStepState model,
        int number,
        string fruitName,
        string effectText,
        string rgbText,
        SolidColorBrush fruitBrush,
        SolidColorBrush rgbBrush)
    {
        this.model = model;
        NumberText = number.ToString("00");
        FruitName = fruitName;
        EffectText = effectText;
        RgbText = rgbText;
        FruitBrush = fruitBrush;
        RgbBrush = rgbBrush;
    }

    public string NumberText { get; }
    public string FruitName { get; }
    public string EffectText { get; }
    public string RgbText { get; }
    public SolidColorBrush FruitBrush { get; }
    public SolidColorBrush RgbBrush { get; }

    public bool IsComplete
    {
        get => model.IsComplete;
        set
        {
            if (model.IsComplete == value)
                return;
            model.IsComplete = value;
            OnPropertyChanged();
        }
    }

    public bool IsNext
    {
        get => isNext;
        set
        {
            if (isNext == value)
                return;
            isNext = value;
            OnPropertyChanged();
        }
    }

    public bool CanToggle
    {
        get => canToggle;
        set
        {
            if (canToggle == value)
                return;
            canToggle = value;
            OnPropertyChanged();
        }
    }

    public void SyncFromModel() => OnPropertyChanged(nameof(IsComplete));

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
