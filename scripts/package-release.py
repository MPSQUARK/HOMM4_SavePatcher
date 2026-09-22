#!/usr/bin/env python3
"""Package dotnet publish output into GitHub release artifacts."""

from __future__ import annotations

import io
import os
import stat
import tarfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RELEASE = ROOT / "release"
VERSION = "1.0.0"
PACKAGE = "mark-of-tiger-patcher"


def ar_member(name: str, data: bytes) -> bytes:
    header = (
        f"{name:<16}"
        f"{int(os.environ.get('SOURCE_DATE_EPOCH', '0')):<12}"
        f"0     0     100644  "
        f"{len(data):<10}"
        f"`\n"
    ).encode("ascii")
    pad = b"\n" if len(data) % 2 else b""
    return header + data + pad


def build_deb(deb_path: Path, binary_path: Path, control: str) -> None:
    control_tar = io.BytesIO()
    with tarfile.open(fileobj=control_tar, mode="w:gz") as tar:
        info = tarfile.TarInfo(name="./control")
        payload = control.encode("utf-8")
        info.size = len(payload)
        tar.addfile(info, io.BytesIO(payload))

    data_tar = io.BytesIO()
    with tarfile.open(fileobj=data_tar, mode="w:gz") as tar:
        info = tarfile.TarInfo(name="./usr/local/bin/MarkOfTigerPatcher")
        info.mode = 0o755
        info.size = binary_path.stat().st_size
        with binary_path.open("rb") as src:
            tar.addfile(info, src)

    with deb_path.open("wb") as out:
        out.write(b"!<arch>\n")
        out.write(ar_member("debian-binary", b"2.0\n"))
        out.write(ar_member("control.tar.gz", control_tar.getvalue()))
        out.write(ar_member("data.tar.gz", data_tar.getvalue()))


def zip_executable(zip_path: Path, arcname: str, binary_path: Path) -> None:
    mode = stat.S_IFREG | 0o755
    unix_attr = (mode << 16) & 0xFFFF0000
    with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        info = zipfile.ZipInfo(arcname)
        info.external_attr = unix_attr
        with binary_path.open("rb") as src:
            zf.writestr(info, src.read())


def zip_windows(zip_path: Path, exe_path: Path) -> None:
    with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        zf.write(exe_path, arcname=exe_path.name)


def main() -> None:
    RELEASE.mkdir(exist_ok=True)

    artifacts = [
        (
            "MarkOfTigerPatcher-win-x64.zip",
            ROOT / "bin/Release/net10.0/win-x64/publish/MarkOfTigerPatcher.exe",
            "windows",
        ),
        (
            "MarkOfTigerPatcher-osx-arm64.zip",
            ROOT / "bin/Release/net10.0/osx-arm64/publish/MarkOfTigerPatcher",
            "mac-arm",
        ),
        (
            "MarkOfTigerPatcher-osx-x64.zip",
            ROOT / "bin/Release/net10.0/osx-x64/publish/MarkOfTigerPatcher",
            "mac-x64",
        ),
        (
            "MarkOfTigerPatcher-linux-x64.zip",
            ROOT / "bin/Release/net10.0/linux-x64/publish/MarkOfTigerPatcher",
            "linux",
        ),
    ]

    for zip_name, binary, kind in artifacts:
        if not binary.is_file():
            raise SystemExit(f"Missing publish output: {binary}")

        out = RELEASE / zip_name
        if kind == "windows":
            zip_windows(out, binary)
        else:
            zip_executable(out, binary.name, binary)

    linux_binary = ROOT / "bin/Release/net10.0/linux-x64/publish/MarkOfTigerPatcher"
    control = f"""Package: {PACKAGE}
Version: {VERSION}
Section: utils
Priority: optional
Architecture: amd64
Maintainer: Marcel Pawelczyk <52792361+MPSQUARK@users.noreply.github.com>
Homepage: https://github.com/MPSQUARK/HOMM4_SavePatcher
Description: Fix HOMM4 Mark of the Tiger save soft-lock
 Patches .h4s save files so Elwin can open the Orc Gate on Mark of the Tiger.
"""
    build_deb(RELEASE / f"{PACKAGE}_{VERSION}_amd64.deb", linux_binary, control)

    print("Created:")
    for path in sorted(RELEASE.iterdir()):
        print(f"  {path.name} ({path.stat().st_size:,} bytes)")


if __name__ == "__main__":
    main()
