# Chocobo Color Calculator

A Dalamud API 15 plugin that calculates and tracks an ordered companion-chocobo fruit route entirely in game.

## Install with Dalamud

> **Dalamud custom repository URL — copy this entire line:**

```text
https://raw.githubusercontent.com/t0nysama/ChocoboColorCalculator/main/repo.json
```

1. Launch Final Fantasy XIV through XIVLauncher and wait for Dalamud to load.
2. Enter `/xlsettings` in the in-game chat window.
3. Open the **Experimental** tab.
4. Find **Custom Plugin Repositories**, paste the URL above into an empty field, and select the **+** button.
5. Select **Save and Close**.
6. Enter `/xlplugins` in chat to open the Dalamud Plugin Installer.
7. Search for **Chocobo Color Calculator**, select it, and choose **Install**.

Open the calculator with `/chococolor`. Future releases will appear through Dalamud's normal plugin update system as long as the custom repository remains enabled.

This is a third-party custom-repository plugin and is not part of Dalamud's official plugin repository. Support for this plugin should be requested through this GitHub project rather than the XIVLauncher support channels.

## Standalone Windows desktop app

Prefer a normal Windows program? Download the self-contained desktop edition from the [latest desktop release](https://github.com/t0nysama/ChocoboColorCalculator/releases/tag/desktop-v1.2.0).

1. Download `ChocoboColorCalculator-Desktop-win-x64.zip`.
2. Extract the ZIP file to a folder of your choice.
3. Run `ChocoboColorCalculator.Desktop.exe`.

The desktop application does not require XIVLauncher, Dalamud, or a separate .NET installation. It shares the same verified calculation and export engine as the plugin and includes searchable colors, shopping totals, next-feed guidance, the complete ordered route, manual progress tracking, persistent state, PDF/Text/HTML exports, and the built-in guide. Its current version is always visible in the header and Updates tab. On each launch it performs one asynchronous check of the project's public GitHub releases; the Updates tab also supports manual checks and one-click background download, SHA-256 verification, installation, and automatic relaunch. Offline or failed checks are handled without delaying or interrupting the calculator.

Automatic feed detection is exclusive to the in-game Dalamud plugin because it depends on FFXIV's structured chat events. The desktop edition clearly uses manual tracking instead. The executable is currently unsigned, so Windows SmartScreen may ask you to confirm that you want to run it.

## What is implemented

- All 85 named companion colors and their published RGB values.
- The actual FFXIV item icons for every fruit, loaded from the local game data rather than bundled approximations.
- A single streamlined, glass-inspired interface with translucent layered panels, animated progress, gradient accents, hover transitions, ambient color effects, large previews, and one scrollable ordered route.
- The six color fruits with verified game-data item row IDs and the accepted ±5 RGB effects.
- Per-fruit 0–255 clamping, exact algebraic ordering for reachable endpoints, and a three-fruit lookahead fallback.
- A verified **closest-safe target** policy for ordinary calculations. The engine proves that the selected lattice point is the closest unclamped endpoint to the published swatch with at least 3 RGB units of true color-boundary clearance.
- A dedicated **Soot Black precision policy**: the successful 19-apple, 23-pear, 32-berry Han Lemon reset recipe is preserved and fully interleaved, while correction routes choose endpoints that remain Soot Black across the largest tested neighborhood of plausible hidden starting RGB values.
- Exact ordered feed list, total fruit counts, simulated RGB after every step, and predicted resulting named color. Reliability is reported as the true Euclidean distance to the nearest named-color boundary, rather than a less meaningful difference between center distances.
- A standard verifier checks deterministic output, exact endpoint arrival, simulation consistency, positive boundary clearance, intended named-color classification, and zero channel-clamping for all 7,225 named start/target pairs. A separate exhaustive audit classifies 2,856,817 reachable lattice points, and a Soot-specific audit verifies every named start plus 110,803 plausible hidden-start scenarios.
- Independent manual and automatic checkboxes for every step.
- Automatic progress from FFXIV's structured, client-localized chocobo-snack log message, with a rendered-chat fallback; consecutive feeds of the same fruit advance correctly, while cross-source duplicate events and unexpected fruit do not silently advance the route.
- Persistent route and progress across plugin/game restarts, undo, reset, and optional next-step chat reminders.
- One-click PDF, plain-text, and responsive HTML exports saved under `Documents\Chocobo Color Calculator\Exports`, including a visual route overview, shopping list, usage instructions, every ordered step, and RGB progression. All exported documents stay clean and status-free because live completion tracking remains inside the plugin.

## Accuracy and the unavoidable limitation

The accepted model changes the hidden RGB channels by approximately 5 per fruit, applies each fruit in order, clamps each channel to 0-255, and then maps the endpoint to the nearest named color by Euclidean RGB distance. The route engine simulates the established +/-5 model exactly, but Square Enix has never published the underlying formula and community experiments have reported shifts around 3-6. No calculator can guarantee a tight color such as Soot Black in one attempt.

FFXIV does not expose a previously recolored chocobo's exact hidden RGB values. A visible named color can therefore only supply an estimated starting point. For the most reliable first attempt, feed a **Han Lemon**, confirm **Desert Yellow**, and calculate from there. Feed exactly the ordered list: the feather-growth message indicates that a color boundary was crossed, not that an unannounced fruit failed, so do not add extra fruit solely because that message did not appear. Ink Blue, Deepwood Green, and other neighboring outcomes are treated as misses, never as "close enough"; select the actual result and calculate its optimized Soot Black correction route.

## Build

Prerequisites are the same as the current Dalamud API 15 sample plugin: .NET 10 SDK, XIVLauncher/Dalamud installed, and the default Dalamud development path (or `DALAMUD_HOME` configured).

```powershell
dotnet build .\ChocoboColorCalculator\ChocoboColorCalculator.csproj -c Release
dotnet run --project .\ChocoboColorCalculator.Verifier\ChocoboColorCalculator.Verifier.csproj -c Release
dotnet run --project .\ChocoboColorCalculator.Verifier\ChocoboColorCalculator.Verifier.csproj -c Release -- --deep-audit
dotnet run --project .\ChocoboColorCalculator.Verifier\ChocoboColorCalculator.Verifier.csproj -c Release -- --soot-audit
```

Add the resulting `ChocoboColorCalculator.dll` under `ChocoboColorCalculator/bin/x64/Release` as a Dalamud dev plugin, then open it with `/chococolor`.

To build or publish the standalone Windows application:

```powershell
dotnet build .\ChocoboColorCalculator.Desktop\ChocoboColorCalculator.Desktop.csproj -c Release
dotnet publish .\ChocoboColorCalculator.Desktop\ChocoboColorCalculator.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Desktop releases use `desktop-v*` tags and are deliberately not marked as the repository-wide latest release. This keeps Dalamud's `releases/latest/download/latest.zip` installation URL pointed only at plugin releases.

## Research basis

- Square Enix patch 2.35 notes and current UI guide for the six fruits, stable requirement, six-hour timer, Han Lemon reset, and cancellation on early removal.
- The current community color table for the 85 named RGB swatches.
- Lulu's Tools' published lookahead analysis for the lattice, clamping, nearest-color model, and the two documented Currant Purple failure pairs.
- Current Dalamud API 15 documentation and sample plugin structure.

See [RESEARCH.md](RESEARCH.md) for links, discrepancies, and implementation decisions.
