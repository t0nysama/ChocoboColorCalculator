using ChocoboColorCalculator.Core.Data;
using ChocoboColorCalculator.Core.Models;

namespace ChocoboColorCalculator.Core.Services;

public sealed class RouteCalculator
{
    private const int Lookahead = 3;
    private const int MaxSteps = 512;
    private const int InitialSearchRadius = 20;
    private const double MinimumReliableBoundaryClearance = 3;
    private const double NarrowBoundaryMargin = 4;

    private readonly IReadOnlyList<FruitKind[]> paths;

    public RouteCalculator()
    {
        var result = new List<FruitKind[]> { Array.Empty<FruitKind>() };
        BuildPaths(result, Array.Empty<FruitKind>(), Lookahead);
        paths = result;
    }

    public CalculationResult Calculate(ChocoboColor start, ChocoboColor target)
    {
        var aim = start.Name == target.Name
            ? start.Rgb
            : FindReliableAimPoint(start.Rgb, target);

        var steps = CalculateRoute(start.Rgb, aim, out var endpoint, out var usedFallback);
        var predicted = ChocoboData.NearestColor(endpoint);
        var margin = ClassificationMargin(endpoint, target);
        var warnings = new List<string>();

        if (!ReferenceEquals(predicted, target) && predicted.Name != target.Name)
        {
            warnings.Add($"The closest reachable endpoint resolves to {predicted.Name}; " +
                         "try Reliable target mode or reset to Desert Yellow with a Han Lemon.");
        }

        if (start.Name != "Desert Yellow")
        {
            warnings.Add("The game does not expose the hidden RGB value. A selected named starting " +
                         "color is necessarily an estimate; a Han Lemon reset gives the most reliable baseline.");
        }

        if (predicted.Name == target.Name && margin < NarrowBoundaryMargin)
        {
            warnings.Add($"{target.Name} has a narrow color region and the exact per-fruit variance is not " +
                         "published. Do not add fruit just because no feather-growth message appeared; if the " +
                         "result misses, calculate a correction from the resulting color instead of resetting.");
        }

        var warning = warnings.Count == 0 ? null : string.Join(" ", warnings);
        return new(start, target, aim, endpoint, predicted, steps, margin, usedFallback, warning);
    }

    public RgbColor Simulate(RgbColor start, IEnumerable<FruitKind> steps)
    {
        var current = start;
        foreach (var step in steps)
            current = ChocoboData.Fruit(step).Apply(current);
        return current;
    }

    public double ClassificationMargin(RgbColor point, ChocoboColor intended)
    {
        var intendedDistance = intended.Rgb.DistanceSquared(point);
        var margin = double.PositiveInfinity;
        foreach (var competitor in ChocoboData.Colors)
        {
            if (competitor.Name == intended.Name)
                continue;

            var centerDistance = Math.Sqrt(intended.Rgb.DistanceSquared(competitor.Rgb));
            var signedBoundaryDistance =
                (competitor.Rgb.DistanceSquared(point) - intendedDistance) /
                (2d * centerDistance);
            margin = Math.Min(margin, signedBoundaryDistance);
        }

        return margin;
    }

    private RgbColor FindReliableAimPoint(RgbColor start, ChocoboColor target)
    {
        var selection = new AimSelection();

        // First find a qualifying point. The radius expands to the whole RGB cube if a
        // color has an unusually small or offset Voronoi region.
        for (var radius = InitialSearchRadius; !selection.HasReliable; radius = Math.Min(255, radius * 2))
        {
            SearchAimCube(start, target, radius, selection);
            if (radius == 255)
                break;
        }

        if (!selection.HasReliable)
            return selection.HasClassified ? selection.ClosestClassified : target.Rgb;

        // Once one reliable endpoint is known, no better endpoint can be farther from
        // the swatch than its Euclidean distance. Searching that exact bound proves the
        // selected point is the globally closest reliable point on the start lattice.
        var exactRadius = (int)Math.Ceiling(Math.Sqrt(selection.ReliableDistance));
        SearchAimCube(start, target, exactRadius, selection);
        return selection.ClosestReliable;
    }

    private void SearchAimCube(RgbColor start, ChocoboColor target, int radius, AimSelection selection)
    {
        for (var r = FirstMatchingResidue(Math.Max(0, target.Rgb.R - radius), start.R);
             r <= Math.Min(255, target.Rgb.R + radius);
             r += 5)
        for (var g = FirstMatchingResidue(Math.Max(0, target.Rgb.G - radius), start.G);
             g <= Math.Min(255, target.Rgb.G + radius);
             g += 5)
        for (var b = FirstMatchingResidue(Math.Max(0, target.Rgb.B - radius), start.B);
             b <= Math.Min(255, target.Rgb.B + radius);
             b += 5)
        {
            var candidate = new RgbColor(r, g, b);
            if (!IsLatticeReachableWithoutClamping(start, candidate) ||
                ChocoboData.NearestColor(candidate).Name != target.Name)
                continue;

            var targetDistance = candidate.DistanceSquared(target.Rgb);
            var boundaryMargin = ClassificationMargin(candidate, target);
            var cost = EstimatedFruitCount(start, candidate);
            selection.ConsiderClassified(candidate, targetDistance, boundaryMargin, cost);

            if (boundaryMargin >= MinimumReliableBoundaryClearance)
                selection.ConsiderReliable(candidate, targetDistance, boundaryMargin, cost);
        }
    }

    private static int FirstMatchingResidue(int minimum, int reference)
    {
        var offset = ((reference - minimum) % 5 + 5) % 5;
        return minimum + offset;
    }

    private IReadOnlyList<FruitKind> CalculateRoute(
        RgbColor start,
        RgbColor aim,
        out RgbColor endpoint,
        out bool usedFallback)
    {
        if (start == aim)
        {
            endpoint = start;
            usedFallback = false;
            return [];
        }

        if (IsLatticeReachableWithoutClamping(start, aim))
        {
            var directRoute = BuildAlgebraicRoute(start, aim);
            var directEndpoint = Simulate(start, directRoute);
            if (directEndpoint == aim)
            {
                endpoint = directEndpoint;
                usedFallback = false;
                return directRoute;
            }
        }

        var steps = new List<FruitKind>();
        var current = start;
        var visited = new HashSet<(RgbColor Color, int Phase)>();
        usedFallback = false;

        while (current != aim && steps.Count < MaxSteps)
        {
            var currentDistance = current.DistanceSquared(aim);
            FruitKind[]? bestPath = null;
            var bestDistance = currentDistance;

            foreach (var path in paths)
            {
                var result = Simulate(current, path);
                var distance = result.DistanceSquared(aim);
                if (distance < bestDistance ||
                    (distance == bestDistance && bestPath is not null && path.Length < bestPath.Length))
                {
                    bestDistance = distance;
                    bestPath = path;
                }
            }

            if (bestPath is null || bestPath.Length == 0 || bestDistance >= currentDistance)
                break;

            var next = bestPath[0];
            var key = (current, steps.Count % Lookahead);
            if (!visited.Add(key))
                break;
            steps.Add(next);
            current = ChocoboData.Fruit(next).Apply(current);
        }

        if (current != aim && IsLatticeReachableWithoutClamping(current, aim))
        {
            usedFallback = true;
            foreach (var fruit in BuildAlgebraicRoute(current, aim))
            {
                steps.Add(fruit);
                current = ChocoboData.Fruit(fruit).Apply(current);
            }
        }

        endpoint = current;
        return steps;
    }

    private static IReadOnlyList<FruitKind> BuildAlgebraicRoute(RgbColor start, RgbColor target)
    {
        var dx = (target.R - start.R) / 5;
        var dy = (target.G - start.G) / 5;
        var dz = (target.B - start.B) / 5;
        var signed = new[]
        {
            -(dy + dz) / 2,
            -(dx + dz) / 2,
            -(dx + dy) / 2,
        };
        var kinds = new[]
        {
            signed[0] >= 0 ? FruitKind.XelphatolApple : FruitKind.DomanPlum,
            signed[1] >= 0 ? FruitKind.MamookPear : FruitKind.Valfruit,
            signed[2] >= 0 ? FruitKind.OGhomoroBerries : FruitKind.CieldalaesPineapple,
        };
        var remaining = signed.Select(Math.Abs).ToArray();
        var route = new List<FruitKind>(remaining.Sum());
        var current = start;

        while (remaining.Sum() > 0)
        {
            var choice = Enumerable.Range(0, 3)
                .Where(i => remaining[i] > 0)
                .OrderBy(i => ChocoboData.Fruit(kinds[i]).WouldClamp(current))
                .ThenBy(i => ChocoboData.Fruit(kinds[i]).Apply(current).DistanceSquared(target))
                .First();
            route.Add(kinds[choice]);
            current = ChocoboData.Fruit(kinds[choice]).Apply(current);
            remaining[choice]--;
        }

        return route;
    }

    private static bool IsLatticeReachableWithoutClamping(RgbColor start, RgbColor target)
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

    private static int EstimatedFruitCount(RgbColor start, RgbColor target)
    {
        var dx = (target.R - start.R) / 5;
        var dy = (target.G - start.G) / 5;
        var dz = (target.B - start.B) / 5;
        return (Math.Abs(dy + dz) + Math.Abs(dx + dz) + Math.Abs(dx + dy)) / 2;
    }

    private static void BuildPaths(List<FruitKind[]> paths, FruitKind[] prefix, int remaining)
    {
        if (remaining == 0)
            return;

        foreach (var fruit in Enum.GetValues<FruitKind>())
        {
            var next = new FruitKind[prefix.Length + 1];
            prefix.CopyTo(next, 0);
            next[^1] = fruit;
            paths.Add(next);
            BuildPaths(paths, next, remaining - 1);
        }
    }

    private sealed class AimSelection
    {
        public bool HasClassified { get; private set; }
        public RgbColor ClosestClassified { get; private set; }
        public long ClassifiedDistance { get; private set; } = long.MaxValue;
        public double ClassifiedMargin { get; private set; } = double.NegativeInfinity;
        public int ClassifiedCost { get; private set; } = int.MaxValue;

        public bool HasReliable { get; private set; }
        public RgbColor ClosestReliable { get; private set; }
        public long ReliableDistance { get; private set; } = long.MaxValue;
        public double ReliableMargin { get; private set; } = double.NegativeInfinity;
        public int ReliableCost { get; private set; } = int.MaxValue;

        public void ConsiderClassified(RgbColor point, long distance, double margin, int cost)
        {
            if (!IsBetter(point, distance, margin, cost,
                    HasClassified, ClosestClassified, ClassifiedDistance, ClassifiedMargin, ClassifiedCost))
                return;

            HasClassified = true;
            ClosestClassified = point;
            ClassifiedDistance = distance;
            ClassifiedMargin = margin;
            ClassifiedCost = cost;
        }

        public void ConsiderReliable(RgbColor point, long distance, double margin, int cost)
        {
            if (!IsBetter(point, distance, margin, cost,
                    HasReliable, ClosestReliable, ReliableDistance, ReliableMargin, ReliableCost))
                return;

            HasReliable = true;
            ClosestReliable = point;
            ReliableDistance = distance;
            ReliableMargin = margin;
            ReliableCost = cost;
        }

        private static bool IsBetter(
            RgbColor point,
            long distance,
            double margin,
            int cost,
            bool hasCurrent,
            RgbColor currentPoint,
            long currentDistance,
            double currentMargin,
            int currentCost)
        {
            if (!hasCurrent || distance < currentDistance)
                return true;
            if (distance > currentDistance)
                return false;
            if (margin > currentMargin + 0.0001)
                return true;
            if (margin < currentMargin - 0.0001)
                return false;
            if (cost != currentCost)
                return cost < currentCost;
            if (point.R != currentPoint.R)
                return point.R < currentPoint.R;
            if (point.G != currentPoint.G)
                return point.G < currentPoint.G;
            return point.B < currentPoint.B;
        }
    }
}
