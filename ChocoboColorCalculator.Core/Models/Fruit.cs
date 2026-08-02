namespace ChocoboColorCalculator.Core.Models;

public enum FruitKind
{
    XelphatolApple,
    MamookPear,
    OGhomoroBerries,
    DomanPlum,
    Valfruit,
    CieldalaesPineapple,
}

public sealed record Fruit(
    FruitKind Kind,
    string Name,
    uint ItemId,
    RgbColor Delta)
{
    public RgbColor Apply(RgbColor color) => new RgbColor(
        color.R + Delta.R,
        color.G + Delta.G,
        color.B + Delta.B).Clamp();

    public bool WouldClamp(RgbColor color)
    {
        var raw = new RgbColor(color.R + Delta.R, color.G + Delta.G, color.B + Delta.B);
        return raw.R is < 0 or > 255 || raw.G is < 0 or > 255 || raw.B is < 0 or > 255;
    }
}
