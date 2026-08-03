# Research and calculation notes

Checked 2026-08-02.

## Sources

- [Official patch 2.35 notes](https://na.finalfantasyxiv.com/lodestone/topics/detail/2952cde08127ad3911220b2b5744330af2f11d85): introduced the stable-feeding system, six fruits, six-hour Earth-time wait, Han Lemon reset, and early-removal cancellation.
- [Official UI guide](https://eu.finalfantasyxiv.com/uiguide/faq/faq-chocobo/chocobo_color.html): current confirmation of fruit effects and stable behavior.
- [Lulu's Tools algorithm explanation](https://ffxiv.pf-n.co/chocobo-color/about): ±5 vectors, channel clamping, Euclidean nearest-color assumption, lookahead-3 reasoning, lattice error, and documented Honey Yellow/Celeste Green → Currant Purple failures.
- [FFXIV Chocobo Colour Calculator: Desert Yellow to Soot Black](https://ffxivchocobo.com/en/desert-yellow/soot-black): established 19-apple, 23-pear, 32-berry totals and ordered feeding route.
- [Chocobo Dye: The Missing Manual](https://forum.square-enix.com/ffxiv/threads/189066): community experiment summary reporting approximate 3–6 point shifts and explaining that the feather-growth message indicates crossing a color window rather than whether a fruit was applied.
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
3. Apply one Reliable target policy to every calculation: find both the reachable endpoint closest to the published target swatch and the endpoint with the largest classification margin. Use the closest endpoint when it retains at least 5 RGB-distance units of margin and the ordered solver reaches it exactly; otherwise use the deeper endpoint. There is no alternate calculation mode that can bypass this protection.
4. Simulate the final ordered route again and report both endpoint and nearest named color. The verifier checks all 7,225 named start/target pairs for deterministic output, exact aim-point arrival, simulation consistency, positive classification margin, and the intended named color.

The nearest-color rule is a well-supported community model, not an officially published Square Enix formula. The UI describes that honestly.

## Soot Black correction

The earlier Safe center policy maximized only the theoretical nearest-color margin. From a Han Lemon reset it selected RGB `44/35/42`, producing 19 Xelphatol apples, 22 Mamook pears, and 32 O'Ghomoro berries. That moved farther from the published Soot Black swatch and disagreed with the established recipe.

Reliable target selects reachable RGB `39/40/37`, producing 19 apples, 23 pears, and 32 berries. This endpoint is close to the published Soot Black swatch (`43/41/35`), retains a positive 5.47 classification margin, and reproduces the long-established recipe. Because the game's exact hidden values and per-fruit variance are not published, this is a material accuracy improvement rather than a one-attempt guarantee.

## Automatic feed detection

Automatic tracking listens for FFXIV `LogMessage` row 4495 (the companion chocobo "devours" message) through Dalamud's structured `IChatGui.LogMessage` event. It reads the client-localized string parameters to identify the consumed fruit. A corrected log-kind 57 chat listener is retained as a rendered-message fallback, and a short debounce prevents the two callbacks from advancing the route twice.
