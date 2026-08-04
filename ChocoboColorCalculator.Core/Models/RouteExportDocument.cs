namespace ChocoboColorCalculator.Core.Models;

public enum RouteStepCompletion
{
    Pending,
    Manual,
    Automatic,
    ManualAndAutomatic,
}

public sealed record RouteExportStep(
    int Number,
    FruitKind Fruit,
    string FruitName,
    RgbColor RgbAfter,
    RouteStepCompletion Completion);

public sealed record RouteExportDocument(
    string StartName,
    RgbColor StartRgb,
    string TargetName,
    RgbColor TargetRgb,
    string PredictedColorName,
    RgbColor AimRgb,
    RgbColor EndpointRgb,
    double ClassificationMargin,
    DateTime CalculatedAtUtc,
    IReadOnlyList<RouteExportStep> Steps,
    string? Warning)
{
    public int CompletedCount => Steps.Count(step => step.Completion != RouteStepCompletion.Pending);

    public int NextStepNumber => Steps.FirstOrDefault(step => step.Completion == RouteStepCompletion.Pending)?.Number ?? -1;
}
