namespace ChocoboColorCalculator.Core.Models;

public sealed record RouteExportStep(
    int Number,
    FruitKind Fruit,
    string FruitName,
    RgbColor RgbAfter);

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
    string? Warning);
