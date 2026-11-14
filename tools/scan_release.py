"""Scan the release files (the DLL and every file inside the zip) for personal or build-machine strings.

Run before publishing. Prints each hit with the bytes around it; exit code 1 if anything is found.
The repo's own name is not searched: it is the product name and appears in the DLL on purpose.
"""
import getpass
import glob
import os
import sys
import zipfile

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIST = os.path.join(REPO, "dist")
B = chr(92)

NEEDLES = [getpass.getuser(), os.path.expanduser("~"), REPO, B + "obj" + B, ".pdb", "SteamLibrary",
           "AppData", "Temp" + B, "7656119", "claude", "anthropic"] + sys.argv[1:]


def hits(data, label):
    found = 0
    low = data.lower()
    for n in NEEDLES:
        for enc in ("ascii", "utf-16le"):
            try:
                b = n.lower().encode(enc)
            except UnicodeEncodeError:
                continue
            i = low.find(b)
            if i != -1:
                found += 1
                print(f"HIT in {label}: {n!r} ({enc}) ...{data[max(0, i - 40):i + 60]!r}")
    return found


def main():
    total = 0
    dll = os.path.join(DIST, "PEAKMenu.dll")
    total += hits(open(dll, "rb").read(), "PEAKMenu.dll")
    zips = glob.glob(os.path.join(DIST, "PEAKMenu-v*-with-BepInEx.zip"))
    if not zips:
        raise SystemExit("no release zip in dist/")
    # files byte-identical to the official BepInEx release are theirs, untouched: their DLLs carry
    # BepInEx's, Cecil's and MonoMod's own build-server paths, which are not ours to scrub
    official = {}
    bep = os.path.join(REPO, "build", "BepInEx_win_x64_5.4.23.6.zip")
    with zipfile.ZipFile(bep) as b:
        for info in b.infolist():
            if not info.is_dir():
                official[info.filename] = b.read(info)
    skipped = 0
    for zp in zips:
        with zipfile.ZipFile(zp) as z:
            for name in z.namelist():
                data = z.read(name)
                if official.get(name) == data:
                    skipped += 1
                    continue
                total += hits(data, os.path.basename(zp) + "/" + name)
    print(f"skipped {skipped} files identical to the official BepInEx 5.4.23.6 release")
    # positive control: the scanner must find the product name, or it is not reading the files
    data = open(dll, "rb").read().lower()
    if b"peakmenu" not in data:
        raise SystemExit("positive control failed: 'peakmenu' not found in the DLL")
    print(f"{total} hits")
    return 1 if total else 0


if __name__ == "__main__":
    raise SystemExit(main())
