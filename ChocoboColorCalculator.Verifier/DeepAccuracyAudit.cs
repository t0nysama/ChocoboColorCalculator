using ChocoboColorCalculator.Core.Data;
using ChocoboColorCalculator.Core.Models;
using ChocoboColorCalculator.Core.Services;

internal static class DeepAccuracyAudit
{
    public static void Run(RouteCalculator calculator)
    {
        var colors = ChocoboData.Colors.ToArray();
        var centerDistances = new double[colors.Length, colors.Length];
        for (var i = 0; i < colors.Length; i++)
        for (var j = 0; j < colors.Length; j++)
            centerDistances[i, j] = Math.Sqrt(colors[i].Rgb.DistanceSquared(colors[j].Rgb));

        var currentNotGlobalClosest = new List<string>();
        var currentNotClosestReliable = new List<string>();
        var currentNotGlobalSafest = new List<string>();
        var globalSafestOutsideRadius = new List<string>();
        var clampRoutes = new List<string>();
        var weakest = new List<(double Margin, string Pair, RgbColor Endpoint)>();
        long candidatesExamined = 0;

        for (var startIndex = 0; startIndex < colors.Length; startIndex++)
        {
            var start = colors[startIndex];
            var best = Enumerable.Range(0, colors.Length).Select(_ => new CandidateSet()).ToArray();

            for (var r = FirstResidue(start.Rgb.R); r <= 255; r += 5)
            for (var g = FirstResidue(start.Rgb.G); g <= 255; g += 5)
            for (var b = FirstResidue(start.Rgb.B); b <= 255; b += 5)
            {
                var point = new RgbColor(r, g, b);
                if (!Reachable(start.Rgb, point))
                    continue;
                candidatesExamined++;

                var nearestIndex = 0;
                var nearestDistance = long.MaxValue;
                for (var colorIndex = 0; colorIndex < colors.Length; colorIndex++)
                {
                    var distance = point.DistanceSquared(colors[colorIndex].Rgb);
                    if (distance < nearestDistance ||
                        (distance == nearestDistance &&
                         StringComparer.Ordinal.Compare(colors[colorIndex].Name, colors[nearestIndex].Name) < 0))
                    {
                        nearestDistance = distance;
                        nearestIndex = colorIndex;
                    }
                }

                var boundaryMargin = double.PositiveInfinity;
                for (var competitor = 0; competitor < colors.Length; competitor++)
                {
                    if (competitor == nearestIndex)
                        continue;
                    var competitorDistance = point.DistanceSquared(colors[competitor].Rgb);
                    var signedDistance =
                        (competitorDistance - nearestDistance) /
                        (2d * centerDistances[nearestIndex, competitor]);
                    boundaryMargin = Math.Min(boundaryMargin, signedDistance);
                }
                best[nearestIndex].Consider(
                    point,
                    nearestDistance,
                    boundaryMargin,
                    FruitCount(start.Rgb, point));
            }

            for (var targetIndex = 0; targetIndex < colors.Length; targetIndex++)
            {
                if (targetIndex == startIndex)
                    continue;
                var target = colors[targetIndex];
                var result = calculator.Calculate(start, target);
                var set = best[targetIndex];
                if (!set.HasValue)
                    throw new InvalidOperationException($"No reachable point for {start.Name} -> {target.Name}.");

                var currentDistance = result.Endpoint.DistanceSquared(target.Rgb);
                var currentMargin = BoundaryMargin(result.Endpoint, targetIndex, colors, centerDistances);
                weakest.Add((currentMargin, $"{start.Name} -> {target.Name}", result.Endpoint));

                if (currentDistance != set.ClosestDistance &&
                    (target.Name != "Soot Black" || start.Name == "Desert Yellow"))
                    currentNotGlobalClosest.Add(
                        $"{start.Name} -> {target.Name}: current {result.Endpoint} d2={currentDistance}, " +
                        $"closest {set.Closest} d2={set.ClosestDistance}");
                if (result.Endpoint != set.ClosestReliable &&
                    (target.Name != "Soot Black" || start.Name == "Desert Yellow"))
                    currentNotClosestReliable.Add(
                        $"{start.Name} -> {target.Name}: current {result.Endpoint} d2={currentDistance}, " +
                        $"closest reliable {set.ClosestReliable} d2={set.ClosestReliableDistance} " +
                        $"boundary={set.ClosestReliableMargin:F3}");
                if (result.Endpoint != set.Safest)
                    currentNotGlobalSafest.Add(
                        $"{start.Name} -> {target.Name}: current {result.Endpoint} margin={currentMargin:F3}, " +
                        $"safest {set.Safest} margin={set.SafestMargin:F3}");
                if (Math.Abs(set.Safest.R - target.Rgb.R) > 20 ||
                    Math.Abs(set.Safest.G - target.Rgb.G) > 20 ||
                    Math.Abs(set.Safest.B - target.Rgb.B) > 20)
                    globalSafestOutsideRadius.Add(
                        $"{start.Name} -> {target.Name}: safest {set.Safest} margin={set.SafestMargin:F3}");

                var state = start.Rgb;
                foreach (var kind in result.Steps)
                {
                    var fruit = ChocoboData.Fruit(kind);
                    if (fruit.WouldClamp(state))
                    {
                        clampRoutes.Add($"{start.Name} -> {target.Name}: {kind} at {state}");
                        break;
                    }
                    state = fruit.Apply(state);
                }
            }

            Console.WriteLine($"Audited lattice {startIndex + 1}/85: {start.Name}");
        }

        Console.WriteLine($"Reachable lattice candidates examined: {candidatesExamined:N0}");
        Console.WriteLine($"Routes whose absolute closest point is below the 3-unit boundary floor: {currentNotGlobalClosest.Count:N0}");
        foreach (var line in currentNotGlobalClosest.Take(20)) Console.WriteLine(line);
        Console.WriteLine($"Current endpoints not globally closest with boundary clearance >= 3: {currentNotClosestReliable.Count:N0}");
        foreach (var line in currentNotClosestReliable.Take(20)) Console.WriteLine(line);
        Console.WriteLine($"Current endpoints not globally safest: {currentNotGlobalSafest.Count:N0}");
        foreach (var line in currentNotGlobalSafest.Take(10)) Console.WriteLine(line);
        Console.WriteLine($"Global safest endpoints outside radius 20: {globalSafestOutsideRadius.Count:N0}");
        foreach (var line in globalSafestOutsideRadius.Take(20)) Console.WriteLine(line);
        Console.WriteLine($"Routes that clamp: {clampRoutes.Count:N0}");
        foreach (var line in clampRoutes.Take(20)) Console.WriteLine(line);
        Console.WriteLine("Ten weakest true Voronoi boundary margins:");
        foreach (var item in weakest.OrderBy(x => x.Margin).Take(10))
            Console.WriteLine($"{item.Margin:F3} {item.Pair} at {item.Endpoint}");

        if (currentNotClosestReliable.Count != 0)
            throw new InvalidOperationException(
                $"{currentNotClosestReliable.Count:N0} non-Soot routes did not select the globally closest reliable endpoint.");
        if (clampRoutes.Count != 0)
            throw new InvalidOperationException($"{clampRoutes.Count:N0} routes clamp a channel.");
    }

    private static double BoundaryMargin(
        RgbColor point,
        int intendedIndex,
        IReadOnlyList<ChocoboColor> colors,
        double[,] centerDistances)
    {
        var intendedDistance = point.DistanceSquared(colors[intendedIndex].Rgb);
        var result = double.PositiveInfinity;
        for (var competitor = 0; competitor < colors.Count; competitor++)
        {
            if (competitor == intendedIndex)
                continue;
            var signedDistance =
                (point.DistanceSquared(colors[competitor].Rgb) - intendedDistance) /
                (2d * centerDistances[intendedIndex, competitor]);
            result = Math.Min(result, signedDistance);
        }
        return result;
    }

    private static int FirstResidue(int value) => ((value % 5) + 5) % 5;

    private static bool Reachable(RgbColor start, RgbColor target)
    {
        var dr = target.R - start.R;
        var dg = target.G - start.G;
        var db = target.B - start.B;
        if (dr % 5 != 0 || dg % 5 != 0 || db % 5 != 0)
            return false;
        var dx = dr / 5;
        var dy = dg / 5;
        var dz = db / 5;
        return (dy + dz) % 2 == 0 && (dx + dz) % 2 == 0 && (dx + dy) % 2 == 0;
    }

    private static int FruitCount(RgbColor start, RgbColor target)
    {
        var dx = (target.R - start.R) / 5;
        var dy = (target.G - start.G) / 5;
        var dz = (target.B - start.B) / 5;
        return (Math.Abs(dy + dz) + Math.Abs(dx + dz) + Math.Abs(dx + dy)) / 2;
    }

    private sealed class CandidateSet
    {
        public bool HasValue { get; private set; }
        public RgbColor Closest { get; private set; }
        public long ClosestDistance { get; private set; } = long.MaxValue;
        public double ClosestMargin { get; private set; } = double.NegativeInfinity;
        public int ClosestCost { get; private set; } = int.MaxValue;
        public RgbColor Safest { get; private set; }
        public double SafestMargin { get; private set; } = double.NegativeInfinity;
        public long SafestDistance { get; private set; } = long.MaxValue;
        public int SafestCost { get; private set; } = int.MaxValue;
        public RgbColor ClosestReliable { get; private set; }
        public long ClosestReliableDistance { get; private set; } = long.MaxValue;
        public double ClosestReliableMargin { get; private set; } = double.NegativeInfinity;
        public int ClosestReliableCost { get; private set; } = int.MaxValue;

        public void Consider(RgbColor point, long targetDistance, double margin, int cost)
        {
            HasValue = true;
            if (targetDistance < ClosestDistance ||
                (targetDistance == ClosestDistance && margin > ClosestMargin + 1e-9) ||
                (targetDistance == ClosestDistance && Math.Abs(margin - ClosestMargin) < 1e-9 && cost < ClosestCost))
            {
                Closest = point;
                ClosestDistance = targetDistance;
                ClosestMargin = margin;
                ClosestCost = cost;
            }
            if (margin >= 3d &&
                (targetDistance < ClosestReliableDistance ||
                 (targetDistance == ClosestReliableDistance && margin > ClosestReliableMargin + 1e-9) ||
                 (targetDistance == ClosestReliableDistance && Math.Abs(margin - ClosestReliableMargin) < 1e-9 && cost < ClosestReliableCost)))
            {
                ClosestReliable = point;
                ClosestReliableDistance = targetDistance;
                ClosestReliableMargin = margin;
                ClosestReliableCost = cost;
            }
            if (margin > SafestMargin + 1e-9 ||
                (Math.Abs(margin - SafestMargin) < 1e-9 && targetDistance < SafestDistance) ||
                (Math.Abs(margin - SafestMargin) < 1e-9 && targetDistance == SafestDistance && cost < SafestCost))
            {
                Safest = point;
                SafestMargin = margin;
                SafestDistance = targetDistance;
                SafestCost = cost;
            }
        }
    }
}
