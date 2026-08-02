namespace ChocoboColorCalculator.Core.Models;

public readonly record struct RgbColor(int R, int G, int B)
{
    public RgbColor Clamp() => new(
        Math.Clamp(R, 0, 255),
        Math.Clamp(G, 0, 255),
        Math.Clamp(B, 0, 255));

    public long DistanceSquared(RgbColor other)
    {
        var dr = R - other.R;
        var dg = G - other.G;
        var db = B - other.B;
        return (long)dr * dr + (long)dg * dg + (long)db * db;
    }

    public string Hex => $"#{R:X2}{G:X2}{B:X2}";
}
