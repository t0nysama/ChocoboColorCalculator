using System.Numerics;
using ChocoboColorCalculator.Core.Data;
using ChocoboColorCalculator.Core.Models;
using ChocoboColorCalculator.Core.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace ChocoboColorCalculator.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private static readonly Vector4 AccentGold = new(1.00f, 0.72f, 0.24f, 1f);
    private static readonly Vector4 AccentCoral = new(1.00f, 0.39f, 0.42f, 1f);
    private static readonly Vector4 AccentBlue = new(0.31f, 0.68f, 1.00f, 1f);
    private static readonly Vector4 AccentViolet = new(0.62f, 0.42f, 1.00f, 1f);
    private static readonly Vector4 Success = new(0.31f, 0.90f, 0.62f, 1f);
    private static readonly Vector4 Danger = new(1.00f, 0.42f, 0.42f, 1f);
    private static readonly Vector4 TextPrimary = new(0.93f, 0.95f, 1.00f, 1f);
    private static readonly Vector4 TextMuted = new(0.57f, 0.62f, 0.72f, 1f);
    private static readonly Vector4 Canvas = new(0.025f, 0.032f, 0.055f, 0.98f);
    private static readonly Vector4 Glass = new(0.075f, 0.092f, 0.135f, 0.82f);
    private static readonly Vector4 GlassRaised = new(0.105f, 0.125f, 0.18f, 0.88f);
    private static readonly Vector4 GoldGlass = new(0.18f, 0.125f, 0.055f, 0.88f);
    private static readonly Vector4 BlueGlass = new(0.055f, 0.115f, 0.205f, 0.88f);
    private static readonly Vector4 GreenGlass = new(0.045f, 0.16f, 0.115f, 0.88f);

    private readonly Plugin plugin;
    private readonly Dictionary<string, float> hoverAnimations = [];
    private float entranceProgress;
    private float displayedProgress;
    private float routeReveal = 1f;
    private DateTime observedPlanCreatedAt;
    private bool showGuide;
    private string currentColorSearch = string.Empty;
    private string targetColorSearch = string.Empty;
    private string? exportNotice;
    private bool exportNoticeIsError;

    public MainWindow(Plugin plugin)
        : base("Chocobo Color Calculator##Main")
    {
        this.plugin = plugin;
        Flags = ImGuiWindowFlags.None;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(820, 720),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        var deltaTime = Math.Clamp(ImGui.GetIO().DeltaTime, 0f, 0.1f);
        entranceProgress = SmoothTowards(entranceProgress, 1f, 8f, deltaTime);
        routeReveal = SmoothTowards(routeReveal, 1f, 9f, deltaTime);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14, 12) * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(9, 5) * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 8f * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 14f * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 10f * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6) * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleColor(ImGuiCol.Text, TextPrimary);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Canvas);
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.35f, 0.45f, 0.68f, 0.24f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.08f, 0.10f, 0.15f, 0.92f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.12f, 0.15f, 0.22f, 0.96f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.15f, 0.18f, 0.27f, 1f));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, AccentBlue);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, new Vector4(0.02f, 0.03f, 0.05f, 0.55f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, new Vector4(0.25f, 0.34f, 0.52f, 0.68f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, new Vector4(0.35f, 0.48f, 0.72f, 0.82f));

        DrawAmbientBackground();
        DrawHeader(deltaTime);
        ImGui.Dummy(new Vector2(1, 8) * ImGuiHelpers.GlobalScale);

        var slide = (1f - EaseOutCubic(entranceProgress)) * 14f * ImGuiHelpers.GlobalScale;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + slide);
        if (showGuide)
            DrawHelp();
        else
            DrawCalculator(deltaTime);

        ImGui.PopStyleColor(10);
        ImGui.PopStyleVar(6);
    }

    private static float SmoothTowards(float current, float target, float speed, float deltaTime) =>
        current + (target - current) * (1f - MathF.Exp(-speed * deltaTime));

    private static float EaseOutCubic(float value)
    {
        var inverse = 1f - Math.Clamp(value, 0f, 1f);
        return 1f - inverse * inverse * inverse;
    }

    private void DrawAmbientBackground()
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos() - new Vector2(14, 12) * ImGuiHelpers.GlobalScale;
        var max = ImGui.GetWindowPos() + ImGui.GetWindowSize() - new Vector2(2, 2) * ImGuiHelpers.GlobalScale;
        drawList.AddRectFilledMultiColor(
            min,
            max,
            ImGui.GetColorU32(new Vector4(0.035f, 0.045f, 0.08f, 0.98f)),
            ImGui.GetColorU32(new Vector4(0.055f, 0.035f, 0.095f, 0.98f)),
            ImGui.GetColorU32(new Vector4(0.018f, 0.028f, 0.052f, 0.99f)),
            ImGui.GetColorU32(new Vector4(0.018f, 0.038f, 0.065f, 0.99f)));

        var pulse = 0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * 0.65f);
        drawList.AddCircleFilled(
            min + new Vector2(ImGui.GetWindowSize().X * 0.82f, 150 * ImGuiHelpers.GlobalScale),
            (100 + pulse * 14) * ImGuiHelpers.GlobalScale,
            ImGui.GetColorU32(new Vector4(0.34f, 0.24f, 0.82f, 0.045f)));
        drawList.AddCircleFilled(
            min + new Vector2(75 * ImGuiHelpers.GlobalScale, ImGui.GetWindowSize().Y * 0.68f),
            105 * ImGuiHelpers.GlobalScale,
            ImGui.GetColorU32(new Vector4(0.08f, 0.48f, 0.78f, 0.035f)));
    }

    private void DrawHeader(float deltaTime)
    {
        var height = 88 * ImGuiHelpers.GlobalScale;
        BeginGlassPanel("##heroHeader", new Vector2(0, height), GlassRaised, AccentViolet, AccentBlue);
        if (ImGui.BeginTable("##headerLayout", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Brand", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Navigation", ImGuiTableColumnFlags.WidthFixed, 224 * ImGuiHelpers.GlobalScale);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextColored(AccentGold, "CHOCOBO COLOR");
            ImGui.SameLine();
            ImGui.TextColored(TextPrimary, "CALCULATOR");
            ImGui.TextColored(TextMuted, "Reliable routes, live feeding progress, zero browser tabs.");
            ImGui.Spacing();
            ImGui.TextColored(Success, "●  RELIABLE ENGINE");
            ImGui.SameLine();
            ImGui.TextColored(TextMuted, "7,225 color routes verified");

            ImGui.TableNextColumn();
            ImGui.Dummy(new Vector2(1, 4) * ImGuiHelpers.GlobalScale);
            if (NavigationButton("ROUTE##navRoute", !showGuide, deltaTime))
                showGuide = false;
            ImGui.SameLine();
            if (NavigationButton("GUIDE##navGuide", showGuide, deltaTime))
                showGuide = true;
            ImGui.EndTable();
        }
        EndGlassPanel();
    }

    private void DrawCalculator(float deltaTime)
    {
        DrawSectionHeading("CREATE A ROUTE", "The reliable calculation model is applied automatically to every color pair.");
        DrawColorSelection();
        ImGui.Dummy(new Vector2(1, 4) * ImGuiHelpers.GlobalScale);
        DrawCalculateActions(deltaTime);

        var plan = plugin.Configuration.ActivePlan;
        if (plan is null)
        {
            ImGui.Dummy(new Vector2(1, 8) * ImGuiHelpers.GlobalScale);
            DrawEmptyState();
            return;
        }

        if (observedPlanCreatedAt != plan.CreatedAtUtc)
        {
            observedPlanCreatedAt = plan.CreatedAtUtc;
            routeReveal = 0f;
            displayedProgress = plan.Steps.Count == 0 ? 1f : (float)plan.CompletedCount / plan.Steps.Count;
        }

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (1f - EaseOutCubic(routeReveal)) * 10 * ImGuiHelpers.GlobalScale);
        ImGui.Dummy(new Vector2(1, 10) * ImGuiHelpers.GlobalScale);
        DrawPlanOverview(plan);
        ImGui.Dummy(new Vector2(1, 8) * ImGuiHelpers.GlobalScale);
        DrawNextStep(plan, deltaTime);
        ImGui.Dummy(new Vector2(1, 8) * ImGuiHelpers.GlobalScale);
        DrawTrackingOptions(plan, deltaTime);
        ImGui.Dummy(new Vector2(1, 8) * ImGuiHelpers.GlobalScale);
        DrawExportPanel(deltaTime);
        ImGui.Dummy(new Vector2(1, 8) * ImGuiHelpers.GlobalScale);
        DrawStepTable(plan);
    }

    private void DrawColorSelection()
    {
        if (!ImGui.BeginTable("##colorSelectors", 3, ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("Current", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Arrow", ImGuiTableColumnFlags.WidthFixed, 64 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Target", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        var currentIndex = plugin.Configuration.CurrentColorIndex;
        if (DrawColorSelectorCard(
                "CURRENT PLUMAGE",
                "##currentColor",
                ref currentIndex,
                ref currentColorSearch,
                AccentBlue))
        {
            plugin.Configuration.CurrentColorIndex = currentIndex;
            plugin.Configuration.Save();
        }

        ImGui.TableNextColumn();
        var pulse = 0.6f + 0.4f * MathF.Sin((float)ImGui.GetTime() * 2f);
        ImGui.Dummy(new Vector2(1, 38) * ImGuiHelpers.GlobalScale);
        CenteredInColumn("→", new Vector4(AccentGold.X, AccentGold.Y, AccentGold.Z, pulse));
        CenteredInColumn("reliable", TextMuted);

        ImGui.TableNextColumn();
        var targetIndex = plugin.Configuration.TargetColorIndex;
        if (DrawColorSelectorCard(
                "DESIRED PLUMAGE",
                "##targetColor",
                ref targetIndex,
                ref targetColorSearch,
                AccentViolet))
        {
            plugin.Configuration.TargetColorIndex = targetIndex;
            plugin.Configuration.Save();
        }

        ImGui.EndTable();
    }

    private bool DrawColorSelectorCard(
        string heading,
        string comboId,
        ref int index,
        ref string searchText,
        Vector4 accent)
    {
        var selected = ChocoboData.Colors[index];
        var changed = false;
        BeginGlassPanel($"{comboId}Card", new Vector2(0, 112 * ImGuiHelpers.GlobalScale), Glass, accent, accent);
        ImGui.TextColored(accent, heading);
        ImGui.Dummy(new Vector2(1, 2) * ImGuiHelpers.GlobalScale);
        DrawModernSwatch(selected.Rgb, 44);
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextColored(TextPrimary, selected.Name);
        ImGui.SameLine();
        ImGui.TextColored(TextMuted, $"RGB  {selected.Rgb.R}  /  {selected.Rgb.G}  /  {selected.Rgb.B}");
        ImGui.SetNextItemWidth(-1);
        if (DrawColorCombo(comboId, ref index, ref searchText))
            changed = true;
        ImGui.EndGroup();
        EndGlassPanel();
        return changed;
    }

    private void DrawCalculateActions(float deltaTime)
    {
        if (!ImGui.BeginTable("##calculateActions", 2, ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("Calculate", ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn("Swap", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        var calculateWidth = ImGui.GetContentRegionAvail().X;
        if (GradientButton(
                "CALCULATE RELIABLE ROUTE##calculate",
                new Vector2(calculateWidth, 40 * ImGuiHelpers.GlobalScale),
                AccentViolet,
                AccentBlue,
                deltaTime))
        {
            plugin.CreatePlan();
        }

        ImGui.TableNextColumn();
        var swapWidth = ImGui.GetContentRegionAvail().X;
        if (GlassButton("SWAP COLORS##swap", new Vector2(swapWidth, 40 * ImGuiHelpers.GlobalScale), deltaTime))
        {
            (plugin.Configuration.CurrentColorIndex, plugin.Configuration.TargetColorIndex) =
                (plugin.Configuration.TargetColorIndex, plugin.Configuration.CurrentColorIndex);
            plugin.Configuration.Save();
        }
        ImGui.EndTable();
    }

    private static void DrawEmptyState()
    {
        BeginGlassPanel("##emptyState", new Vector2(0, 112 * ImGuiHelpers.GlobalScale), Glass, AccentBlue, AccentViolet);
        ImGui.Dummy(new Vector2(1, 8) * ImGuiHelpers.GlobalScale);
        CenteredText("READY WHEN YOU ARE", AccentBlue);
        CenteredText("Choose two colors and calculate a verified feeding route.", TextPrimary);
        CenteredText("Your next fruit, live progress, and complete ordered list will appear here.", TextMuted);
        EndGlassPanel();
    }

    private void DrawPlanOverview(ActivePlanState plan)
    {
        DrawSectionHeading("ROUTE OVERVIEW", $"{plan.StartName}  →  {plan.TargetName}");
        if (ImGui.BeginTable("##overviewCards", 3, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            DrawMetricCard("TOTAL FEEDS", plan.Steps.Count.ToString(), "ordered fruit steps", AccentGold);
            ImGui.TableNextColumn();
            DrawMetricCard("PREDICTED COLOR", plan.PredictedColorName, $"RGB {plan.EndR}/{plan.EndG}/{plan.EndB}", AccentBlue);
            ImGui.TableNextColumn();
            DrawMetricCard("RELIABILITY MARGIN", plan.ClassificationMargin.ToString("F1"), "distance from nearest rival", Success);
            ImGui.EndTable();
        }

        ImGui.Dummy(new Vector2(1, 3) * ImGuiHelpers.GlobalScale);
        BeginGlassPanel("##fruitTotals", new Vector2(0, 52 * ImGuiHelpers.GlobalScale), Glass, AccentGold, AccentCoral);
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(TextMuted, "SHOPPING LIST");
        foreach (var group in plan.Steps.GroupBy(step => (FruitKind)step.FruitKind))
        {
            ImGui.SameLine(0, 26 * ImGuiHelpers.GlobalScale);
            DrawFruitTotalChip(group.Key, group.Count());
        }
        EndGlassPanel();

        if (!string.IsNullOrWhiteSpace(plan.Warning))
        {
            ImGui.Dummy(new Vector2(1, 4) * ImGuiHelpers.GlobalScale);
            DrawNotice("ACCURACY NOTE", plan.Warning, AccentGold, GoldGlass);
        }
    }

    private static void DrawMetricCard(string label, string value, string caption, Vector4 accent)
    {
        BeginGlassPanel($"##metric{label}", new Vector2(0, 68 * ImGuiHelpers.GlobalScale), Glass, accent, accent);
        ImGui.TextColored(accent, label);
        ImGui.TextColored(TextPrimary, value);
        ImGui.SameLine();
        ImGui.TextColored(TextMuted, caption);
        EndGlassPanel();
    }

    private void DrawFruitTotalChip(FruitKind fruit, int count)
    {
        ImGui.PushID($"total{fruit}");
        DrawFruitIcon(fruit, 26);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(TextPrimary, $"{plugin.LocalizedFruitName(fruit)}  ×{count}");
        ImGui.PopID();
    }

    private void DrawNextStep(ActivePlanState plan, float deltaTime)
    {
        var next = plan.NextStepIndex;
        var completed = plan.CompletedCount;
        var targetProgress = plan.Steps.Count == 0 ? 1f : (float)completed / plan.Steps.Count;
        displayedProgress = SmoothTowards(displayedProgress, targetProgress, 7f, deltaTime);
        var accent = next < 0 ? Success : AccentGold;
        var panel = next < 0 ? GreenGlass : GoldGlass;

        BeginGlassPanel("##nextStep", new Vector2(0, 122 * ImGuiHelpers.GlobalScale), panel, accent, AccentCoral);
        if (next < 0)
        {
            ImGui.TextColored(Success, "ROUTE COMPLETE");
            ImGui.TextColored(TextPrimary, "All fruit has been accounted for.");
            ImGui.TextWrapped("Leave your chocobo stabled for six Earth hours. Removing it early cancels the pending color change.");
        }
        else
        {
            var fruit = (FruitKind)plan.Steps[next].FruitKind;
            DrawFruitIcon(fruit, 58);
            ImGui.SameLine();
            ImGui.BeginGroup();
            ImGui.TextColored(AccentGold, $"NEXT FEED  ·  STEP {next + 1} OF {plan.Steps.Count}");
            ImGui.TextColored(TextPrimary, plugin.LocalizedFruitName(fruit));
            ImGui.TextColored(TextMuted, "Feed exactly one. Manual or automatic detection will advance the route.");
            ImGui.EndGroup();
        }

        ImGui.Dummy(new Vector2(1, 5) * ImGuiHelpers.GlobalScale);
        DrawAnimatedProgress(displayedProgress, accent, AccentBlue, $"{completed} / {plan.Steps.Count}");
        EndGlassPanel();
    }

    private void DrawTrackingOptions(ActivePlanState plan, float deltaTime)
    {
        BeginGlassPanel("##trackingPanel", new Vector2(0, 90 * ImGuiHelpers.GlobalScale), Glass, AccentBlue, AccentViolet);
        ImGui.TextColored(TextMuted, "LIVE TRACKING");
        if (ImGui.BeginTable("##trackingControls", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Options", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 430 * ImGuiHelpers.GlobalScale);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var auto = plugin.Configuration.AutoTrackingEnabled;
            if (ImGui.Checkbox("Automatic detection", ref auto))
            {
                plugin.Configuration.AutoTrackingEnabled = auto;
                plugin.Configuration.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Advances when FFXIV reports that the expected fruit was consumed.");

            ImGui.SameLine();
            var print = plugin.Configuration.PrintNextStepToChat;
            if (ImGui.Checkbox("Chat reminders", ref print))
            {
                plugin.Configuration.PrintNextStepToChat = print;
                plugin.Configuration.Save();
            }

            ImGui.TableNextColumn();
            var next = plan.NextStepIndex;
            ImGui.BeginDisabled(next < 0);
            if (CompactButton("CONFIRM NEXT##confirm", new Vector2(128, 29) * ImGuiHelpers.GlobalScale, Success, deltaTime))
                plugin.MarkNextManually();
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.BeginDisabled(plan.CompletedCount == 0);
            if (CompactButton("UNDO##undo", new Vector2(68, 29) * ImGuiHelpers.GlobalScale, AccentBlue, deltaTime))
                plugin.UndoLastStep();
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (CompactButton("RESET##reset", new Vector2(68, 29) * ImGuiHelpers.GlobalScale, AccentGold, deltaTime))
                plugin.ResetProgress();
            ImGui.SameLine();
            if (CompactButton("CLEAR##clear", new Vector2(68, 29) * ImGuiHelpers.GlobalScale, Danger, deltaTime))
                plugin.ClearPlan();
            ImGui.EndTable();
        }
        EndGlassPanel();

        if (!string.IsNullOrWhiteSpace(plugin.Configuration.LastDetectionNotice))
        {
            var isError = plugin.Configuration.LastDetectionNotice.StartsWith("Detected ", StringComparison.Ordinal);
            ImGui.Dummy(new Vector2(1, 4) * ImGuiHelpers.GlobalScale);
            DrawNotice(
                isError ? "DETECTION MISMATCH" : "AUTOMATIC DETECTION",
                plugin.Configuration.LastDetectionNotice,
                isError ? Danger : Success,
                isError ? GoldGlass : GreenGlass);
        }
    }

    private void DrawStepTable(ActivePlanState plan)
    {
        BeginGlassPanel("##routeListPanel", new Vector2(0, 420 * ImGuiHelpers.GlobalScale), Glass, AccentViolet, AccentBlue);
        if (ImGui.BeginTable("##routeListHeader", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Title", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Filter", ImGuiTableColumnFlags.WidthFixed, 150 * ImGuiHelpers.GlobalScale);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextColored(TextPrimary, "ORDERED FEEDING ROUTE");
            ImGui.TextColored(TextMuted, "One scrollable list · current step highlighted · progress saved automatically");
            ImGui.TableNextColumn();
            var hide = plugin.Configuration.HideCompletedSteps;
            if (ImGui.Checkbox("Hide completed", ref hide))
            {
                plugin.Configuration.HideCompletedSteps = hide;
                plugin.Configuration.Save();
            }
            ImGui.EndTable();
        }
        ImGui.Dummy(new Vector2(1, 5) * ImGuiHelpers.GlobalScale);

        if (ImGui.BeginTable("##routeSteps", 7,
                ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY |
                ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX,
                new Vector2(0, -1)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("STEP", ImGuiTableColumnFlags.WidthFixed, 58 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 42 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("FRUIT", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("STATUS", ImGuiTableColumnFlags.WidthFixed, 108 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("MANUAL", ImGuiTableColumnFlags.WidthFixed, 70 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("AUTO", ImGuiTableColumnFlags.WidthFixed, 58 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("RGB AFTER", ImGuiTableColumnFlags.WidthFixed, 106 * ImGuiHelpers.GlobalScale);
            ImGui.TableHeadersRow();

            var next = plan.NextStepIndex;
            var rgb = new RgbColor(plan.StartR, plan.StartG, plan.StartB);
            var visible = 0;
            for (var i = 0; i < plan.Steps.Count; i++)
            {
                var step = plan.Steps[i];
                var fruit = (FruitKind)step.FruitKind;
                rgb = ChocoboData.Fruit(fruit).Apply(rgb);
                if (plugin.Configuration.HideCompletedSteps && step.IsComplete)
                    continue;

                visible++;
                DrawRouteRow(step, fruit, i, next, rgb);
            }

            if (visible == 0)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, 54 * ImGuiHelpers.GlobalScale);
                ImGui.TableNextColumn();
                ImGui.TableSetColumnIndex(2);
                ImGui.TextColored(Success, "All feeding steps are complete.");
            }
            ImGui.EndTable();
        }
        EndGlassPanel();
    }

    private void DrawExportPanel(float deltaTime)
    {
        BeginGlassPanel("##exportPanel", new Vector2(0, 102 * ImGuiHelpers.GlobalScale), Glass, AccentCoral, AccentViolet);
        ImGui.TextColored(AccentCoral, "EXPORT ROUTE");
        ImGui.SameLine();
        ImGui.TextColored(TextMuted, "Create a complete visual guide with every step, RGB result, shopping list, and instructions.");
        ImGui.Dummy(new Vector2(1, 3) * ImGuiHelpers.GlobalScale);

        if (ImGui.BeginTable("##exportActions", 4, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (CompactButton("PDF DOCUMENT##exportPdf", new Vector2(ImGui.GetContentRegionAvail().X, 32 * ImGuiHelpers.GlobalScale), AccentCoral, deltaTime))
                TryExport(RouteExportFormat.Pdf);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A polished, paginated document designed for printing or sharing.");

            ImGui.TableNextColumn();
            if (CompactButton("TEXT CHECKLIST##exportText", new Vector2(ImGui.GetContentRegionAvail().X, 32 * ImGuiHelpers.GlobalScale), AccentBlue, deltaTime))
                TryExport(RouteExportFormat.Text);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A lightweight plain-text route that opens anywhere.");

            ImGui.TableNextColumn();
            if (CompactButton("HTML GUIDE##exportHtml", new Vector2(ImGui.GetContentRegionAvail().X, 32 * ImGuiHelpers.GlobalScale), AccentViolet, deltaTime))
                TryExport(RouteExportFormat.Html);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A responsive visual guide for any web browser.");

            ImGui.TableNextColumn();
            if (CompactButton("OPEN EXPORT FOLDER##openExports", new Vector2(ImGui.GetContentRegionAvail().X, 32 * ImGuiHelpers.GlobalScale), AccentGold, deltaTime))
                TryOpenExportFolder();
            ImGui.EndTable();
        }
        EndGlassPanel();

        if (!string.IsNullOrWhiteSpace(exportNotice))
        {
            ImGui.Dummy(new Vector2(1, 4) * ImGuiHelpers.GlobalScale);
            DrawNotice(
                exportNoticeIsError ? "EXPORT FAILED" : "EXPORT READY",
                exportNotice,
                exportNoticeIsError ? Danger : Success,
                exportNoticeIsError ? GoldGlass : GreenGlass);
        }
    }

    private void TryExport(RouteExportFormat format)
    {
        try
        {
            var path = plugin.ExportActiveRoute(format);
            exportNotice = $"{format.ToString().ToUpperInvariant()} saved as {Path.GetFileName(path)} in {plugin.RouteExportDirectory}";
            exportNoticeIsError = false;
        }
        catch (Exception exception)
        {
            Plugin.Log.Error(exception, "Failed to export the active chocobo route.");
            exportNotice = $"The route could not be exported: {exception.Message}";
            exportNoticeIsError = true;
        }
    }

    private void TryOpenExportFolder()
    {
        try
        {
            plugin.OpenRouteExportDirectory();
            exportNotice = $"Export folder opened: {plugin.RouteExportDirectory}";
            exportNoticeIsError = false;
        }
        catch (Exception exception)
        {
            Plugin.Log.Error(exception, "Failed to open the chocobo route export folder.");
            exportNotice = $"The export folder could not be opened: {exception.Message}";
            exportNoticeIsError = true;
        }
    }

    private void DrawRouteRow(TrackedStepState step, FruitKind fruit, int index, int next, RgbColor rgb)
    {
        var status = "QUEUED";
        var statusColor = TextMuted;
        if (step.AutoCompleted)
        {
            status = step.ManualCompleted ? "AUTO + MANUAL" : "AUTO-DETECTED";
            statusColor = AccentBlue;
        }
        else if (step.ManualCompleted)
        {
            status = "MANUAL";
            statusColor = Success;
        }
        else if (index == next)
        {
            status = "NEXT";
            statusColor = AccentGold;
        }

        ImGui.TableNextRow(ImGuiTableRowFlags.None, 42 * ImGuiHelpers.GlobalScale);
        if (index == next)
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.26f, 0.17f, 0.055f, 0.78f)));
        else if (step.AutoCompleted)
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.04f, 0.13f, 0.23f, 0.66f)));
        else if (step.ManualCompleted)
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.035f, 0.16f, 0.105f, 0.62f)));

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(statusColor, $"{index + 1:00}");

        ImGui.TableNextColumn();
        DrawFruitIcon(fruit, 30);

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(TextPrimary, plugin.LocalizedFruitName(fruit));

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(statusColor, status);

        ImGui.TableNextColumn();
        var manual = step.ManualCompleted;
        var canEdit = index == next || manual || step.AutoCompleted;
        ImGui.BeginDisabled(!canEdit);
        if (ImGui.Checkbox($"##manual{index}", ref manual))
            plugin.SetManualStep(index, manual);
        ImGui.EndDisabled();

        ImGui.TableNextColumn();
        var automatic = step.AutoCompleted;
        ImGui.BeginDisabled();
        ImGui.Checkbox($"##auto{index}", ref automatic);
        ImGui.EndDisabled();

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(TextMuted, $"{rgb.R}/{rgb.G}/{rgb.B}");
    }

    private bool DrawColorCombo(string id, ref int index, ref string searchText)
    {
        var changed = false;
        var selected = ChocoboData.Colors[index];
        if (!ImGui.BeginCombo(id, $"{selected.Name}##{selected.Name}"))
            return false;

        var popupAppearing = ImGui.IsWindowAppearing();
        if (popupAppearing)
        {
            searchText = string.Empty;
            ImGui.SetKeyboardFocusHere();
        }

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint($"##colorSearch{id}", "Search color names...", ref searchText, 64);
        ImGui.Separator();

        var matches = 0;
        for (var i = 0; i < ChocoboData.Colors.Count; i++)
        {
            var color = ChocoboData.Colors[i];
            if (!string.IsNullOrWhiteSpace(searchText) &&
                !color.Name.Contains(searchText.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            matches++;
            ImGui.PushID(i);
            DrawSmallSwatch(color.Rgb, "combo");
            ImGui.SameLine();
            if (ImGui.Selectable($"{color.Name}   ·   {color.Rgb.R}/{color.Rgb.G}/{color.Rgb.B}", i == index))
            {
                index = i;
                changed = true;
            }
            if (i == index && string.IsNullOrWhiteSpace(searchText))
                ImGui.SetItemDefaultFocus();
            ImGui.PopID();
        }

        if (matches == 0)
            ImGui.TextColored(TextMuted, "No matching plumage colors.");

        ImGui.EndCombo();
        return changed;
    }

    private void DrawFruitIcon(FruitKind fruit, float logicalSize)
    {
        var size = new Vector2(logicalSize) * ImGuiHelpers.GlobalScale;
        var iconId = plugin.FruitIconId(fruit);
        if (iconId == 0)
        {
            ImGui.Dummy(size);
            return;
        }

        var texture = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
        if (texture is null)
        {
            ImGui.Dummy(size);
            return;
        }

        var min = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddCircleFilled(
            min + size / 2 + new Vector2(0, 2 * ImGuiHelpers.GlobalScale),
            size.X * 0.56f,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.28f)));
        ImGui.Image(texture.Handle, size);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(plugin.LocalizedFruitName(fruit));
    }

    private static void DrawModernSwatch(RgbColor rgb, float logicalSize)
    {
        var size = new Vector2(logicalSize) * ImGuiHelpers.GlobalScale;
        var min = ImGui.GetCursorScreenPos();
        var max = min + size;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(
            min + new Vector2(0, 4 * ImGuiHelpers.GlobalScale),
            max + new Vector2(0, 4 * ImGuiHelpers.GlobalScale),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.34f)),
            13 * ImGuiHelpers.GlobalScale);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(ToVector(rgb)), 13 * ImGuiHelpers.GlobalScale);
        drawList.AddLine(
            min + new Vector2(7, 7) * ImGuiHelpers.GlobalScale,
            new Vector2(max.X - 7 * ImGuiHelpers.GlobalScale, min.Y + 7 * ImGuiHelpers.GlobalScale),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.34f)),
            2 * ImGuiHelpers.GlobalScale);
        ImGui.Dummy(size);
    }

    private static void DrawSmallSwatch(RgbColor rgb, string id)
    {
        ImGui.ColorButton(id, ToVector(rgb),
            ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoDragDrop | ImGuiColorEditFlags.NoPicker,
            new Vector2(18, 18) * ImGuiHelpers.GlobalScale);
    }

    private static Vector4 ToVector(RgbColor rgb) =>
        new(rgb.R / 255f, rgb.G / 255f, rgb.B / 255f, 1f);

    private static void DrawSectionHeading(string title, string subtitle)
    {
        ImGui.TextColored(TextPrimary, title);
        ImGui.SameLine();
        ImGui.TextColored(TextMuted, subtitle);
        var min = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.GetWindowDrawList().AddRectFilledMultiColor(
            min,
            min + new Vector2(width, 2 * ImGuiHelpers.GlobalScale),
            ImGui.GetColorU32(AccentViolet),
            ImGui.GetColorU32(AccentBlue),
            ImGui.GetColorU32(AccentBlue),
            ImGui.GetColorU32(AccentViolet));
        ImGui.Dummy(new Vector2(1, 8) * ImGuiHelpers.GlobalScale);
    }

    private static void DrawNotice(string title, string body, Vector4 accent, Vector4 background)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var contentWidth = Math.Max(1, ImGui.GetContentRegionAvail().X - 24 * scale);
        var bodyHeight = ImGui.CalcTextSize(body, false, contentWidth).Y;
        var height = 26 * scale + ImGui.GetTextLineHeight() + bodyHeight;
        BeginGlassPanel($"##notice{title}", new Vector2(0, height), background, accent, accent);
        ImGui.TextColored(accent, title);
        ImGui.TextWrapped(body);
        EndGlassPanel();
    }

    private static void DrawAnimatedProgress(float fraction, Vector4 left, Vector4 right, string label)
    {
        fraction = Math.Clamp(fraction, 0f, 1f);
        var height = 12 * ImGuiHelpers.GlobalScale;
        var min = ImGui.GetCursorScreenPos();
        var labelWidth = ImGui.CalcTextSize(label).X;
        var width = Math.Max(height, ImGui.GetContentRegionAvail().X - labelWidth - 14 * ImGuiHelpers.GlobalScale);
        var max = min + new Vector2(width, height);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.015f, 0.02f, 0.035f, 0.88f)), height / 2);
        if (fraction > 0.001f)
        {
            var fillMax = min + new Vector2(Math.Max(height, width * fraction), height);
            drawList.AddRectFilledMultiColor(
                min,
                fillMax,
                ImGui.GetColorU32(left),
                ImGui.GetColorU32(right),
                ImGui.GetColorU32(right),
                ImGui.GetColorU32(left));
            var shimmer = 0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * 2.5f);
            var shimmerX = min.X + (fillMax.X - min.X) * shimmer;
            drawList.AddLine(
                new Vector2(shimmerX, min.Y + 1),
                new Vector2(shimmerX, max.Y - 1),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.45f)),
                2 * ImGuiHelpers.GlobalScale);
        }
        ImGui.Dummy(new Vector2(width, height));
        ImGui.SameLine();
        ImGui.TextColored(TextMuted, label);
    }

    private bool GradientButton(
        string label,
        Vector2 size,
        Vector4 left,
        Vector4 right,
        float deltaTime)
    {
        var min = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton(label, size);
        var clicked = ImGui.IsItemClicked();
        var hover = AnimateHover(label, ImGui.IsItemHovered(), deltaTime);
        var activeOffset = ImGui.IsItemActive() ? 2 * ImGuiHelpers.GlobalScale : 0f;
        min.Y += activeOffset;
        var max = min + size;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(
            min + new Vector2(0, 5 * ImGuiHelpers.GlobalScale),
            max + new Vector2(0, 5 * ImGuiHelpers.GlobalScale),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.32f)),
            10 * ImGuiHelpers.GlobalScale);
        drawList.AddRectFilledMultiColor(
            min,
            max,
            ImGui.GetColorU32(Lerp(left, new Vector4(0.78f, 0.55f, 1f, 1f), hover)),
            ImGui.GetColorU32(Lerp(right, new Vector4(0.42f, 0.84f, 1f, 1f), hover)),
            ImGui.GetColorU32(Lerp(right, new Vector4(0.35f, 0.70f, 0.96f, 1f), hover)),
            ImGui.GetColorU32(Lerp(left, new Vector4(0.52f, 0.30f, 0.86f, 1f), hover)));
        DrawButtonText(drawList, label, min, max, TextPrimary);
        return clicked;
    }

    private bool GlassButton(string label, Vector2 size, float deltaTime) =>
        CompactButton(label, size, AccentBlue, deltaTime);

    private bool NavigationButton(string label, bool selected, float deltaTime)
    {
        var accent = selected ? AccentGold : AccentBlue;
        return CompactButton(label, new Vector2(104, 34) * ImGuiHelpers.GlobalScale, accent, deltaTime, selected);
    }

    private bool CompactButton(
        string label,
        Vector2 size,
        Vector4 accent,
        float deltaTime,
        bool selected = false)
    {
        var min = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton(label, size);
        var clicked = ImGui.IsItemClicked();
        var hover = AnimateHover(label, ImGui.IsItemHovered(), deltaTime);
        var max = min + size;
        var baseColor = selected
            ? new Vector4(accent.X * 0.36f, accent.Y * 0.36f, accent.Z * 0.36f, 0.92f)
            : new Vector4(0.10f, 0.12f, 0.18f, 0.88f);
        var color = Lerp(baseColor, new Vector4(accent.X * 0.42f, accent.Y * 0.42f, accent.Z * 0.42f, 0.96f), hover);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(color), 9 * ImGuiHelpers.GlobalScale);
        drawList.AddRectFilled(
            new Vector2(min.X + 8 * ImGuiHelpers.GlobalScale, max.Y - 2 * ImGuiHelpers.GlobalScale),
            new Vector2(max.X - 8 * ImGuiHelpers.GlobalScale, max.Y),
            ImGui.GetColorU32(new Vector4(accent.X, accent.Y, accent.Z, selected ? 0.95f : 0.45f + hover * 0.45f)),
            ImGuiHelpers.GlobalScale);
        DrawButtonText(drawList, label, min, max, selected ? accent : TextPrimary);
        return clicked;
    }

    private float AnimateHover(string id, bool hovered, float deltaTime)
    {
        var current = hoverAnimations.GetValueOrDefault(id);
        current = SmoothTowards(current, hovered ? 1f : 0f, 14f, deltaTime);
        hoverAnimations[id] = current;
        return current;
    }

    private static void DrawButtonText(
        ImDrawListPtr drawList,
        string label,
        Vector2 min,
        Vector2 max,
        Vector4 color)
    {
        var visibleLabel = label.Split("##", StringSplitOptions.None)[0];
        var textSize = ImGui.CalcTextSize(visibleLabel);
        var textPosition = min + (max - min - textSize) / 2;
        drawList.AddText(textPosition, ImGui.GetColorU32(color), visibleLabel);
    }

    private static Vector4 Lerp(Vector4 from, Vector4 to, float amount) => from + (to - from) * amount;

    private static void BeginGlassPanel(
        string id,
        Vector2 size,
        Vector4 background,
        Vector4 gradientLeft,
        Vector4 gradientRight)
    {
        var min = ImGui.GetCursorScreenPos();
        if (size.X <= 0)
            size.X = ImGui.GetContentRegionAvail().X;
        var max = min + size;
        var rounding = 14 * ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(
            min + new Vector2(0, 6 * ImGuiHelpers.GlobalScale),
            max + new Vector2(0, 6 * ImGuiHelpers.GlobalScale),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.28f)),
            rounding);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(background), rounding);
        drawList.AddRectFilledMultiColor(
            min,
            new Vector2(max.X, min.Y + 2 * ImGuiHelpers.GlobalScale),
            ImGui.GetColorU32(gradientLeft),
            ImGui.GetColorU32(gradientRight),
            ImGui.GetColorU32(gradientRight),
            ImGui.GetColorU32(gradientLeft));
        drawList.AddLine(
            min + new Vector2(12, 1) * ImGuiHelpers.GlobalScale,
            new Vector2(max.X - 12 * ImGuiHelpers.GlobalScale, min.Y + ImGuiHelpers.GlobalScale),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f)),
            ImGuiHelpers.GlobalScale);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 8) * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0f));
        ImGui.BeginChild(
            id,
            size,
            false,
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4 * ImGuiHelpers.GlobalScale);
    }

    private static void EndGlassPanel()
    {
        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }

    private static void CenteredText(string text, Vector4 color)
    {
        var width = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), (ImGui.GetWindowWidth() - width) / 2));
        ImGui.TextColored(color, text);
    }

    private static void CenteredInColumn(string text, Vector4 color)
    {
        var width = ImGui.CalcTextSize(text).X;
        var available = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (available - width) / 2));
        ImGui.TextColored(color, text);
    }

    private static void DrawHelp()
    {
        DrawSectionHeading("HOW IT WORKS", "A simple four-step workflow built around the reliable calculation model.");
        DrawHelpCard(
            "01",
            "CHOOSE COLORS",
            "Select the plumage your chocobo currently shows and the color you want. A Han Lemon reset to Desert Yellow provides the most reliable baseline.",
            AccentGold,
            GoldGlass);
        DrawHelpCard(
            "02",
            "FOLLOW THE ORDER",
            "Feed only the listed fruit from top to bottom. The exact algebraic route is arranged to reach its selected RGB endpoint without unintended clamping.",
            AccentBlue,
            BlueGlass);
        DrawHelpCard(
            "03",
            "TRACK EVERY FEED",
            "Automatic detection fills the Auto column when the expected fruit is consumed. You can always use Manual, Undo, Reset, or Clear without losing control.",
            AccentViolet,
            GlassRaised);
        DrawHelpCard(
            "04",
            "WAIT SIX HOURS",
            "After every step is complete, leave the chocobo stabled for six Earth hours. Removing it early cancels the pending plumage change.",
            Success,
            GreenGlass);

        DrawNotice(
            "WHY RELIABLE TARGET?",
            "Every one of the 7,225 named start/target combinations uses the same reliable policy. It stays close to the published swatch when safely possible, falls back to a deeper point near tight neighbors, and verifies that the ordered route reaches the selected endpoint exactly.",
            AccentGold,
            Glass);
        ImGui.Dummy(new Vector2(1, 6) * ImGuiHelpers.GlobalScale);
        DrawNotice(
            "FEATHER MESSAGE",
            "A feather-growth message means the pending color crossed a named-color boundary. Its absence does not mean the fruit failed, so never add extra fruit unless it appears in the calculated list.",
            AccentBlue,
            BlueGlass);
    }

    private static void DrawHelpCard(
        string number,
        string title,
        string body,
        Vector4 accent,
        Vector4 background)
    {
        BeginGlassPanel($"##help{number}", new Vector2(0, 92 * ImGuiHelpers.GlobalScale), background, accent, accent);
        if (ImGui.BeginTable($"##helpLayout{number}", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Number", ImGuiTableColumnFlags.WidthFixed, 58 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Copy", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextColored(accent, number);
            ImGui.TableNextColumn();
            ImGui.TextColored(TextPrimary, title);
            ImGui.TextWrapped(body);
            ImGui.EndTable();
        }
        EndGlassPanel();
        ImGui.Dummy(new Vector2(1, 7) * ImGuiHelpers.GlobalScale);
    }
}
