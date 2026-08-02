using ChocoboColorCalculator.Core.Data;
using ChocoboColorCalculator.Core.Models;
using ChocoboColorCalculator.Core.Services;

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

Require(ChocoboData.Colors.Count == 85, $"Expected 85 colors, got {ChocoboData.Colors.Count}.");
Require(ChocoboData.Colors.Select(c => c.Name).Distinct().Count() == 85, "Color names must be unique.");
Require(ChocoboData.Fruits.Count == 6, "Expected six color-changing fruits.");
Require(ChocoboData.Fruits.Select(f => f.ItemId).Distinct().Count() == 6, "Fruit item IDs must be unique.");

var calculator = new RouteCalculator();
var failures = new List<string>();
var longest = 0;
var minimumMargin = double.PositiveInfinity;
string? minimumMarginPair = null;
var pairCount = 0;

foreach (var start in ChocoboData.Colors)
foreach (var target in ChocoboData.Colors)
{
    pairCount++;
    var result = calculator.Calculate(start, target, TargetMode.SafeCenter);
    var simulated = calculator.Simulate(start.Rgb, result.Steps);
    if (simulated != result.Endpoint)
        failures.Add($"{start.Name} -> {target.Name}: simulation differs from endpoint.");
    if (result.PredictedColor.Name != target.Name)
        failures.Add($"{start.Name} -> {target.Name}: predicts {result.PredictedColor.Name}.");
    if (result.Steps.Count > 512)
        failures.Add($"{start.Name} -> {target.Name}: route exceeds 512 steps.");
    if (start.Name == target.Name && result.Steps.Count != 0)
        failures.Add($"{start.Name} -> itself should require no fruit.");
    longest = Math.Max(longest, result.Steps.Count);
    if (start.Name != target.Name && result.ClassificationMargin < minimumMargin)
    {
        minimumMargin = result.ClassificationMargin;
        minimumMarginPair = $"{start.Name} -> {target.Name}";
    }
}

Require(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(30)));

var honey = ChocoboData.Colors.Single(c => c.Name == "Honey Yellow");
var celeste = ChocoboData.Colors.Single(c => c.Name == "Celeste Green");
var currant = ChocoboData.Colors.Single(c => c.Name == "Currant Purple");
Require(calculator.Calculate(honey, currant).PredictedColor.Name == "Currant Purple",
    "Honey Yellow -> Currant Purple regression.");
Require(calculator.Calculate(celeste, currant).PredictedColor.Name == "Currant Purple",
    "Celeste Green -> Currant Purple regression.");

Console.WriteLine($"Verified {pairCount:N0} color pairs; longest safe route: {longest} fruits.");
Console.WriteLine($"Smallest endpoint classification margin: {minimumMargin:F2} ({minimumMarginPair}).");
