"""Build the release files into dist/.

- dist/PEAKMenu.dll: the plugin on its own, for people who already have BepInEx.
- dist/PEAKMenu-v<version>-with-BepInEx.zip: drag-and-drop into the PEAK folder. It holds the files of
  the official BepInEx 5.4.23.6 release (Mono, win-x64; downloaded once into build/ and checked
  against its SHA-256) plus the plugin and its licence files in BepInEx/plugins/PEAKMenu/.

Build the plugin first: dotnet build src/PEAKMenu -c Release -p:GameDir="<PEAK folder>"
"""
import hashlib
import os
import re
import shutil
import urllib.request
import zipfile

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BUILD = os.path.join(REPO, "build")
DIST = os.path.join(REPO, "dist")
DLL = os.path.join(REPO, "src", "PEAKMenu", "bin", "Release", "PEAKMenu.dll")
BRAND = os.path.join(REPO, "src", "PEAKMenu", "Brand.cs")

BEPINEX_VERSION = "5.4.23.6"
BEPINEX_URL = f"https://github.com/BepInEx/BepInEx/releases/download/v{BEPINEX_VERSION}/BepInEx_win_x64_{BEPINEX_VERSION}.zip"
BEPINEX_SHA256 = "6c57a714a892efb4e9e1fef63d608a3391f30ec984031c465404e4ee381a0755"
BEPINEX_ZIP = os.path.join(BUILD, f"BepInEx_win_x64_{BEPINEX_VERSION}.zip")

PLUGIN_DIR = "BepInEx/plugins/PEAKMenu/"
SKIP = {"changelog.txt"}
STAMP = (2026, 1, 1, 0, 0, 0)  # fixed dates, so the same input always gives the same zip


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def version():
    m = re.search(r'Version\s*=\s*"([0-9.]+)"', open(BRAND, encoding="utf-8").read())
    if not m:
        raise SystemExit("no Version in Brand.cs")
    return m.group(1)


def fetch_bepinex():
    os.makedirs(BUILD, exist_ok=True)
    if not os.path.isfile(BEPINEX_ZIP) or sha256(BEPINEX_ZIP) != BEPINEX_SHA256:
        print("downloading", BEPINEX_URL)
        urllib.request.urlretrieve(BEPINEX_URL, BEPINEX_ZIP)
    got = sha256(BEPINEX_ZIP)
    if got != BEPINEX_SHA256:
        raise SystemExit(f"BepInEx zip hash mismatch: {got} (expected {BEPINEX_SHA256})")


def add(out, name, data):
    info = zipfile.ZipInfo(name, STAMP)
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = 0o644 << 16
    out.writestr(info, data)


def main():
    if not os.path.isfile(DLL):
        raise SystemExit(f"{DLL} is missing. Build the plugin first.")
    fetch_bepinex()
    ver = version()
    os.makedirs(DIST, exist_ok=True)

    plugin_dll = os.path.join(DIST, "PEAKMenu.dll")
    shutil.copyfile(DLL, plugin_dll)

    docs = {
        PLUGIN_DIR + "README.txt": os.path.join(REPO, "tools", "PLUGIN-README.txt"),
        PLUGIN_DIR + "LICENSE.txt": os.path.join(REPO, "LICENSE"),
        PLUGIN_DIR + "THIRD-PARTY-NOTICES.md": os.path.join(REPO, "THIRD-PARTY-NOTICES.md"),
        PLUGIN_DIR + "BepInEx-LICENSE.txt": os.path.join(REPO, "third_party", "BepInEx-LICENSE.txt"),
        PLUGIN_DIR + "UnityDoorstop-LICENSE.txt": os.path.join(REPO, "third_party", "UnityDoorstop-LICENSE.txt"),
    }
    for src in docs.values():
        if not os.path.isfile(src):
            raise SystemExit(f"missing {src}")

    zip_path = os.path.join(DIST, f"PEAKMenu-v{ver}-with-BepInEx.zip")
    with zipfile.ZipFile(BEPINEX_ZIP) as src, zipfile.ZipFile(zip_path, "w") as out:
        for info in sorted(src.infolist(), key=lambda i: i.filename):
            if info.is_dir() or info.filename in SKIP:
                continue
            add(out, info.filename, src.read(info))
        add(out, PLUGIN_DIR + "PEAKMenu.dll", open(DLL, "rb").read())
        for rel, full in sorted(docs.items()):
            add(out, rel, open(full, "rb").read())

    for path in (plugin_dll, zip_path):
        print(f"{os.path.basename(path)}  {os.path.getsize(path)} bytes  sha256 {sha256(path)}")
    with zipfile.ZipFile(zip_path) as z:
        for name in z.namelist():
            print("   ", name)


if __name__ == "__main__":
    main()
