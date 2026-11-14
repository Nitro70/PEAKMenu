# PEAKMenu

A mod menu for **PEAK** (BepInEx 5), by Nitro70.

> **Status: pre-release, not tested in game yet.** It builds against PEAK 3.1, and every game class, method and network message it uses was checked against the game's code, but nobody has played with it yet. Expect rough edges and please open an issue if something doesn't work.

Press **Insert** in game to open it (you can change the key in Settings). The window has a sidebar of tabs, a search box over every card, favourites (click the star), key binds (right-click a card) and a CLIENT or HOST badge on each card. If a game update breaks a card, that card switches itself off and says so; the rest keeps working.

## Install

### Easiest: one zip (includes BepInEx)

1. Download `PEAKMenu-vX.Y.Z-with-BepInEx.zip` from [Releases](../../releases).
2. Open your PEAK folder: in Steam, right-click PEAK, then **Manage > Browse local files**.
3. Open the zip and drag everything in it into that folder (next to `PEAK.exe`). Say yes if Windows asks to merge the `BepInEx` folder.
4. Start PEAK through Steam and press **Insert**.

The zip holds `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` and a `BepInEx` folder with BepInEx 5.4.23.6 and the menu in `BepInEx/plugins/PEAKMenu/`.

### If you already have BepInEx

Download just `PEAKMenu.dll` from [Releases](../../releases) and put it in `BepInEx/plugins/` (or `BepInEx/plugins/PEAKMenu/`).

Dependencies, if you'd rather get them yourself:
- **BepInEx 5** (Mono, Windows x64): [BepInEx 5.4.23.6 on GitHub](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.6) (`BepInEx_win_x64_5.4.23.6.zip`), or the PEAK pack on Thunderstore: [BepInExPack_PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/).
- Nothing else. The menu draws its own window, so it does not need DearImGuiInjection or any other library.

If you also run another menu that opens with Insert (for example PEAK AIO), remove it or change one of the keys.

## What's in it

| Tab | What it does |
|---|---|
| **Self** | Infinite stamina, god mode, freeze statuses, no weight, never hungry, no cold or heat, no fall damage, never fall over, never pass out, auto revive, heal fully, revive yourself, pass out, turn into a zombie, set any status exactly |
| **Movement** | Fly, walk speed, sprint boost, super jump, fast climbing (rock, vines, ropes) |
| **Teleport** | To your ping, where you look, 25 m up, back to the start, to coordinates, saved spots |
| **ESP** | Scouts (health, passed out, dead), items, food, special items, closed luggage and what can come out, campfires, revive statues and effigies, the Scoutmaster, zombies, spiders, beetles and other mobs, beehives. Optional boxes, lines and distance |
| **Visuals** | Field of view, no fog, fullbright, always daytime (just for you), hide the HUD, the game's own free camera |
| **Players** | Pick a scout. Help: revive, bring to me, teleport to them, wake up, free. Troll: knock over, launch, fling, rocket, big jump, pass out, zombify, pin in place, throw their held item, carry them, explode, banana slip, play dead, backflip, force crouch, Scoutmaster hunts or throws them, give any status. Repeating versions (keep knocking over, keep launching, rocket spam, float) and everyone-at-once versions |
| **Items** | Spawn any item (in your hands, at your feet, where you look, on a picked scout), quick kits, refill what you hold, infinite item uses, long-range rescue claw (range slider up to 1000 m) |
| **World** | Open all or nearby luggage, teleport to the campfire or the summit, light the campfire (moves the run to the next area), put out every campfire, anger every bee swarm, win the run, end the run. As host: jump to an area, time of day, stop the fog, spawn the Scoutmaster, lobby size |
| **Unlocks** | All cosmetics for the session (nothing saved), every badge, ascent levels. Badges and ascents are written to your Steam account as real achievements and stats, so those buttons ask for a second click |

## Guest or host

- **CLIENT** cards work in any lobby, including someone else's. They either change only you and your screen, or send a message that PEAK accepts from every player.
- **HOST** cards lock with a note when you're a guest.

PEAK 3.x only accepts killing or teleporting another player from the host. So as a guest:
- "Bring to me" uses the game's revive-and-move. The scout arrives healed but drops what they carry.
- "Down" a scout by giving them full injury.

Friends don't need the mod. Use the trolling on people who are in on the joke.

## Build

```
dotnet build src/PEAKMenu -c Release -p:GameDir="<your PEAK folder>"
```

`GameDir` defaults to `C:\Program Files (x86)\Steam\steamapps\common\PEAK`. The game's own DLLs are referenced from there (publicized at build time) and never copied into this repository. The output is `src/PEAKMenu/bin/Release/PEAKMenu.dll`. `python tools/make_release.py` builds the two release files into `dist/`.

## License

MIT, see [LICENSE](LICENSE). The release zip also carries BepInEx (MIT) and Unity Doorstop (LGPL-2.1); see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). PEAKMenu is a fan-made mod and is not affiliated with Aggro Crab, Landfall or the BepInEx project.
