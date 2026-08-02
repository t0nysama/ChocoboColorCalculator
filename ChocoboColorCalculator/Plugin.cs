using ChocoboColorCalculator.Core.Data;
using ChocoboColorCalculator.Core.Models;
using ChocoboColorCalculator.Core.Services;
using ChocoboColorCalculator.Windows;
using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace ChocoboColorCalculator;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/chococolor";
    private static readonly TimeSpan DetectionDebounce = TimeSpan.FromMilliseconds(400);

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly RouteCalculator calculator = new();
    private readonly Dictionary<FruitKind, string> localizedFruitNames = [];
    private readonly Dictionary<FruitKind, uint> fruitIconIds = [];
    private readonly WindowSystem windowSystem = new("ChocoboColorCalculator");
    private readonly MainWindow mainWindow;
    private DateTime lastDetectionUtc = DateTime.MinValue;
    private FruitKind? lastDetectedFruit;

    internal Configuration Configuration { get; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        NormalizeConfiguration();
        LoadLocalizedFruitNames();

        mainWindow = new MainWindow(this, calculator);
        windowSystem.AddWindow(mainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Chocobo Color Calculator.",
        });
        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMainUi;
        ChatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        ChatGui.ChatMessage -= OnChatMessage;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMainUi;
        CommandManager.RemoveHandler(CommandName);
        windowSystem.RemoveAllWindows();
        mainWindow.Dispose();
    }

    internal void CreatePlan()
    {
        var start = ChocoboData.Colors[Configuration.CurrentColorIndex];
        var target = ChocoboData.Colors[Configuration.TargetColorIndex];
        var mode = Configuration.UseSafeCenter ? TargetMode.SafeCenter : TargetMode.PublishedRgb;
        var result = calculator.Calculate(start, target, mode);

        Configuration.ActivePlan = new ActivePlanState
        {
            StartName = start.Name,
            TargetName = target.Name,
            StartR = start.Rgb.R,
            StartG = start.Rgb.G,
            StartB = start.Rgb.B,
            AimR = result.AimPoint.R,
            AimG = result.AimPoint.G,
            AimB = result.AimPoint.B,
            EndR = result.Endpoint.R,
            EndG = result.Endpoint.G,
            EndB = result.Endpoint.B,
            PredictedColorName = result.PredictedColor.Name,
            ClassificationMargin = result.ClassificationMargin,
            UsedFallback = result.UsedFallback,
            Warning = result.Warning,
            Steps = result.Steps.Select(kind => new TrackedStepState { FruitKind = (int)kind }).ToList(),
        };
        Configuration.LastDetectionNotice = null;
        Configuration.Save();
    }

    internal void SetManualStep(int index, bool value)
    {
        var plan = Configuration.ActivePlan;
        if (plan is null || index < 0 || index >= plan.Steps.Count)
            return;

        var next = plan.NextStepIndex;
        if (value && next != index && !plan.Steps[index].AutoCompleted)
            return;
        plan.Steps[index].ManualCompleted = value;
        Configuration.Save();
    }

    internal void MarkNextManually()
    {
        var index = Configuration.ActivePlan?.NextStepIndex ?? -1;
        if (index >= 0)
            SetManualStep(index, true);
    }

    internal void UndoLastStep()
    {
        var plan = Configuration.ActivePlan;
        if (plan is null)
            return;
        var index = plan.Steps.FindLastIndex(step => step.IsComplete);
        if (index < 0)
            return;
        plan.Steps[index].ManualCompleted = false;
        plan.Steps[index].AutoCompleted = false;
        Configuration.LastDetectionNotice = null;
        Configuration.Save();
    }

    internal void ResetProgress()
    {
        if (Configuration.ActivePlan is null)
            return;
        foreach (var step in Configuration.ActivePlan.Steps)
        {
            step.ManualCompleted = false;
            step.AutoCompleted = false;
        }
        Configuration.LastDetectionNotice = null;
        Configuration.Save();
    }

    internal void ClearPlan()
    {
        Configuration.ActivePlan = null;
        Configuration.LastDetectionNotice = null;
        Configuration.Save();
    }

    internal string LocalizedFruitName(FruitKind kind) =>
        localizedFruitNames.GetValueOrDefault(kind, ChocoboData.Fruit(kind).Name);

    internal uint FruitIconId(FruitKind kind) => fruitIconIds.GetValueOrDefault(kind);

    private void OnCommand(string command, string arguments) => mainWindow.Toggle();
    private void ToggleMainUi() => mainWindow.Toggle();

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (!Configuration.AutoTrackingEnabled || message.LogKind != XivChatType.Progress)
            return;

        var plan = Configuration.ActivePlan;
        var next = plan?.NextStepIndex ?? -1;
        if (plan is null || next < 0)
            return;

        var text = message.Message.TextValue;
        FruitKind? detected = null;
        foreach (var fruit in ChocoboData.Fruits)
        {
            if (text.Contains(LocalizedFruitName(fruit.Kind), StringComparison.CurrentCultureIgnoreCase))
            {
                detected = fruit.Kind;
                break;
            }
        }

        if (detected is null)
            return;
        var now = DateTime.UtcNow;
        if (lastDetectedFruit == detected && now - lastDetectionUtc < DetectionDebounce)
            return;
        lastDetectedFruit = detected;
        lastDetectionUtc = now;

        var expected = (FruitKind)plan.Steps[next].FruitKind;
        if (detected != expected)
        {
            var notice =
                $"Detected {LocalizedFruitName(detected.Value)}, but step {next + 1} expects " +
                $"{LocalizedFruitName(expected)}. Progress was not advanced.";
            Configuration.LastDetectionNotice = notice;
            Configuration.Save();
            ChatGui.PrintError(notice, "Chocobo Color");
            return;
        }

        plan.Steps[next].AutoCompleted = true;
        Configuration.LastDetectionNotice = $"Automatically detected step {next + 1}: {LocalizedFruitName(expected)}.";
        Configuration.Save();

        if (!Configuration.PrintNextStepToChat)
            return;
        var following = plan.NextStepIndex;
        if (following < 0)
            ChatGui.Print("Feeding route complete. Leave your chocobo stabled for six hours.", "Chocobo Color");
        else
            ChatGui.Print(
                $"Step {following + 1}/{plan.Steps.Count}: feed " +
                $"{LocalizedFruitName((FruitKind)plan.Steps[following].FruitKind)}.",
                "Chocobo Color");
    }

    private void LoadLocalizedFruitNames()
    {
        var items = DataManager.GetExcelSheet<Item>();
        foreach (var fruit in ChocoboData.Fruits)
        {
            if (items.TryGetRow(fruit.ItemId, out var item) &&
                !string.IsNullOrWhiteSpace(item.Name.ToString()))
            {
                localizedFruitNames[fruit.Kind] = item.Name.ToString();
                fruitIconIds[fruit.Kind] = item.Icon;
            }
            else
            {
                localizedFruitNames[fruit.Kind] = fruit.Name;
                fruitIconIds[fruit.Kind] = 0;
            }
        }
    }

    private void NormalizeConfiguration()
    {
        Configuration.CurrentColorIndex = Math.Clamp(Configuration.CurrentColorIndex, 0, ChocoboData.Colors.Count - 1);
        Configuration.TargetColorIndex = Math.Clamp(Configuration.TargetColorIndex, 0, ChocoboData.Colors.Count - 1);
    }
}
