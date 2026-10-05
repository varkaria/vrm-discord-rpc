"""Create a deterministic VPM ZIP, preserving every Unity GUID and ignored helper file."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile


def package(source, output):
    manifest = json.loads((source / "package.json").read_text(encoding="utf-8-sig"))
    output.mkdir(parents=True, exist_ok=True)
    destination = output / f"{manifest['name']}-{manifest['version']}.zip"
    with zipfile.ZipFile(destination, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in sorted(source.rglob("*")):
            if not path.is_file():
                continue
            if path.is_symlink():
                raise ValueError("Package sources cannot contain symbolic links")
            relative = path.relative_to(source)
            if any(part.startswith(".") for part in relative.parts):
                continue
            if not path.name.endswith(".meta") and not any(part.endswith("~") for part in relative.parts):
                if not path.with_name(path.name + ".meta").exists():
                    raise ValueError(f"Missing Unity metadata: {relative}")
            info = zipfile.ZipInfo(relative.as_posix(), date_time=(2024, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            archive.writestr(info, path.read_bytes())
    digest = hashlib.sha256(destination.read_bytes()).hexdigest()
    destination.with_suffix(".zip.sha256").write_text(f"{digest}  {destination.name}\n", encoding="utf-8")
    print(f"{destination.name}: {digest}")
    return destination


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("--output", type=Path, default=Path("dist"))
    args = parser.parse_args()
    package(args.source, args.output)
