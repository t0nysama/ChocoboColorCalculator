using System.IO;
using System.Text.Json;

namespace ChocoboColorCalculator.Desktop;

public sealed class DesktopState
{
    public int CalculationModelVersion { get; set; }
    public int CurrentColorIndex { get; set; }
    public int TargetColorIndex { get; set; } = 6;
    public DesktopRouteState? ActiveRoute { get; set; }
}

public sealed class DesktopRouteState
{
    public string StartName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public int StartR { get; set; }
    public int StartG { get; set; }
    public int StartB { get; set; }
    public int TargetR { get; set; }
    public int TargetG { get; set; }
    public int TargetB { get; set; }
    public int AimR { get; set; }
    public int AimG { get; set; }
    public int AimB { get; set; }
    public int EndR { get; set; }
    public int EndG { get; set; }
    public int EndB { get; set; }
    public string PredictedColorName { get; set; } = string.Empty;
    public double ClassificationMargin { get; set; }
    public string? Warning { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<DesktopStepState> Steps { get; set; } = [];
}

public sealed class DesktopStepState
{
    public int FruitKind { get; set; }
    public bool IsComplete { get; set; }
}

public static class DesktopStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static string DirectoryPath
    {
        get
        {
            var overrideDirectory = Environment.GetEnvironmentVariable("CHOCOBO_COLOR_CALCULATOR_STATE_DIR");
            return string.IsNullOrWhiteSpace(overrideDirectory)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Chocobo Color Calculator")
                : Path.GetFullPath(overrideDirectory);
        }
    }

    public static string FilePath => Path.Combine(DirectoryPath, "desktop-state.json");

    public static DesktopState Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return NewState();
            return JsonSerializer.Deserialize<DesktopState>(File.ReadAllText(FilePath), JsonOptions) ?? NewState();
        }
        catch
        {
            return NewState();
        }
    }

    private static DesktopState NewState() => new() { CalculationModelVersion = 2 };

    public static void Save(DesktopState state)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporaryPath = FilePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temporaryPath, FilePath, true);
    }
}
