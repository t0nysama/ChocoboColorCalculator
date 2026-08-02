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
    private static readonly Vector4 Gold = new(0.94f, 0.72f, 0.22f, 1f);
    private static readonly Vector4 Green = new(0.35f, 0.86f, 0.52f, 1f);
    private static readonly Vector4 Blue = new(0.30f, 0.70f, 1.00f, 1f);
    private static readonly Vector4 Red = new(0.95f, 0.42f, 0.39f, 1f);
    private static readonly Vector4 Muted = new(0.62f, 0.65f, 0.70f, 1f);
    private static readonly Vector4 Panel = new(0.075f, 0.085f, 0.105f, 0.94f);
    private static readonly Vector4 PanelRaised = new(0.105f, 0.12f, 0.145f, 0.96f);
    private static readonly Vector4 GoldPanel = new(0.18f, 0.145f, 0.065f, 0.94f);
    private static readonly Vector4 GreenPanel = new(0.06f, 0.16f, 0.105f, 0.94f);
    private static readonly Vector4 BluePanel = new(0.055f, 0.115f, 0.19f, 0.94f);

    private readonly Plugin plugin;

    public MainWindow(Plugin plugin, RouteCalculator calculator)
        : base("Chocobo Color Calculator##Main")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(780, 720),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14, 12) * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8f * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(9, 7) * ImGuiHelpers.GlobalScale);

        DrawHeader();
        ImGui.Spacing();

        if (ImGui.BeginTabBar("##mainTabs", ImGuiTabBarFlags.None))
        {
            if (ImGui.BeginTabItem("Calculator & route"))
            {
                DrawCalculator();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("How it works"))
            {
                DrawHelp();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        ImGui.PopStyleVar(4);
    }

    private static void DrawHeader()
    {
        ImGui.TextColored(Gold, "CHOCOBO COLOR CALCULATOR");
        ImGui.SameLine();
        ImGui.TextDisabled("  Plan, feed, and track without leaving the game");
        ImGui.Separator();
    }

    private void DrawCalculator()
    {
        DrawColorSelection();
        ImGui.Spacing();
        DrawCalculationMode();
        ImGui.Spacing();
        DrawCalculateActions();
        ImGui.Spacing();

        var plan = plugin.Configuration.ActivePlan;
        if (plan is null)
        {
            DrawEmptyState();
            return;
        }

        DrawPlanOverview(plan);
        ImGui.Spacing();
        DrawFeedViewToolbar(plan);
        ImGui.Spacing();
        if (plugin.Configuration.UseFeedListView)
            DrawFeedStepList(plan);
        else
            DrawNextStep(plan);
        ImGui.Spacing();
        DrawTrackingOptions(plan);
        if (!plugin.Configuration.UseFeedListView)
        {
            ImGui.Spacing();
            DrawStepTable(plan);
        }
    }

    private void DrawColorSelection()
    {
        if (!ImGui.BeginTable("##colorSelectors", 3, ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("Current", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Arrow", ImGuiTableColumnFlags.WidthFixed, 52 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Target", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        var currentIndex = plugin.Configuration.CurrentColorIndex;
        if (DrawColorSelectorCard("Current plumage", "##currentColor", ref currentIndex))
        {
            plugin.Configuration.CurrentColorIndex = currentIndex;
            plugin.Configuration.Save();
        }

        ImGui.TableNextColumn();
        var arrowOffset = 39f * ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(1, arrowOffset));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 12 * ImGuiHelpers.GlobalScale);
        ImGui.TextColored(Gold, ">>");

        ImGui.TableNextColumn();
        var targetIndex = plugin.Configuration.TargetColorIndex;
        if (DrawColorSelectorCard("Desired plumage", "##targetColor", ref targetIndex))
        {
            plugin.Configuration.TargetColorIndex = targetIndex;
            plugin.Configuration.Save();
        }

        ImGui.EndTable();
    }

    private bool DrawColorSelectorCard(string heading, string comboId, ref int index)
    {
        var selected = ChocoboData.Colors[index];
        var changed = false;

        ImGui.PushStyleColor(ImGuiCol.ChildBg, PanelRaised);
        if (ImGui.BeginChild($"{comboId}Card", new Vector2(0, 108 * ImGuiHelpers.GlobalScale), true,
                ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.TextDisabled(heading.ToUpperInvariant());
            DrawLargeSwatch(selected.Rgb, $"{comboId}Large");
            ImGui.SameLine();
            ImGui.BeginGroup();
            ImGui.TextUnformatted(selected.Name);
            ImGui.TextDisabled($"RGB {selected.Rgb.R} / {selected.Rgb.G} / {selected.Rgb.B}");
            ImGui.EndGroup();

            ImGui.SetNextItemWidth(-1);
            if (DrawColorCombo(comboId, ref index))
                changed = true;
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
        return changed;
    }

    private void DrawCalculationMode()
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Panel);
        if (ImGui.BeginChild("##modePanel", new Vector2(0, 74 * ImGuiHelpers.GlobalScale), true,
                ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.TextColored(Gold, "ACCURACY MODE");
            var safe = plugin.Configuration.UseSafeCenter;
            if (ImGui.BeginTable("##modeChoices", 2, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Safe", ImGuiTableColumnFlags.WidthStretch, 2f);
                ImGui.TableSetupColumn("Exact", ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGui.RadioButton("Safe center", safe))
                {
                    plugin.Configuration.UseSafeCenter = true;
                    plugin.Configuration.Save();
                }
                ImGui.SameLine();
                ImGui.TextDisabled("Recommended - more separation from neighboring colors");

                ImGui.TableNextColumn();
                if (ImGui.RadioButton("Published RGB", !safe))
                {
                    plugin.Configuration.UseSafeCenter = false;
                    plugin.Configuration.Save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Aims directly at the published swatch. This can be less forgiving for very close colors.");
                ImGui.EndTable();
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawCalculateActions()
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.70f, 0.48f, 0.10f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.86f, 0.62f, 0.16f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.57f, 0.37f, 0.07f, 1f));
        if (ImGui.Button("CALCULATE ROUTE", new Vector2(-145 * ImGuiHelpers.GlobalScale, 36 * ImGuiHelpers.GlobalScale)))
            plugin.CreatePlan();
        ImGui.PopStyleColor(3);

        ImGui.SameLine();
        if (ImGui.Button("Swap colors", new Vector2(-1, 36 * ImGuiHelpers.GlobalScale)))
        {
            (plugin.Configuration.CurrentColorIndex, plugin.Configuration.TargetColorIndex) =
                (plugin.Configuration.TargetColorIndex, plugin.Configuration.CurrentColorIndex);
            plugin.Configuration.Save();
        }
    }

    private static void DrawEmptyState()
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Panel);
        if (ImGui.BeginChild("##emptyState", new Vector2(0, 115 * ImGuiHelpers.GlobalScale), true,
                ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.Dummy(new Vector2(1, 13 * ImGuiHelpers.GlobalScale));
            CenteredText("Choose your current and desired colors, then calculate a feeding route.", Muted);
            CenteredText("Your ordered fruit list and live progress tracker will appear here.", Muted);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawPlanOverview(ActivePlanState plan)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, PanelRaised);
        if (ImGui.BeginChild("##routeOverview", new Vector2(0, 186 * ImGuiHelpers.GlobalScale), true,
                ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.TextColored(Gold, "YOUR FEEDING PLAN");
            ImGui.SameLine();
            ImGui.TextDisabled($"{plan.StartName}  >  {plan.TargetName}");

            ImGui.TextDisabled(
                $"Aim {plan.AimR}/{plan.AimG}/{plan.AimB}   |   " +
                $"Simulated result {plan.EndR}/{plan.EndG}/{plan.EndB}   |   " +
                $"Safety margin {plan.ClassificationMargin:F1}");

            ImGui.Spacing();
            if (ImGui.BeginTable("##fruitTotals", 3, ImGuiTableFlags.SizingStretchSame))
            {
                var column = 0;
                foreach (var group in plan.Steps.GroupBy(step => (FruitKind)step.FruitKind))
                {
                    if (column % 3 == 0)
                        ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    DrawFruitTotalChip(group.Key, group.Count());
                    column++;
                }
                ImGui.EndTable();
            }

            if (!string.IsNullOrWhiteSpace(plan.Warning))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Gold);
                ImGui.TextWrapped($"Note: {plan.Warning}");
                ImGui.PopStyleColor();
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawFruitTotalChip(FruitKind fruit, int count)
    {
        ImGui.PushID($"total{fruit}");
        DrawFruitIcon(fruit, 28);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted($"{plugin.LocalizedFruitName(fruit)}  x{count}");
        ImGui.PopID();
    }

    private void DrawFeedViewToolbar(ActivePlanState plan)
    {
        if (!ImGui.BeginTable("##feedViewToolbar", 2, ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("Progress", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("View", ImGuiTableColumnFlags.WidthFixed, 202 * ImGuiHelpers.GlobalScale);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Gold, "NEXT FEED");
        ImGui.SameLine();
        ImGui.TextDisabled($"{plan.CompletedCount} of {plan.Steps.Count} steps complete");

        ImGui.TableNextColumn();
        var listView = plugin.Configuration.UseFeedListView;
        if (DrawViewButton("Card view", !listView))
        {
            plugin.Configuration.UseFeedListView = false;
            plugin.Configuration.Save();
        }
        ImGui.SameLine();
        if (DrawViewButton("List view", listView))
        {
            plugin.Configuration.UseFeedListView = true;
            plugin.Configuration.Save();
        }

        ImGui.EndTable();
    }

    private static bool DrawViewButton(string label, bool selected)
    {
        if (selected)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.48f, 0.32f, 0.08f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.62f, 0.43f, 0.11f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.40f, 0.26f, 0.06f, 1f));
        }

        var clicked = ImGui.Button(label, new Vector2(96 * ImGuiHelpers.GlobalScale, 0));
        if (selected)
            ImGui.PopStyleColor(3);
        return clicked;
    }

    private void DrawNextStep(ActivePlanState plan)
    {
        var next = plan.NextStepIndex;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, next < 0 ? GreenPanel : GoldPanel);
        if (ImGui.BeginChild("##nextStep", new Vector2(0, 112 * ImGuiHelpers.GlobalScale), true,
                ImGuiWindowFlags.NoScrollbar))
        {
            if (next < 0)
            {
                ImGui.TextColored(Green, "FEEDING ROUTE COMPLETE");
                ImGui.TextWrapped("Leave your chocobo in the stable until the six-hour timer finishes.");
                ImGui.Spacing();
                ImGui.TextDisabled("Removing the chocobo early cancels the pending color change.");
            }
            else
            {
                var fruit = (FruitKind)plan.Steps[next].FruitKind;
                DrawFruitIcon(fruit, 58);
                ImGui.SameLine();
                ImGui.BeginGroup();
                ImGui.TextColored(Gold, $"NEXT FEED  -  STEP {next + 1} OF {plan.Steps.Count}");
                ImGui.TextUnformatted(plugin.LocalizedFruitName(fruit));
                ImGui.TextDisabled("Feed exactly one, then confirm manually or let auto-detection advance.");
                ImGui.EndGroup();
            }

            var completed = plan.CompletedCount;
            var fraction = plan.Steps.Count == 0 ? 1f : (float)completed / plan.Steps.Count;
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, next < 0 ? Green : Gold);
            ImGui.ProgressBar(fraction, new Vector2(-1, 8 * ImGuiHelpers.GlobalScale), string.Empty);
            ImGui.PopStyleColor();
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawFeedStepList(ActivePlanState plan)
    {
        var hide = plugin.Configuration.HideCompletedSteps;
        if (ImGui.Checkbox("Hide completed", ref hide))
        {
            plugin.Configuration.HideCompletedSteps = hide;
            plugin.Configuration.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("Manual checks and automatic detection update this list immediately.");

        var next = plan.NextStepIndex;
        var fraction = plan.Steps.Count == 0 ? 1f : (float)plan.CompletedCount / plan.Steps.Count;
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, next < 0 ? Green : Gold);
        ImGui.ProgressBar(fraction, new Vector2(-1, 7 * ImGuiHelpers.GlobalScale), string.Empty);
        ImGui.PopStyleColor();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.ChildBg, Panel);
        if (ImGui.BeginChild("##feedStepList", new Vector2(0, 350 * ImGuiHelpers.GlobalScale), true))
        {
            var rgb = new RgbColor(plan.StartR, plan.StartG, plan.StartB);
            var visibleCount = 0;
            for (var i = 0; i < plan.Steps.Count; i++)
            {
                var step = plan.Steps[i];
                var fruit = (FruitKind)step.FruitKind;
                rgb = ChocoboData.Fruit(fruit).Apply(rgb);
                if (hide && step.IsComplete)
                    continue;

                DrawFeedListRow(step, fruit, i, next, rgb);
                visibleCount++;
                if (i < plan.Steps.Count - 1)
                    ImGui.Spacing();
            }

            if (visibleCount == 0)
            {
                ImGui.Dummy(new Vector2(1, 18 * ImGuiHelpers.GlobalScale));
                CenteredText("All feeding steps are complete.", Green);
                CenteredText("Disable Hide completed to review the finished route.", Muted);
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawFeedListRow(
        TrackedStepState step,
        FruitKind fruit,
        int index,
        int next,
        RgbColor predictedRgb)
    {
        var status = "QUEUED";
        var statusColor = Muted;
        var rowBackground = PanelRaised;
        if (step.AutoCompleted && step.ManualCompleted)
        {
            status = "AUTO + MANUAL";
            statusColor = Blue;
            rowBackground = BluePanel;
        }
        else if (step.AutoCompleted)
        {
            status = "AUTO-DETECTED";
            statusColor = Blue;
            rowBackground = BluePanel;
        }
        else if (step.ManualCompleted)
        {
            status = "MANUAL";
            statusColor = Green;
            rowBackground = GreenPanel;
        }
        else if (index == next)
        {
            status = "NEXT";
            statusColor = Gold;
            rowBackground = GoldPanel;
        }

        ImGui.PushID(index);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, rowBackground);
        if (ImGui.BeginChild("##feedListRow", new Vector2(0, 54 * ImGuiHelpers.GlobalScale), true,
                ImGuiWindowFlags.NoScrollbar))
        {
            if (ImGui.BeginTable("##feedListRowColumns", 6, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Step", ImGuiTableColumnFlags.WidthFixed, 52 * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("FruitIcon", ImGuiTableColumnFlags.WidthFixed, 38 * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Fruit", ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 126 * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Manual", ImGuiTableColumnFlags.WidthFixed, 82 * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Auto", ImGuiTableColumnFlags.WidthFixed, 66 * ImGuiHelpers.GlobalScale);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(statusColor, $"#{index + 1}");

                ImGui.TableNextColumn();
                DrawFruitIcon(fruit, 30);

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(plugin.LocalizedFruitName(fruit));
                ImGui.TextDisabled($"Predicted RGB {predictedRgb.R}/{predictedRgb.G}/{predictedRgb.B}");

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(statusColor, status);

                ImGui.TableNextColumn();
                var manual = step.ManualCompleted;
                var canEdit = index == next || manual || step.AutoCompleted;
                ImGui.BeginDisabled(!canEdit);
                if (ImGui.Checkbox("Manual##feedListManual", ref manual))
                    plugin.SetManualStep(index, manual);
                ImGui.EndDisabled();

                ImGui.TableNextColumn();
                var automatic = step.AutoCompleted;
                ImGui.BeginDisabled();
                ImGui.Checkbox("Auto##feedListAuto", ref automatic);
                ImGui.EndDisabled();

                ImGui.EndTable();
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopID();
    }

    private void DrawTrackingOptions(ActivePlanState plan)
    {
        var next = plan.NextStepIndex;
        var completed = plan.CompletedCount;

        if (ImGui.BeginTable("##trackingControls", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Options", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 375 * ImGuiHelpers.GlobalScale);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var auto = plugin.Configuration.AutoTrackingEnabled;
            if (ImGui.Checkbox("Automatic detection", ref auto))
            {
                plugin.Configuration.AutoTrackingEnabled = auto;
                plugin.Configuration.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Checks the Auto column when FFXIV reports that your chocobo ate the expected fruit.");

            ImGui.SameLine();
            var print = plugin.Configuration.PrintNextStepToChat;
            if (ImGui.Checkbox("Chat reminders", ref print))
            {
                plugin.Configuration.PrintNextStepToChat = print;
                plugin.Configuration.Save();
            }

            ImGui.TableNextColumn();
            ImGui.BeginDisabled(next < 0);
            if (ImGui.Button("Confirm next manually"))
                plugin.MarkNextManually();
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.BeginDisabled(completed == 0);
            if (ImGui.Button("Undo"))
                plugin.UndoLastStep();
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button("Reset"))
                plugin.ResetProgress();
            ImGui.SameLine();
            if (ImGui.Button("Clear"))
                plugin.ClearPlan();
            ImGui.EndTable();
        }

        if (!string.IsNullOrWhiteSpace(plugin.Configuration.LastDetectionNotice))
        {
            var isError = plugin.Configuration.LastDetectionNotice.StartsWith("Detected ", StringComparison.Ordinal);
            ImGui.PushStyleColor(ImGuiCol.Text, isError ? Red : Green);
            ImGui.TextWrapped(plugin.Configuration.LastDetectionNotice);
            ImGui.PopStyleColor();
        }
    }

    private void DrawStepTable(ActivePlanState plan)
    {
        ImGui.TextColored(Gold, "ORDERED FEEDING ROUTE");
        ImGui.SameLine();
        var hide = plugin.Configuration.HideCompletedSteps;
        if (ImGui.Checkbox("Hide completed", ref hide))
        {
            plugin.Configuration.HideCompletedSteps = hide;
            plugin.Configuration.Save();
        }

        if (!ImGui.BeginTable("##routeSteps", 7,
                ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY |
                ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX,
                new Vector2(0, -1)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Step", ImGuiTableColumnFlags.WidthFixed, 52 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 38 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Fruit", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 78 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Manual", ImGuiTableColumnFlags.WidthFixed, 66 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Auto", ImGuiTableColumnFlags.WidthFixed, 54 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Predicted RGB", ImGuiTableColumnFlags.WidthFixed, 108 * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();

        var next = plan.NextStepIndex;
        var rgb = new RgbColor(plan.StartR, plan.StartG, plan.StartB);
        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            var fruit = (FruitKind)step.FruitKind;
            rgb = ChocoboData.Fruit(fruit).Apply(rgb);
            if (hide && step.IsComplete)
                continue;

            ImGui.TableNextRow(ImGuiTableRowFlags.None, 34 * ImGuiHelpers.GlobalScale);
            if (i == next)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.30f, 0.21f, 0.06f, 0.72f)));
            else if (step.IsComplete)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.06f, 0.19f, 0.11f, 0.58f)));

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted((i + 1).ToString());

            ImGui.TableNextColumn();
            DrawFruitIcon(fruit, 28);

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(plugin.LocalizedFruitName(fruit));

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            if (step.IsComplete)
                ImGui.TextColored(Green, "Done");
            else if (i == next)
                ImGui.TextColored(Gold, "Next");
            else
                ImGui.TextDisabled("Queued");

            ImGui.TableNextColumn();
            var manual = step.ManualCompleted;
            var canEdit = i == next || manual || step.AutoCompleted;
            ImGui.BeginDisabled(!canEdit);
            if (ImGui.Checkbox($"##manual{i}", ref manual))
                plugin.SetManualStep(i, manual);
            ImGui.EndDisabled();

            ImGui.TableNextColumn();
            var automatic = step.AutoCompleted;
            ImGui.BeginDisabled();
            ImGui.Checkbox($"##auto{i}", ref automatic);
            ImGui.EndDisabled();

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled($"{rgb.R}/{rgb.G}/{rgb.B}");
        }
        ImGui.EndTable();
    }

    private bool DrawColorCombo(string id, ref int index)
    {
        var changed = false;
        var selected = ChocoboData.Colors[index];
        if (!ImGui.BeginCombo(id, $"Select color...##{selected.Name}"))
            return false;

        for (var i = 0; i < ChocoboData.Colors.Count; i++)
        {
            var color = ChocoboData.Colors[i];
            ImGui.PushID(i);
            DrawSmallSwatch(color.Rgb, "combo");
            ImGui.SameLine();
            if (ImGui.Selectable($"{color.Name}   RGB {color.Rgb.R}/{color.Rgb.G}/{color.Rgb.B}", i == index))
            {
                index = i;
                changed = true;
            }
            if (i == index)
                ImGui.SetItemDefaultFocus();
            ImGui.PopID();
        }
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

        ImGui.Image(texture.Handle, size);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(plugin.LocalizedFruitName(fruit));
    }

    private static void DrawLargeSwatch(RgbColor rgb, string id)
    {
        var color = ToVector(rgb);
        ImGui.ColorButton(id, color,
            ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoDragDrop | ImGuiColorEditFlags.NoPicker,
            new Vector2(42, 42) * ImGuiHelpers.GlobalScale);
    }

    private static void DrawSmallSwatch(RgbColor rgb, string id)
    {
        ImGui.ColorButton(id, ToVector(rgb),
            ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoDragDrop | ImGuiColorEditFlags.NoPicker,
            new Vector2(17, 17) * ImGuiHelpers.GlobalScale);
    }

    private static Vector4 ToVector(RgbColor rgb) =>
        new(rgb.R / 255f, rgb.G / 255f, rgb.B / 255f, 1f);

    private static void CenteredText(string text, Vector4 color)
    {
        var width = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), (ImGui.GetWindowWidth() - width) / 2));
        ImGui.TextColored(color, text);
    }

    private static void DrawHelp()
    {
        DrawHelpCard(
            "1. CHOOSE COLORS",
            "Select the plumage your chocobo currently shows and the color you want. If your chocobo " +
            "has been recolored before, its exact hidden RGB is unknown; a Han Lemon reset to Desert " +
            "Yellow gives the most reliable starting point.",
            GoldPanel);

        DrawHelpCard(
            "2. FOLLOW THE ORDER",
            "Every fruit changes all three hidden RGB channels by 5. Channels clamp at 0 and 255, so " +
            "the same fruit totals in a different order can produce a different endpoint. Feed the list " +
            "from top to bottom, one item per step.",
            PanelRaised);

        DrawHelpCard(
            "3. TRACK EACH FEED",
            "The Manual checkbox is always yours to control. The Auto checkbox is filled when the game " +
            "reports that your chocobo ate the expected localized fruit. Automatic tracking never clicks " +
            "the game UI, uses an item, hides chat, or reads process memory. Switch between Card and List " +
            "views at any time; in List view the next step is gold, manual completions are green, and " +
            "auto-detected completions are blue.",
            PanelRaised);

        DrawHelpCard(
            "4. WAIT SIX HOURS",
            "Once every step is complete, leave the chocobo stabled for six Earth hours. Removing it " +
            "from the stable early cancels the pending plumage change.",
            GreenPanel);

        ImGui.Spacing();
        ImGui.TextColored(Gold, "WHY SAFE CENTER?");
        ImGui.TextWrapped(
            "Named colors occupy regions around published RGB swatches. Safe center aims for a reachable " +
            "point with extra distance from neighboring regions, improving reliability for close pairs such " +
            "as Currant Purple and Grape Purple.");
    }

    private static void DrawHelpCard(string title, string body, Vector4 background)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, background);
        if (ImGui.BeginChild($"##{title}", new Vector2(0, 96 * ImGuiHelpers.GlobalScale), true,
                ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.TextColored(Gold, title);
            ImGui.TextWrapped(body);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.Spacing();
    }
}
