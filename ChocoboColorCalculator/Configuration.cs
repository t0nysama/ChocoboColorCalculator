using Dalamud.Configuration;

namespace ChocoboColorCalculator;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public int CurrentColorIndex { get; set; }
    public int TargetColorIndex { get; set; } = 6;
    public bool AutoTrackingEnabled { get; set; } = true;
    public bool PrintNextStepToChat { get; set; } = true;
    public bool HideCompletedSteps { get; set; }
    public ActivePlanState? ActivePlan { get; set; }
    public string? LastDetectionNotice { get; set; }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}

[Serializable]
public sealed class ActivePlanState
{
    public string StartName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public int StartR { get; set; }
    public int StartG { get; set; }
    public int StartB { get; set; }
    public int AimR { get; set; }
    public int AimG { get; set; }
    public int AimB { get; set; }
    public int EndR { get; set; }
    public int EndG { get; set; }
    public int EndB { get; set; }
    public string PredictedColorName { get; set; } = string.Empty;
    public double ClassificationMargin { get; set; }
    public bool UsedFallback { get; set; }
    public string? Warning { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<TrackedStepState> Steps { get; set; } = [];

    public int NextStepIndex => Steps.FindIndex(step => !step.IsComplete);
    public int CompletedCount => Steps.Count(step => step.IsComplete);
}

[Serializable]
public sealed class TrackedStepState
{
    public int FruitKind { get; set; }
    public bool ManualCompleted { get; set; }
    public bool AutoCompleted { get; set; }
    public bool IsComplete => ManualCompleted || AutoCompleted;
}
