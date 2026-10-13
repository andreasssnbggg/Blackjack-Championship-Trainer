# ♠ BJC Cheat Suite

A BepInEx mod for **Blackjack Championship** (Steam) that adds a real-time card counting overlay, perfect basic strategy advisor, and full auto-play with anti-detection delays.

> **Language:** The in-game overlay supports **English** and **German**. Toggle with **F5** or via the menu.

---

## Features

| Feature | Description |
|---|---|
| **Card Counter** | Hi-Lo counting system with true count, running count, shoe progress, and player edge % |
| **Strategy Advisor** | Perfect basic strategy for 4–8 deck, S17, DAS, late surrender. Shows optimal play in real-time |
| **Bet Advisor** | Recommends bet size based on true count (1×–16× minimum, up to table max) |
| **Auto-Play** | Automatically plays perfect basic strategy with randomized delays (1–2.5s) for anti-detection |
| **Auto-Bet** | Automatically places the recommended bet with randomized delays (2–4s) |
| **Auto-Insurance** | Automatically takes/declines insurance based on true count (≥+3 = take) |
| **Speed Hack** | 5× game speed via `Time.timeScale` |
| **Bilingual UI** | Full German/English support, toggle with F5 |

## Keybinds

| Key | Action |
|---|---|
| **F1** | Toggle menu window |
| **F2** | Toggle auto-play |
| **F3** | Toggle auto-bet |
| **F4** | Toggle speed hack (5×) |
| **F5** | Toggle language (DE ↔ EN) |

## Screenshots

The overlay shows two draggable windows:

- **Strategy window** (top): Shows the current optimal play (HIT / STAND / DOUBLE / SPLIT / SURRENDER), bet recommendation during betting phase, and insurance advice
- **Menu window** (bottom): Card counter with true count, running count, shoe progress bar, player edge %, bet tips, controls, and an expandable "How does it work?" explanation

Both windows feature an animated glowing frame that shifts through emerald, teal, and gold.

---

## Installation

### Prerequisites

1. **Blackjack Championship** on Steam (App ID: 1178160)
2. **.NET SDK 6.0+** for building
3. **BepInEx 5.4.x** (32-bit, x86) — the game uses Unity 2022.3 Mono

### Install BepInEx

1. Download [BepInEx 5.4.23.2 x86](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2) (`BepInEx_win_x86_5.4.23.2.zip`)
2. Extract into your game folder:
   ```
   Blackjack Championship/
   ├── BepInEx/
   │   ├── core/
   │   ├── plugins/        ← BJCCheat.dll goes here
   │   └── ...
   ├── doorstop_config.ini
   ├── winhttp.dll
   └── Blackjack Championship.exe
   ```
3. Run the game once so BepInEx creates its config files, then close it.

### Build the Mod

1. Clone this repo
2. Open `BJCCheat.csproj` and update the `<GameDir>` path to match your Steam library:
   ```xml
   <GameDir>D:\SteamLibrary\steamapps\common\Blackjack Championship</GameDir>
   ```
3. Build:
   ```bash
   dotnet build -c Release
   ```
4. Copy `bin/Release/net472/BJCCheat.dll` to `<GameDir>/BepInEx/plugins/`
5. Launch the game!

---

## How It Works

### Card Counting (Hi-Lo)

The mod hooks into the game's `BJTable.DecodeOnPlayUpdate` and `BJTable.DecodeOnAction` methods via Harmony to read every card dealt at the table. It tracks:

- **Running Count**: 2–6 = +1, 7–9 = 0, 10/J/Q/K/A = −1
- **True Count**: Running Count ÷ decks remaining (from server shoe data)
- **Player Edge**: ~0.5% per true count point above 0

The count resets automatically on shuffle and table change.

### Strategy Tables

Full basic strategy lookup tables for:
- Hard hands (5–21 vs dealer 2–A)
- Soft hands (S13–S21 vs dealer 2–A)
- Pair splits (AA–TT vs dealer 2–A)

With proper fallbacks: Double→Hit when can't double, Surrender→Hit when can't surrender, Double-or-Stand→Stand when can't double.

### Bet Spread

| True Count | Bet Size |
|---|---|
| < +2 | Table minimum |
| +2 | 8× minimum |
| +3 | 16× minimum |
| +4+ | Table maximum |

### Anti-Detection

Auto-play uses **randomized delays** to avoid bot detection:
- Play actions (HIT/STAND/DOUBLE/SPLIT/SURRENDER): **1.0–2.5 seconds**
- Bet placement: **2.0–4.0 seconds**
- Insurance decisions: **1.0–2.5 seconds**

All timers use `Time.unscaledTime` so speed hack doesn't affect them.

### Server-Authoritative

The game is server-authoritative — chips, cards, and results are validated server-side. This mod **cannot**:
- Give you infinite chips
- Change card values
- Manipulate game outcomes

It only provides **information** (counting, strategy) and **automation** (auto-play) that a skilled player could do manually.

---

## Project Structure

```
src/
├── Plugin.cs           # BepInEx plugin entry point + IMGUI overlay
├── Lang.cs             # Bilingual string system (DE/EN)
├── Hooks.cs            # Harmony patches + auto-play/bet/insurance logic
├── CardCounter.cs      # Hi-Lo counting engine
├── StrategyAdvisor.cs  # Basic strategy lookup tables
└── MonoShutdownFix.cs  # Fixes crash-on-exit caused by MonoMod
```

| File | Purpose |
|---|---|
| **Plugin.cs** | BepInEx `[BepInPlugin]` entry point. Creates the overlay `GameObject`, applies Harmony patches, runs the `MonoShutdownFix`. Contains the full `CheatOverlay` MonoBehaviour with IMGUI rendering (two draggable windows with animated glow frames). |
| **Lang.cs** | Static localization class. `Dictionary<key, Dictionary<lang, text>>` with `Lang.T("key")` and `Lang.T("key", args)`. Supports `"de"` and `"en"`, toggled with `Lang.Toggle()`. |
| **Hooks.cs** | `ActiveTable` static class tracks the live `BJTable`, refreshes game state every update, and implements `PollAutoPlay()` / `PollAutoBet()` / `PollAutoInsurance()` with randomized delays. `TableUpdateHook` is the Harmony postfix on `DecodeOnPlayUpdate` and `DecodeOnAction`. |
| **CardCounter.cs** | Hi-Lo counting engine. Tracks running count, cards seen, shoe progress from server data, true count calculation, player edge estimate, and bet spread recommendations. Handles mid-shoe joins and deduplication of counted cards. |
| **StrategyAdvisor.cs** | Complete basic strategy lookup tables for hard, soft, and pair hands. Resolves Double/Surrender availability fallbacks. Insurance threshold at True Count +3. |
| **MonoShutdownFix.cs** | Repairs corrupted `friend_assembly_names` pointer in Mono assembly structs caused by MonoMod 22.01 (shipped with BepInEx 5.4.23). Prevents crash-on-exit in Unity 2022.3 x86. |

---

## Known Limitations

- **Hole card** is not visible — the server sends `"0u"` until the result phase, so the advisor only sees the dealer's up card
- **Side bets** are not supported by auto-play
- **Mid-shoe join**: if you sit down at a table mid-shoe, the count starts from zero and shows a warning until the next shuffle
- The game may update and break hooks — check `Assembly-CSharp.dll` for API changes

---

## Technical Notes

- **BepInEx 5 (x86)** is required — the game is 32-bit Mono
- The overlay uses Unity **IMGUI** (`OnGUI`) since it's the simplest way to draw on top of everything without touching the game's UI system
- All window textures use `HideFlags.HideAndDontSave` to survive scene loads
- The `MonoShutdownFix` uses unsafe code to patch a single `uint` pointer at a known offset — it only acts on assemblies whose layout matches the expected Unity 2022.3 Mono struct

---

## License

MIT — see [LICENSE](LICENSE)
