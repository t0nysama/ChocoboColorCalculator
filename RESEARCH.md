# Research and calculation notes

Checked 2026-08-02.

## Sources

- [Official patch 2.35 notes](https://na.finalfantasyxiv.com/lodestone/topics/detail/2952cde08127ad3911220b2b5744330af2f11d85): introduced the stable-feeding system, six fruits, six-hour Earth-time wait, Han Lemon reset, and early-removal cancellation.
- [Official UI guide](https://eu.finalfantasyxiv.com/uiguide/faq/faq-chocobo/chocobo_color.html): current confirmation of fruit effects and stable behavior.
- [Lulu's Tools algorithm explanation](https://ffxiv.pf-n.co/chocobo-color/about): ±5 vectors, channel clamping, Euclidean nearest-color assumption, lookahead-3 reasoning, lattice error, and documented Honey Yellow/Celeste Green → Currant Purple failures.
- [Community chocobo color table](https://ffxiv.consolegameswiki.com/wiki/Chocobo_Colors): the full named RGB palette and fruit vectors.
- [Current Dalamud getting-started guide](https://dalamud.dev/plugin-development/getting-started/) and [API 15 notes](https://dalamud.dev/versions/v15/): project SDK and current chat-event interfaces.
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
2. Use lookahead 3 so the solver can take a temporary non-improving step when a three-fruit combination improves all channels together.
3. In Safe center mode, search nearby lattice-reachable endpoints classified as the desired named color and maximize the distance margin over the closest competitor. Route cost breaks ties.
4. Simulate the final ordered route again and report both endpoint and nearest named color. The verifier checks all 7,225 named start/target pairs.

The nearest-color rule is a well-supported community model, not an officially published Square Enix formula. The UI describes that honestly.
