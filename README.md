# Chocobo Color Calculator

A Dalamud API 15 plugin that calculates and tracks an ordered companion-chocobo fruit route entirely in game.

## What is implemented

- All 85 named companion colors and their published RGB values.
- The actual FFXIV item icons for every fruit, loaded from the local game data rather than bundled approximations.
- A single streamlined, glass-inspired interface with translucent layered panels, animated progress, gradient accents, hover transitions, ambient color effects, large previews, and one scrollable ordered route.
- The six color fruits with verified game-data item row IDs and the accepted ±5 RGB effects.
- Per-fruit 0–255 clamping, exact algebraic ordering for reachable endpoints, and a three-fruit lookahead fallback.
- One enforced **Reliable target** policy for every calculation. It prefers the reachable point closest to the published swatch, then falls back to a deeper point when the closest route is too near a neighboring color or cannot reach its selected endpoint. This preserves known close-color fixes while matching the established 19-apple, 23-pear, 32-berry Desert Yellow to Soot Black recipe.
- Exact ordered feed list, total fruit counts, simulated RGB after every step, and predicted resulting named color. The verifier confirms deterministic output, exact endpoint arrival, simulation consistency, and a positive classification margin for all 7,225 named start/target pairs.
- Independent manual and automatic checkboxes for every step.
- Automatic progress from FFXIV's structured, client-localized chocobo-snack log message, with a rendered-chat fallback; unexpected fruit is detected and does not silently advance the route.
- Persistent route and progress across plugin/game restarts, undo, reset, and optional next-step chat reminders.

## Accuracy and the unavoidable limitation

The accepted model changes the hidden RGB channels by approximately 5 per fruit, applies each fruit in order, clamps each channel to 0–255, and then maps the endpoint to the nearest named color by Euclidean RGB distance. The route engine simulates the established ±5 model exactly, but Square Enix has never published the underlying formula and community experiments have reported per-channel shifts around 3–6. No calculator can guarantee a tight color such as Soot Black in one attempt.

FFXIV does not expose a previously recolored chocobo's exact hidden RGB values. A visible named color can therefore only supply an estimated starting point. For the most reliable first attempt, feed a **Han Lemon**, confirm **Desert Yellow**, and calculate from there. Feed exactly the ordered list: the feather-growth message indicates that a color boundary was crossed, not that an unannounced fruit failed, so do not add extra fruit solely because that message did not appear. If a narrow target misses, select the resulting visible color and calculate a short correction instead of resetting again.

## Build

Prerequisites are the same as the current Dalamud API 15 sample plugin: .NET 10 SDK, XIVLauncher/Dalamud installed, and the default Dalamud development path (or `DALAMUD_HOME` configured).

```powershell
dotnet build .\ChocoboColorCalculator\ChocoboColorCalculator.csproj -c Release
dotnet run --project .\ChocoboColorCalculator.Verifier\ChocoboColorCalculator.Verifier.csproj -c Release
```

Add the resulting `ChocoboColorCalculator.dll` under `ChocoboColorCalculator/bin/x64/Release` as a Dalamud dev plugin, then open it with `/chococolor`.

## Research basis

- Square Enix patch 2.35 notes and current UI guide for the six fruits, stable requirement, six-hour timer, Han Lemon reset, and cancellation on early removal.
- The current community color table for the 85 named RGB swatches.
- Lulu's Tools' published lookahead analysis for the lattice, clamping, nearest-color model, and the two documented Currant Purple failure pairs.
- Current Dalamud API 15 documentation and sample plugin structure.

See [RESEARCH.md](RESEARCH.md) for links, discrepancies, and implementation decisions.
