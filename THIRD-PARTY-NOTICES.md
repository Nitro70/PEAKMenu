# Third-party notices

PEAKMenu is built on and distributed with the following third-party software. Each is the property
of its own authors and keeps its own license. The `PEAKMenu.dll` download on its own contains none of
it; the `-with-BepInEx` release zip contains the unmodified files of the official BepInEx 5.4.23.6
release (Mono, Windows x64), so the mod loads without installing anything else.

## BepInEx

- Project: https://github.com/BepInEx/BepInEx
- Version: 5.4.23.6
- License: MIT (Copyright (c) 2018 Bepis). Text: `third_party/BepInEx-LICENSE.txt` in the source
  repository and `BepInEx/plugins/PEAKMenu/BepInEx-LICENSE.txt` in the release zip.
- The `BepInEx/core` folder also contains HarmonyX, MonoMod and Mono.Cecil, all under the MIT License.

## Unity Doorstop

The loader that starts BepInEx: `winhttp.dll`, `doorstop_config.ini` and `.doorstop_version` in the
release zip, exactly as shipped in the BepInEx release.

- Project and source code: https://github.com/NeighTools/UnityDoorstop
- Version: 4.6.0
- License: GNU Lesser General Public License v2.1. Text: `third_party/UnityDoorstop-LICENSE.txt` in
  the source repository and `BepInEx/plugins/PEAKMenu/UnityDoorstop-LICENSE.txt` in the release zip.

PEAKMenu is not affiliated with or endorsed by the BepInEx or Unity Doorstop projects.

## PEAK

PEAKMenu is an unofficial, fan-made modification for PEAK (by Aggro Crab and Landfall). It is not
affiliated with or endorsed by them. PEAK and its assets are the property of their respective owners.
