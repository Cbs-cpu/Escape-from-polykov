#!/usr/bin/env python3
"""
Creates missing Unity .meta files under Assets/ (scripts, asmdefs, folders, text assets) with fresh GUIDs,
and reports orphan .meta files. Unity would generate these on import, but committing them from the start
keeps GUIDs stable, so prefab/scene references written outside the editor stay valid.

Usage: python3 Tools/ensure_meta.py [--check]   (--check: only report, exit 1 if anything is missing)
"""
import pathlib
import sys
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[1] / "Assets"

FOLDER = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
SCRIPT = "fileFormatVersion: 2\nguid: {guid}"
ASMDEF = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
DEFAULT = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
TEXT = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def template(path: pathlib.Path) -> str:
    if path.is_dir():
        return FOLDER
    return {".cs": SCRIPT, ".asmdef": ASMDEF, ".md": TEXT, ".txt": TEXT, ".json": TEXT}.get(path.suffix, DEFAULT)


def main() -> int:
    check = "--check" in sys.argv
    missing, orphans = [], []
    for path in sorted(ROOT.rglob("*")):
        if path.name.startswith(".") or path.suffix == ".meta" or any(p.name.endswith("~") for p in path.parents):
            continue
        meta = path.with_name(path.name + ".meta")
        if not meta.exists():
            missing.append(path)
            if not check:
                meta.write_text(template(path).format(guid=uuid.uuid4().hex))
    for meta in ROOT.rglob("*.meta"):
        target = meta.with_name(meta.name[:-5])
        if not target.exists():
            orphans.append(meta)
    for p in missing:
        print(("missing meta: " if check else "created meta: ") + str(p.relative_to(ROOT.parent)))
    for m in orphans:
        # Empty folders are not stored by git but their .meta may be; only a warning.
        print("warning: orphan meta (target missing): " + str(m.relative_to(ROOT.parent)))
    return 1 if (check and missing) else 0


if __name__ == "__main__":
    sys.exit(main())
