#!/usr/bin/env python3
"""
Small helpers to edit Unity prefab/scene YAML outside the editor (add a MonoBehaviour to a GameObject).
Usage from the command line:

  python3 Tools/unity_yaml.py add-component <prefab> <gameObjectFileID> <script.cs> "<field yaml>"

<field yaml> is the serialized fields block, one `name: value` per line (use \\n), e.g.
  "cursor: {fileID: 3173978429965695727}\\nmotor: {fileID: 8327803297820930270}"
Prints the new component fileID. Unity re-serializes the file on next save; that diff is expected.
"""
import pathlib
import random
import re
import sys


def script_guid(script: pathlib.Path) -> str:
    meta = script.with_name(script.name + ".meta").read_text()
    return re.search(r"guid: ([0-9a-f]{32})", meta).group(1)


def class_identifier(script: pathlib.Path) -> str:
    """Assembly::Namespace.Class, as Unity writes m_EditorClassIdentifier."""
    src = script.read_text()
    ns = re.search(r"namespace\s+([\w.]+)", src).group(1)
    folder = script.parent
    while not list(folder.glob("*.asmdef")):
        folder = folder.parent
    asm = re.search(r'"name":\s*"([^"]+)"', next(folder.glob("*.asmdef")).read_text()).group(1)
    return f"{asm}::{ns}.{script.stem}"


def new_file_id(text: str) -> int:
    used = set(int(x) for x in re.findall(r"&(-?\d+)", text))
    while True:
        fid = random.randint(10**17, 9 * 10**18)
        if fid not in used:
            return fid


def add_component(prefab: pathlib.Path, game_object: int, script: pathlib.Path, fields: str) -> int:
    text = prefab.read_text()
    fid = new_file_id(text)
    # 1. Register the component on the GameObject (only works for GameObjects defined in this file).
    pattern = re.compile(rf"(--- !u!1 &{game_object}\n(?:.*\n)*?  m_Component:\n(?:  - component: {{fileID: -?\d+}}\n)*)")
    m = pattern.search(text)
    if not m:
        raise SystemExit(f"GameObject {game_object} not found (stripped prefab-instance objects are not supported)")
    text = text[:m.end()] + f"  - component: {{fileID: {fid}}}\n" + text[m.end():]
    # 2. Append the MonoBehaviour document.
    body = "\n".join("  " + line for line in fields.replace("\\n", "\n").splitlines() if line.strip())
    block = f"""--- !u!114 &{fid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {game_object}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid(script)}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: {class_identifier(script)}
{body}
"""
    if not text.endswith("\n"):
        text += "\n"
    prefab.write_text(text + block)
    return fid


if __name__ == "__main__":
    if len(sys.argv) >= 5 and sys.argv[1] == "add-component":
        print(add_component(pathlib.Path(sys.argv[2]), int(sys.argv[3]), pathlib.Path(sys.argv[4]),
                            sys.argv[5] if len(sys.argv) > 5 else ""))
    else:
        print(__doc__)
        sys.exit(1)
