# ♠ Blackjack Championship Trainer

[![Release](https://img.shields.io/github/v/release/andreasssnbggg/Blackjack-Championship-Trainer?label=release)](https://github.com/andreasssnbggg/Blackjack-Championship-Trainer/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/andreasssnbggg/Blackjack-Championship-Trainer/total)](https://github.com/andreasssnbggg/Blackjack-Championship-Trainer/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![BepInEx 5.4 x86](https://img.shields.io/badge/BepInEx-5.4.x%20x86-blue)](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2)
[![Unity 2022.3 Mono](https://img.shields.io/badge/Unity-2022.3%20Mono-lightgrey)](#technical-notes)

A BepInEx mod for **Blackjack Championship** (Steam) that adds a real-time **Hi-Lo card counter**, a **perfect basic strategy advisor** and optional **auto-play**, all in an in-game overlay (plugin name: *BJC Cheat Suite*).

> 🇬🇧 / 🇩🇪 The overlay supports **English** and **German**. Toggle with **F5** or via the menu.

<p align="center"><img src="docs/overlay.png" alt="In-game overlay (German UI)" width="360"></p>

> [!WARNING]
> **Use at your own risk.** Blackjack Championship is an online, server-authoritative game. Automation tools very likely violate the game's Terms of Service and **can get your account banned**. This project is not affiliated with or endorsed by the game's developers. It is provided for educational purposes. You are responsible for how you use it.

---

## Quick Install

1. Install **[BepInEx 5.4.23.2 x86](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2)** (`BepInEx_win_x86_5.4.23.2.zip`) into your game folder and start the game once ([details](#install-bepinex)).
2. Download **`BJCCheat.dll`** from the **[latest release](https://github.com/andreasssnbggg/Blackjack-Championship-Trainer/releases/latest)**.
3. Copy it to `Blackjack Championship/BepInEx/plugins/`.
4. Start the game and press **F1**.

| | |
|---|---|
| **Tested with** | Blackjack Championship (Steam App ID 1178160), October 2026 build |
| **Mod version** | 1.1.0 |
| **Requires** | BepInEx 5.4.x **32-bit (x86)** |

---

## Features

| Feature | Description |
|---|---|
| **Card Counter** | Hi-Lo counting system with true count, running count, shoe progress, and player edge % |
| **Strategy Advisor** | Perfect basic strategy for 4–8 deck, S17, DAS, late surrender. Shows optimal play in real-time |
| **Bet Advisor** | Recommends bet size based on true count (1×–16× minimum, up to table max) |
| **Insurance Advice** | Take/decline insurance based on true count (≥ +3 = take) |
| **Auto-Play / Auto-Bet** | Optional: plays basic strategy and places the recommended bet automatically |
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

## The Overlay

The overlay shows two draggable windows:

- **Strategy window** (top): Shows the current optimal play (HIT / STAND / DOUBLE / SPLIT / SURRENDER), bet recommendation during betting phase, and insurance advice
- **Menu window** (bottom): Card counter with true count, running count, shoe progress bar, player edge %, bet tips, controls, and an expandable "How does it work?" explanation

Both windows feature an animated glowing frame that shifts through emerald, teal, and gold.

---

## Installation (detailed)

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

### Build from Source

Only needed if you don't want to use the prebuilt DLL from the releases.

Requires the **.NET SDK 6.0+**.

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

### Server-Authoritative

The game is server-authoritative: chips, cards, and results are validated server-side. This mod **cannot**:
- Give you infinite chips
- Change card values
- Manipulate game outcomes

It only provides **information** (counting, strategy) and optional **automation** of the plays a skilled player would make manually.

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
| **Hooks.cs** | `ActiveTable` static class tracks the live `BJTable`, refreshes game state every update, and implements `PollAutoPlay()` / `PollAutoBet()` / `PollAutoInsurance()`. `TableUpdateHook` is the Harmony postfix on `DecodeOnPlayUpdate` and `DecodeOnAction`. |
| **CardCounter.cs** | Hi-Lo counting engine. Tracks running count, cards seen, shoe progress from server data, true count calculation, player edge estimate, and bet spread recommendations. Handles mid-shoe joins and deduplication of counted cards. |
| **StrategyAdvisor.cs** | Complete basic strategy lookup tables for hard, soft, and pair hands. Resolves Double/Surrender availability fallbacks. Insurance threshold at True Count +3. |
| **MonoShutdownFix.cs** | Repairs corrupted `friend_assembly_names` pointer in Mono assembly structs caused by MonoMod 22.01 (shipped with BepInEx 5.4.23). Prevents crash-on-exit in Unity 2022.3 x86. |

---

## Known Limitations

- **Hole card** is not visible: the server sends `"0u"` until the result phase, so the advisor only sees the dealer's up card
- **Side bets** are not supported by auto-play
- **Mid-shoe join**: if you sit down at a table mid-shoe, the count starts from zero and shows a warning until the next shuffle
- The game may update and break hooks. Check `Assembly-CSharp.dll` for API changes

## Troubleshooting

| Problem | Fix |
|---|---|
| No overlay, nothing happens | Make sure you installed the **x86** BepInEx build, not x64. Check `BepInEx/LogOutput.log` for errors. |
| `BepInEx` folder has no `plugins` / `config` | Start the game once after installing BepInEx. |
| Overlay appears but count stays 0 | The game probably updated and the hooks no longer match. Open an issue with your `LogOutput.log`. |
| Game crashes on exit | Should be fixed by `MonoShutdownFix`. If not, open an issue with your BepInEx and game version. |

---

## Technical Notes

- **BepInEx 5 (x86)** is required because the game is 32-bit Mono
- The overlay uses Unity **IMGUI** (`OnGUI`) since it's the simplest way to draw on top of everything without touching the game's UI system
- All window textures use `HideFlags.HideAndDontSave` to survive scene loads
- The `MonoShutdownFix` uses unsafe code to patch a single `uint` pointer at a known offset. It only acts on assemblies whose layout matches the expected Unity 2022.3 Mono struct

---

## License

MIT, see [LICENSE](LICENSE). Not affiliated with the developers or publishers of Blackjack Championship.
