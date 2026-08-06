using ChocoboColorCalculator.Core.Data;
using ChocoboColorCalculator.Core.Models;
using ChocoboColorCalculator.Core.Services;

internal static class SootBlackAudit
{
    private const int MonteCarloTrials = 50_000;

    public static void Run(RouteCalculator calculator)
    {
        var soot = ChocoboData.Colors.Single(color => color.Name == "Soot Black");
        var desert = ChocoboData.Colors.Single(color => color.Name == "Desert Yellow");
        var failures = new List<string>();
        var weakest = (Margin: double.PositiveInfinity, Pair: string.Empty);
        var longest = (Feeds: 0, Pair: string.Empty);
        var productionCoverageFive = new List<double>();
        var coverageImprovements = new List<string>();
        long hiddenStartScenarios = 0;
        long hiddenStartClampScenarios = 0;

        foreach (var start in ChocoboData.Colors)
        {
            var result = calculator.Calculate(start, soot);
            var simulated = calculator.Simulate(start.Rgb, result.Steps);
            if (simulated != result.Endpoint || result.Endpoint != result.AimPoint)
                failures.Add($"{start.Name}: route does not reach its Soot Black aim.");
            if (result.PredictedColor.Name != soot.Name)
                failures.Add($"{start.Name}: predicts {result.PredictedColor.Name}.");
            if (start.Name != soot.Name && result.ClassificationMargin < 3d)
                failures.Add($"{start.Name}: boundary clearance {result.ClassificationMargin:F3} is below 3.");

            var routeRgb = start.Rgb;
            foreach (var kind in result.Steps)
            {
                var fruit = ChocoboData.Fruit(kind);
                if (fruit.WouldClamp(routeRgb))
                {
                    failures.Add($"{start.Name}: route clamps {kind} at {routeRgb}.");
                    break;
                }
                routeRgb = fruit.Apply(routeRgb);
            }

            if (start.Name != soot.Name && result.ClassificationMargin < weakest.Margin)
                weakest = (result.ClassificationMargin, start.Name);
            if (result.Steps.Count > longest.Feeds)
                longest = (result.Steps.Count, start.Name);

            if (start.Name == soot.Name)
                continue;

            var offsets = ValidNamedOffsets(start, 5);
            var productionCoverage = LinearCoverage(result.Endpoint, offsets, soot);
            productionCoverageFive.Add(productionCoverage);

            foreach (var offset in offsets)
            {
                hiddenStartScenarios++;
                var actual = new RgbColor(
                    start.Rgb.R + offset.R,
                    start.Rgb.G + offset.G,
                    start.Rgb.B + offset.B);
                var clamped = false;
                foreach (var kind in result.Steps)
                {
                    var fruit = ChocoboData.Fruit(kind);
                    clamped |= fruit.WouldClamp(actual);
                    actual = fruit.Apply(actual);
                }
                if (clamped)
                    hiddenStartClampScenarios++;
            }

            var allCandidates = ReachableSootCandidates(start.Rgb, soot, calculator);
            var candidates = allCandidates
                .OrderBy(candidate => candidate.TargetDistance)
                .Take(64)
                .Concat(allCandidates
                    .OrderByDescending(candidate => candidate.Margin)
                    .Take(32))
                .DistinctBy(candidate => candidate.Point)
                .ToList();
            var bestCoverage = candidates
                .Select(candidate => (Candidate: candidate, Coverage: LinearCoverage(candidate.Point, offsets, soot)))
                .OrderByDescending(item => item.Coverage)
                .ThenBy(item => item.Candidate.TargetDistance)
                .ThenByDescending(item => item.Candidate.Margin)
                .First();
            if (start.Name != "Desert Yellow" &&
                Math.Abs(bestCoverage.Coverage - productionCoverage) > 0.000001)
                failures.Add(
                    $"{start.Name}: selected Soot endpoint coverage {productionCoverage:P2}, " +
                    $"best audited coverage {bestCoverage.Coverage:P2}.");
            if (bestCoverage.Coverage > productionCoverage + 0.05)
            {
                coverageImprovements.Add(
                    $"{start.Name}: production {productionCoverage:P1} at {result.Endpoint}; " +
                    $"local-robust {bestCoverage.Coverage:P1} at {bestCoverage.Candidate.Point} " +
                    $"(margin {bestCoverage.Candidate.Margin:F2}).");
            }
        }

        if (failures.Count != 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, failures.Take(30)));

        var desertRoute = calculator.Calculate(desert, soot);
        Require(desertRoute.Endpoint == new RgbColor(39, 40, 37),
            $"Desert Yellow endpoint changed to {desertRoute.Endpoint}.");
        Require(desertRoute.Steps.Count(kind => kind == FruitKind.XelphatolApple) == 19,
            "Desert Yellow route must contain 19 apples.");
        Require(desertRoute.Steps.Count(kind => kind == FruitKind.MamookPear) == 23,
            "Desert Yellow route must contain 23 pears.");
        Require(desertRoute.Steps.Count(kind => kind == FruitKind.OGhomoroBerries) == 32,
            "Desert Yellow route must contain 32 berries.");
        Require(MaximumRunLength(desertRoute.Steps) == 1,
            "Desert Yellow Soot Black route must fully interleave fruit types.");

        var proportionalRoute = BuildProportionalRoute(19, 23, 32);
        Require(calculator.Simulate(desert.Rgb, proportionalRoute) == desertRoute.Endpoint,
            "Proportional Soot Black order does not reach the established endpoint.");

        var nearbyCandidates = ReachableSootCandidates(desert.Rgb, soot, calculator)
            .Where(candidate => candidate.TargetDistance <= 150 && candidate.Steps.Count <= 85)
            .OrderBy(candidate => candidate.TargetDistance)
            .ThenByDescending(candidate => candidate.Margin)
            .ToList();
        var stochastic = new List<StochasticResult>();
        foreach (var candidate in nearbyCandidates)
        {
            stochastic.Add(EvaluateVariation(candidate, desert.Rgb, soot, independentChannels: false));
            stochastic.Add(EvaluateVariation(candidate, desert.Rgb, soot, independentChannels: true));
        }

        var productionCommon = stochastic.Single(item =>
            item.Endpoint == desertRoute.Endpoint && !item.IndependentChannels);
        var productionIndependent = stochastic.Single(item =>
            item.Endpoint == desertRoute.Endpoint && item.IndependentChannels);
        var bestCommon = stochastic.Where(item => !item.IndependentChannels)
            .OrderByDescending(item => item.HitRate).ThenBy(item => item.ExpectedTargetDistanceSquared).First();
        var bestIndependent = stochastic.Where(item => item.IndependentChannels)
            .OrderByDescending(item => item.HitRate).ThenBy(item => item.ExpectedTargetDistanceSquared).First();
        var proportionalCommon = EvaluateVariation(
            desertRoute.Endpoint,
            proportionalRoute,
            desert.Rgb,
            soot,
            independentChannels: false,
            label: "proportional");
        var proportionalIndependent = EvaluateVariation(
            desertRoute.Endpoint,
            proportionalRoute,
            desert.Rgb,
            soot,
            independentChannels: true,
            label: "proportional");

        var omissionsStillSoot = 0;
        for (var omitted = 0; omitted < desertRoute.Steps.Count; omitted++)
        {
            var shortened = desertRoute.Steps.Where((_, index) => index != omitted);
            if (ChocoboData.NearestColor(calculator.Simulate(desert.Rgb, shortened)).Name == soot.Name)
                omissionsStillSoot++;
        }

        Console.WriteLine("SOOT BLACK FOCUSED AUDIT");
        Console.WriteLine($"Verified all {ChocoboData.Colors.Count:N0} named starts -> Soot Black.");
        Console.WriteLine($"Weakest production boundary clearance: {weakest.Margin:F3} ({weakest.Pair} -> Soot Black).");
        Console.WriteLine($"Longest Soot Black correction: {longest.Feeds} fruit ({longest.Pair}).");
        Console.WriteLine($"Mean local hidden-start coverage within +/-5 RGB: {productionCoverageFive.Average():P1}.");
        Console.WriteLine($"Hidden-start route simulations: {hiddenStartScenarios:N0}; clamped: {hiddenStartClampScenarios:N0}.");
        Console.WriteLine($"Starts with a >5 point local-coverage alternative: {coverageImprovements.Count}.");
        foreach (var line in coverageImprovements.Take(15)) Console.WriteLine(line);
        Console.WriteLine(
            $"Desert Yellow established route: {desertRoute.Steps.Count} fruit, endpoint {desertRoute.Endpoint}, " +
            $"boundary {desertRoute.ClassificationMargin:F3}.");
        Console.WriteLine($"Single omitted feeds still classifying as Soot Black: {omissionsStillSoot}/{desertRoute.Steps.Count}.");
        Console.WriteLine(
            $"Symmetric common-magnitude 4/5/6 model: production {productionCommon.HitRate:P2}; " +
            $"best nearby {bestCommon.HitRate:P2} at {bestCommon.Endpoint} " +
            $"({bestCommon.AppleCount}/{bestCommon.PearCount}/{bestCommon.BerryCount}).");
        Console.WriteLine($"  Production outcomes: {productionCommon.OutcomeSummary}");
        Console.WriteLine($"  Proportional same-count order: {proportionalCommon.HitRate:P2}; {proportionalCommon.OutcomeSummary}; clamp trials {proportionalCommon.ClampTrialRate:P2}.");
        Console.WriteLine(
            $"Symmetric independent-channel 4/5/6 model: production {productionIndependent.HitRate:P2}; " +
            $"best nearby {bestIndependent.HitRate:P2} at {bestIndependent.Endpoint} " +
            $"({bestIndependent.AppleCount}/{bestIndependent.PearCount}/{bestIndependent.BerryCount}).");
        Console.WriteLine($"  Production outcomes: {productionIndependent.OutcomeSummary}");
        Console.WriteLine($"  Proportional same-count order: {proportionalIndependent.HitRate:P2}; {proportionalIndependent.OutcomeSummary}; clamp trials {proportionalIndependent.ClampTrialRate:P2}.");
        Console.WriteLine("Top common-magnitude candidates:");
        foreach (var item in stochastic.Where(item => !item.IndependentChannels)
                     .OrderByDescending(item => item.HitRate).Take(8))
            Console.WriteLine(item);
        Console.WriteLine("Top independent-channel candidates:");
        foreach (var item in stochastic.Where(item => item.IndependentChannels)
                     .OrderByDescending(item => item.HitRate).Take(8))
            Console.WriteLine(item);
        Console.WriteLine(
            $"Production order first 18: {string.Join(", ", desertRoute.Steps.Take(18).Select(ShortName))}");
        Console.WriteLine(
            $"Proportional order first 18: {string.Join(", ", proportionalRoute.Take(18).Select(ShortName))}");
    }

    private static IReadOnlyList<Candidate> ReachableSootCandidates(
        RgbColor start,
        ChocoboColor soot,
        RouteCalculator calculator)
    {
        var result = new List<Candidate>();
        for (var r = ((start.R % 5) + 5) % 5; r <= 255; r += 5)
        for (var g = ((start.G % 5) + 5) % 5; g <= 255; g += 5)
        for (var b = ((start.B % 5) + 5) % 5; b <= 255; b += 5)
        {
            var point = new RgbColor(r, g, b);
            if (!Reachable(start, point) || ChocoboData.NearestColor(point).Name != soot.Name)
                continue;
            var steps = BuildExactRoute(start, point);
            if (calculator.Simulate(start, steps) != point)
                continue;
            result.Add(new Candidate(
                point,
                point.DistanceSquared(soot.Rgb),
                calculator.ClassificationMargin(point, soot),
                steps));
        }
        return result;
    }

    private static IReadOnlyList<RgbColor> ValidNamedOffsets(ChocoboColor start, int radius)
    {
        var result = new List<RgbColor>();
        for (var dr = -radius; dr <= radius; dr++)
        for (var dg = -radius; dg <= radius; dg++)
        for (var db = -radius; db <= radius; db++)
        {
            var actual = new RgbColor(start.Rgb.R + dr, start.Rgb.G + dg, start.Rgb.B + db);
            if (actual.R is < 0 or > 255 || actual.G is < 0 or > 255 || actual.B is < 0 or > 255)
                continue;
            if (ChocoboData.NearestColor(actual).Name == start.Name)
                result.Add(new RgbColor(dr, dg, db));
        }
        return result;
    }

    private static double LinearCoverage(RgbColor endpoint, IReadOnlyList<RgbColor> offsets, ChocoboColor soot)
    {
        var hits = offsets.Count(offset =>
            ChocoboData.NearestColor(new RgbColor(
                endpoint.R + offset.R,
                endpoint.G + offset.G,
                endpoint.B + offset.B).Clamp()).Name == soot.Name);
        return hits / (double)offsets.Count;
    }

    private static StochasticResult EvaluateVariation(
        Candidate candidate,
        RgbColor start,
        ChocoboColor soot,
        bool independentChannels)
        => EvaluateVariation(
            candidate.Point,
            candidate.Steps,
            start,
            soot,
            independentChannels,
            "greedy");

    private static StochasticResult EvaluateVariation(
        RgbColor endpoint,
        IReadOnlyList<FruitKind> steps,
        RgbColor start,
        ChocoboColor soot,
        bool independentChannels,
        string label)
    {
        var seed = unchecked(
            (endpoint.R * 1_000_003) +
            (endpoint.G * 10_007) +
            (endpoint.B * 101) +
            (independentChannels ? 1 : 0));
        var random = new Random(seed);
        var hits = 0;
        var clampTrials = 0;
        var outcomes = new Dictionary<string, int>(StringComparer.Ordinal);
        double totalDistance = 0;
        for (var trial = 0; trial < MonteCarloTrials; trial++)
        {
            var current = start;
            var clamped = false;
            foreach (var kind in steps)
            {
                var delta = ChocoboData.Fruit(kind).Delta;
                var common = random.Next(4, 7);
                var redMagnitude = independentChannels ? random.Next(4, 7) : common;
                var greenMagnitude = independentChannels ? random.Next(4, 7) : common;
                var blueMagnitude = independentChannels ? random.Next(4, 7) : common;
                var next = new RgbColor(
                    current.R + Math.Sign(delta.R) * redMagnitude,
                    current.G + Math.Sign(delta.G) * greenMagnitude,
                    current.B + Math.Sign(delta.B) * blueMagnitude);
                clamped |= next.R is < 0 or > 255 || next.G is < 0 or > 255 || next.B is < 0 or > 255;
                current = next.Clamp();
            }
            if (clamped)
                clampTrials++;
            var outcome = ChocoboData.NearestColor(current).Name;
            outcomes[outcome] = outcomes.GetValueOrDefault(outcome) + 1;
            if (outcome == soot.Name)
                hits++;
            totalDistance += current.DistanceSquared(soot.Rgb);
        }

        var counts = Counts(steps);
        return new StochasticResult(
            label,
            endpoint,
            counts.Apple,
            counts.Pear,
            counts.Berry,
            independentChannels,
            hits / (double)MonteCarloTrials,
            totalDistance / MonteCarloTrials,
            BoundaryMargin: BoundaryMargin(endpoint, soot),
            clampTrials / (double)MonteCarloTrials,
            outcomes.ToDictionary(pair => pair.Key, pair => pair.Value / (double)MonteCarloTrials, StringComparer.Ordinal));
    }

    private static double BoundaryMargin(RgbColor point, ChocoboColor intended)
    {
        var intendedDistance = intended.Rgb.DistanceSquared(point);
        return ChocoboData.Colors
            .Where(competitor => competitor.Name != intended.Name)
            .Min(competitor =>
                (competitor.Rgb.DistanceSquared(point) - intendedDistance) /
                (2d * Math.Sqrt(intended.Rgb.DistanceSquared(competitor.Rgb))));
    }

    private static IReadOnlyList<FruitKind> BuildExactRoute(RgbColor start, RgbColor target)
    {
        var dx = (target.R - start.R) / 5;
        var dy = (target.G - start.G) / 5;
        var dz = (target.B - start.B) / 5;
        var signed = new[] { -(dy + dz) / 2, -(dx + dz) / 2, -(dx + dy) / 2 };
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
                .Where(index => remaining[index] > 0)
                .OrderBy(index => ChocoboData.Fruit(kinds[index]).WouldClamp(current))
                .ThenBy(index => ChocoboData.Fruit(kinds[index]).Apply(current).DistanceSquared(target))
                .First();
            route.Add(kinds[choice]);
            current = ChocoboData.Fruit(kinds[choice]).Apply(current);
            remaining[choice]--;
        }
        return route;
    }

    private static IReadOnlyList<FruitKind> BuildProportionalRoute(int apples, int pears, int berries)
    {
        var counts = new[] { apples, pears, berries };
        var used = new int[3];
        var kinds = new[] { FruitKind.XelphatolApple, FruitKind.MamookPear, FruitKind.OGhomoroBerries };
        var total = counts.Sum();
        var route = new List<FruitKind>(total);
        for (var position = 1; position <= total; position++)
        {
            var choice = Enumerable.Range(0, 3)
                .Where(index => used[index] < counts[index])
                .OrderByDescending(index => position * counts[index] / (double)total - used[index])
                .ThenBy(index => index)
                .First();
            used[choice]++;
            route.Add(kinds[choice]);
        }
        return route;
    }

    private static (int Apple, int Pear, int Berry) Counts(IReadOnlyList<FruitKind> steps) => (
        steps.Count(kind => kind == FruitKind.XelphatolApple),
        steps.Count(kind => kind == FruitKind.MamookPear),
        steps.Count(kind => kind == FruitKind.OGhomoroBerries));

    private static int MaximumRunLength(IReadOnlyList<FruitKind> steps)
    {
        var maximum = 0;
        var run = 0;
        FruitKind? previous = null;
        foreach (var step in steps)
        {
            run = previous == step ? run + 1 : 1;
            maximum = Math.Max(maximum, run);
            previous = step;
        }
        return maximum;
    }

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

    private static string ShortName(FruitKind kind) => kind switch
    {
        FruitKind.XelphatolApple => "A",
        FruitKind.MamookPear => "P",
        FruitKind.OGhomoroBerries => "B",
        FruitKind.DomanPlum => "D",
        FruitKind.Valfruit => "V",
        FruitKind.CieldalaesPineapple => "C",
        _ => "?",
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed record Candidate(RgbColor Point, long TargetDistance, double Margin, IReadOnlyList<FruitKind> Steps);

    private sealed record StochasticResult(
        string Label,
        RgbColor Endpoint,
        int AppleCount,
        int PearCount,
        int BerryCount,
        bool IndependentChannels,
        double HitRate,
        double ExpectedTargetDistanceSquared,
        double BoundaryMargin,
        double ClampTrialRate,
        IReadOnlyDictionary<string, double> Outcomes)
    {
        public string OutcomeSummary => string.Join(", ", Outcomes
            .OrderByDescending(pair => pair.Value)
            .Take(6)
            .Select(pair => $"{pair.Key} {pair.Value:P2}"));

        public override string ToString() =>
            $"{Endpoint} A/P/B {AppleCount}/{PearCount}/{BerryCount}: hit {HitRate:P2}, " +
            $"Ink {Outcomes.GetValueOrDefault("Ink Blue"):P2}, mean d2 {ExpectedTargetDistanceSquared:F1}, " +
            $"boundary {BoundaryMargin:F3}, clamp trials {ClampTrialRate:P2}";
    }
}
