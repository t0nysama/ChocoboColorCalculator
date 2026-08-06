# Research and calculation notes

Checked 2026-08-05.

## Sources

- [Official patch 2.35 notes](https://na.finalfantasyxiv.com/lodestone/topics/detail/2952cde08127ad3911220b2b5744330af2f11d85): introduced the stable-feeding system, six fruits, six-hour Earth-time wait, Han Lemon reset, and early-removal cancellation.
- [Official UI guide](https://na.finalfantasyxiv.com/uiguide/faq/faq-chocobo/chocobo_color.html): current confirmation of fruit effects and stable behavior.
- [Lulu's Tools algorithm explanation](https://ffxiv.pf-n.co/chocobo-color/about): ±5 vectors, channel clamping, Euclidean nearest-color assumption, lookahead-3 reasoning, lattice error, and documented Honey Yellow/Celeste Green → Currant Purple failures.
- [FFXIV Chocobo Colour Calculator: Desert Yellow to Soot Black](https://ffxivchocobo.com/en/desert-yellow/soot-black): established 19-apple, 23-pear, 32-berry totals and ordered feeding route.
- [Chocobo Dye: The Missing Manual](https://forum.square-enix.com/ffxiv/threads/189066): community experiment summary reporting approximate 3–6 point shifts and explaining that the feather-growth message indicates crossing a color window rather than whether a fruit was applied.
- [Mecrofeeding: The Way to Chocobo Color Dyeing](https://forum.square-enix.com/ffxiv/threads/191271-Mecrofeeding-The-Way-to-Chocobo-Color-Dyeing): early controlled Soot Black attempts and correction results showing that a mathematically plausible first route could resolve to a neighboring named color.
- [Chocobo Colour Calculator experiment](https://na.finalfantasyxiv.com/lodestone/character/2168293/blog/1568406) and [follow-up](https://na.finalfantasyxiv.com/lodestone/character/2168293/blog/1586483): player-recorded Soot Black corrections that moved among Ink Blue, Deepwood Green, Kobold Brown, and other nearby colors.
- [Community chocobo color table](https://ffxiv.consolegameswiki.com/wiki/Chocobo_Colors): the full named RGB palette and fruit vectors.
- [Current Dalamud getting-started guide](https://dalamud.dev/plugin-development/getting-started/), [IChatGui API](https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IChatGui/), and [API 15 notes](https://dalamud.dev/versions/v15/): project SDK and current structured log-message interfaces.
- [XIVAPI v2 game data](https://v2.xivapi.com/): item rows 8157–8162 were checked for the six fruits.

## Verified fruit data

| Fruit | Item row | ΔR | ΔG | ΔB |
|---|---:|---:|---:|---:|
| Xelphatol Apple | 8157 | +5 | -5 | -5 |
| Doman Plum | 8158 | -5 | +5 | +5 |
| Mamook Pear | 8159 | -5 | +5 | -5 |
| Valfruit | 8160 | +5 | -5 | +5 |
| O'Ghomoro Berries | 8161 | -5 | -5 | +5 |
| Cieldalaes Pineapple | 8162 | +5 | +5 | -5 |

Han Lemon is row 8163 and resets to Desert Yellow; it is intentionally not a route step because it is a reset, not a ±5 vector.

## Data discrepancy

References disagree on Desert Yellow: the community page prose has used 216/180/87 (and historically 216/175/80), while its calculator table and the established calculator palette use 219/180/87. This implementation uses **219/180/87**, matching the published named-color table used for nearest-color classification. The plugin clearly recommends a Han Lemon baseline because the game does not expose the hidden live RGB value.

## Solver choices

1. Apply each fruit immediately and clamp every channel to `[0,255]`; fruit order is therefore retained.
2. For every lattice-reachable endpoint, construct an exact algebraic fruit route first and order its fruit to avoid clamping. Retain lookahead 3 only as a fallback when direct ordering cannot reach the endpoint exactly.
3. Apply the closest-safe target policy to ordinary calculations. A candidate must be reachable without clamping, classify as the intended named color, and retain at least 3 RGB units of true Euclidean clearance from every named-color boundary. The 3-unit floor preserves the user-verified Desert Yellow to Soot Black endpoint (3.03 units) while eliminating weaker 2.55-unit endpoints produced by the former center-distance rule. The search expands until it finds a qualifying candidate, then searches the exact distance bound around that candidate.
4. If several qualifying points are equally close to the published swatch, prefer the point with the greatest true Voronoi boundary clearance, then the fewest fruit, then a stable RGB ordering. This makes tied choices deterministic and more tolerant of hidden-value error without moving farther from the swatch.
5. Report reliability as the true signed Euclidean distance from the selected endpoint to the nearest named-color Voronoi boundary. The earlier center-distance difference was adequate as a threshold but was not a geometric boundary distance.
6. Simulate the final ordered route again and report both endpoint and nearest named color. The standard verifier checks all 7,225 named start/target pairs for deterministic output, exact aim-point arrival, simulation consistency, positive boundary clearance, intended classification, and zero channel clamping.
7. The deep verifier independently enumerates 2,856,817 unclamped lattice-reachable RGB points across all 85 named starting colors. It proves that every ordinary non-self route selects the globally closest unclamped endpoint satisfying the 3-unit boundary rule. Soot Black corrections intentionally use the robustness policy described below.

For 103 ordinary non-self routes (including the established Desert Yellow to Soot Black reset route), the mathematically closest lattice point to the published swatch has less than 3 units of boundary clearance, so the solver deliberately uses the next closest qualifying point. Soot Black corrections from other named colors use the separate hidden-start robustness policy. The weakest selected endpoint has 3.003 units of true clearance; Desert Yellow to Soot Black has 3.032.

Intentional channel clamping can sometimes reduce the number of fruit in a theoretical fixed-step route, but a clamped fruit has only a partial channel effect and makes an already unpublished, variable process more order-sensitive. Because this project optimizes result accuracy rather than minimum fruit count, the production solver rejects clamped routes. The verifier confirms that none of the 7,225 generated routes hits an RGB wall.

The nearest-color rule is a well-supported community model, not an officially published Square Enix formula. The UI describes that honestly.

## Soot Black correction

The earlier Safe center policy maximized only the theoretical nearest-color margin. From a Han Lemon reset it selected RGB `44/35/42`, producing 19 Xelphatol apples, 22 Mamook pears, and 32 O'Ghomoro berries. That moved farther from the published Soot Black swatch and disagreed with the established recipe.

Closest-safe target selects reachable RGB `39/40/37`, producing 19 apples, 23 pears, and 32 berries. This endpoint is close to the published Soot Black swatch (`43/41/35`), retains 3.03 RGB units of true boundary clearance, and reproduces the long-established recipe. Because the game's exact hidden values and per-fruit variance are not published, this is a material accuracy improvement rather than a one-attempt guarantee.

Reports of this exact target resolving to Ink Blue, Deepwood Green, Kobold Brown, Marsh Green, and other neighbors were treated as named failures, not acceptable RGB proximity. The reset recipe remains 19/23/32 because it is both the established community route and has a verified successful result; simulation-only alternatives such as 19/22/32 were not substituted for that empirical evidence. The same counts are now distributed in a fully interleaved order, eliminating the former nine-berry opening run while retaining the identical unclamped endpoint.

For a correction from any non-Desert-Yellow named result, the solver now accounts for the fact that the visible color hides its exact RGB. It evaluates reachable Soot Black endpoints against every still-matching start-color offset in a +/-5 RGB neighborhood, requires at least 3 units of boundary clearance, and chooses the endpoint with the greatest named Soot Black coverage before considering swatch distance, margin, and fruit count. The focused audit verified all 85 named starts and 110,803 hidden-start route simulations with zero channel clamping. Mean local hidden-start coverage increased from 90.7% to 95.8%; only the deliberately preserved Desert Yellow reset recipe has a tested local-coverage alternative more than five percentage points higher.

## What cannot be guaranteed

Square Enix documents the direction of each fruit, the six-hour stable cycle, and the Han Lemon reset, but does not publish the hidden RGB arithmetic or a live RGB value. Community experiments support the +/-5 planning model while also reporting per-feed variation. A non-Desert-Yellow named starting color can represent many hidden RGB points inside the same named-color region. Consequently, no route planner can truthfully guarantee every first attempt. This project maximizes deterministic accuracy under the best-supported model, recommends a Han Lemon baseline, never treats the feather-growth message as a failed-fruit signal, and supports short corrections from the actual resulting named color.

## Automatic feed detection

Automatic tracking listens for FFXIV `LogMessage` row 4495 (the companion chocobo "devours" message) through Dalamud's structured `IChatGui.LogMessage` event. It reads the client-localized string parameters to identify the consumed fruit. A corrected log-kind 57 chat listener is retained as a rendered-message fallback. The debounce suppresses only a matching cross-source duplicate, so two legitimate consecutive feeds of the same fruit each advance exactly once.
