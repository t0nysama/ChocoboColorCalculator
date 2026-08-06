using ChocoboColorCalculator.Core.Data;
using ChocoboColorCalculator.Core.Models;
using ChocoboColorCalculator.Core.Services;
using System.Text;

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

Require(ChocoboData.Colors.Count == 85, $"Expected 85 colors, got {ChocoboData.Colors.Count}.");
Require(ChocoboData.Colors.Select(c => c.Name).Distinct().Count() == 85, "Color names must be unique.");
Require(ChocoboData.Fruits.Count == 6, "Expected six color-changing fruits.");
Require(ChocoboData.Fruits.Select(f => f.ItemId).Distinct().Count() == 6, "Fruit item IDs must be unique.");

foreach (var fruit in ChocoboData.Fruits)
{
    var detected = FruitMessageDetector.Detect([$"Sunflower devours the {fruit.Name.ToLowerInvariant()}."]);
    Require(detected == fruit.Kind, $"Failed to detect {fruit.Name} from a feeding log message.");
}

Require(
    FruitMessageDetector.Detect(["Valfruit devours the Doman plum."]) == FruitKind.DomanPlum,
    "The consumed fruit must take precedence when a chocobo name is also a fruit name.");
Require(
    FruitMessageDetector.Detect(["You tend to your chocobo."]) is null,
    "Unrelated stable messages must not be detected as fruit feedings.");

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
    var result = calculator.Calculate(start, target);
    var repeated = calculator.Calculate(start, target);
    var simulated = calculator.Simulate(start.Rgb, result.Steps);
    if (simulated != result.Endpoint)
        failures.Add($"{start.Name} -> {target.Name}: simulation differs from endpoint.");
    if (result.Endpoint != result.AimPoint)
        failures.Add($"{start.Name} -> {target.Name}: ordered route did not reach its selected aim point.");
    if (result.PredictedColor.Name != target.Name)
        failures.Add($"{start.Name} -> {target.Name}: predicts {result.PredictedColor.Name}.");
    if (start.Name != target.Name && result.ClassificationMargin <= 0)
        failures.Add($"{start.Name} -> {target.Name}: endpoint has no positive classification margin.");
    if (result.Steps.Count > 512)
        failures.Add($"{start.Name} -> {target.Name}: route exceeds 512 steps.");
    if (result.AimPoint != repeated.AimPoint ||
        result.Endpoint != repeated.Endpoint ||
        !result.Steps.SequenceEqual(repeated.Steps))
        failures.Add($"{start.Name} -> {target.Name}: repeated calculation is not deterministic.");
    if (start.Name == target.Name && result.Steps.Count != 0)
        failures.Add($"{start.Name} -> itself should require no fruit.");
    var routeRgb = start.Rgb;
    foreach (var kind in result.Steps)
    {
        var fruit = ChocoboData.Fruit(kind);
        if (fruit.WouldClamp(routeRgb))
        {
            failures.Add($"{start.Name} -> {target.Name}: route clamps {kind} at {routeRgb}.");
            break;
        }
        routeRgb = fruit.Apply(routeRgb);
    }
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
var desert = ChocoboData.Colors.Single(c => c.Name == "Desert Yellow");
var soot = ChocoboData.Colors.Single(c => c.Name == "Soot Black");
var desertToSoot = calculator.Calculate(desert, soot);
Require(calculator.Calculate(honey, currant).PredictedColor.Name == "Currant Purple",
    "Honey Yellow -> Currant Purple regression.");
Require(calculator.Calculate(celeste, currant).PredictedColor.Name == "Currant Purple",
    "Celeste Green -> Currant Purple regression.");
Require(desertToSoot.Endpoint == new RgbColor(39, 40, 37),
    $"Desert Yellow -> Soot Black should use the closest reliable endpoint, got {desertToSoot.Endpoint}.");
Require(desertToSoot.Steps.Count(kind => kind == FruitKind.XelphatolApple) == 19,
    "Desert Yellow -> Soot Black should use 19 Xelphatol apples.");
Require(desertToSoot.Steps.Count(kind => kind == FruitKind.MamookPear) == 23,
    "Desert Yellow -> Soot Black should use 23 Mamook pears.");
Require(desertToSoot.Steps.Count(kind => kind == FruitKind.OGhomoroBerries) == 32,
    "Desert Yellow -> Soot Black should use 32 O'Ghomoro berries.");
var sootRouteRgb = desert.Rgb;
foreach (var kind in desertToSoot.Steps)
{
    Require(!ChocoboData.Fruit(kind).WouldClamp(sootRouteRgb),
        $"Desert Yellow -> Soot Black must not clamp while feeding {kind} from {sootRouteRgb}.");
    sootRouteRgb = ChocoboData.Fruit(kind).Apply(sootRouteRgb);
}

var exportRgb = desert.Rgb;
var exportSteps = new List<RouteExportStep>(desertToSoot.Steps.Count);
for (var index = 0; index < desertToSoot.Steps.Count; index++)
{
    var kind = desertToSoot.Steps[index];
    exportRgb = ChocoboData.Fruit(kind).Apply(exportRgb);
    exportSteps.Add(new RouteExportStep(index + 1, kind, ChocoboData.Fruit(kind).Name, exportRgb));
}
var exportDocument = new RouteExportDocument(
    desert.Name,
    desert.Rgb,
    soot.Name,
    soot.Rgb,
    desertToSoot.PredictedColor.Name,
    desertToSoot.AimPoint,
    desertToSoot.Endpoint,
    desertToSoot.ClassificationMargin,
    DateTime.UtcNow,
    exportSteps,
    desertToSoot.Warning);
var textExport = RouteExporter.CreateText(exportDocument);
var htmlExport = RouteExporter.CreateHtml(exportDocument);
var pdfExport = RouteExporter.CreatePdf(exportDocument);
var pdfText = Encoding.ASCII.GetString(pdfExport);
Require(textExport.Contains("HOW TO USE THIS ROUTE", StringComparison.Ordinal), "Text export is missing instructions.");
Require(textExport.Contains("74    ", StringComparison.Ordinal), "Text export is missing the final route step.");
Require(!textExport.Contains("STATUS", StringComparison.Ordinal), "Text export must not contain a static status column.");
Require(!textExport.Contains("AUTO-DETECTED", StringComparison.Ordinal), "Text export must not contain saved step statuses.");
Require(htmlExport.Contains("<table>", StringComparison.Ordinal), "HTML export is missing the route table.");
Require(!htmlExport.Contains("<th>STATUS</th>", StringComparison.Ordinal), "HTML export must not contain a static status column.");
Require(!htmlExport.Contains("AUTO-DETECTED", StringComparison.Ordinal), "HTML export must not contain saved step statuses.");
Require(pdfText.StartsWith("%PDF-1.4", StringComparison.Ordinal), "PDF export has an invalid header.");
Require(pdfText.Contains("/Count 4", StringComparison.Ordinal), "PDF export has an unexpected page count.");
Require(pdfText.Contains("(TOTAL FEEDS)", StringComparison.Ordinal), "PDF export is missing its route summary.");
Require(!pdfText.Contains("(STATUS)", StringComparison.Ordinal), "PDF export must not contain a static status column.");
Require(!pdfText.Contains("(AUTO-DETECTED)", StringComparison.Ordinal), "PDF export must not contain saved step statuses.");
Require(pdfText.EndsWith("%%EOF\n", StringComparison.Ordinal), "PDF export is missing its end marker.");

if (args.Length == 1 && !string.Equals(args[0], "--deep-audit", StringComparison.Ordinal))
{
    var exportDirectory = Path.GetFullPath(args[0]);
    foreach (var format in Enum.GetValues<RouteExportFormat>())
        Console.WriteLine($"Created export sample: {RouteExporter.Export(exportDocument, format, exportDirectory)}");
}

if (args.Contains("--deep-audit", StringComparer.Ordinal))
    DeepAccuracyAudit.Run(calculator);

Console.WriteLine($"Verified {pairCount:N0} color pairs; longest reliable route: {longest} fruits.");
Console.WriteLine($"Smallest true boundary clearance: {minimumMargin:F2} RGB units ({minimumMarginPair}).");
Console.WriteLine(
    $"Desert Yellow -> Soot Black: {desertToSoot.Steps.Count} fruits, " +
    $"aim {desertToSoot.AimPoint}, endpoint {desertToSoot.Endpoint}, " +
    $"margin {desertToSoot.ClassificationMargin:F2}.");
Console.WriteLine(
    string.Join(", ", desertToSoot.Steps.GroupBy(kind => kind).Select(group => $"{group.Key} x{group.Count()}")));
