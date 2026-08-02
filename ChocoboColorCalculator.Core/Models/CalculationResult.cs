namespace ChocoboColorCalculator.Core.Models;

public enum TargetMode
{
    SafeCenter,
    PublishedRgb,
}

public sealed record CalculationResult(
    ChocoboColor Start,
    ChocoboColor Target,
    RgbColor AimPoint,
    RgbColor Endpoint,
    ChocoboColor PredictedColor,
    IReadOnlyList<FruitKind> Steps,
    double ClassificationMargin,
    bool UsedFallback,
    string? Warning);
