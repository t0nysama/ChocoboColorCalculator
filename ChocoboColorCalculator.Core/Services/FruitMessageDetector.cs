using ChocoboColorCalculator.Core.Data;
using ChocoboColorCalculator.Core.Models;

namespace ChocoboColorCalculator.Core.Services;

public static class FruitMessageDetector
{
    public static FruitKind? Detect(
        IEnumerable<string> textFragments,
        Func<FruitKind, string>? fruitNameProvider = null)
    {
        var text = string.Join('\n', textFragments.Where(fragment => !string.IsNullOrWhiteSpace(fragment)));
        if (text.Length == 0)
            return null;

        fruitNameProvider ??= kind => ChocoboData.Fruit(kind).Name;

        FruitKind? detected = null;
        var latestMatch = -1;
        foreach (var fruit in ChocoboData.Fruits)
        {
            var fruitName = fruitNameProvider(fruit.Kind);
            if (string.IsNullOrWhiteSpace(fruitName))
                continue;

            var match = text.LastIndexOf(fruitName, StringComparison.OrdinalIgnoreCase);
            if (match <= latestMatch)
                continue;

            detected = fruit.Kind;
            latestMatch = match;
        }

        return detected;
    }
}
