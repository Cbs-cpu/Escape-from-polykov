#!/usr/bin/env bash
# Renders the lobby screens without Unity: 3D backgrounds + item icons in Blender (same assets, camera math and
# framing as the game), then the real UI code draws on top through Skia.
# Usage: Tools/UiPreview/run_previews.sh <outDir> [blender executable]
set -euo pipefail
OUT=$(realpath -m "$1")
BLENDER=${2:-blender}
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
BG="$OUT/bg"; ICONS="$OUT/icons"
mkdir -p "$BG" "$ICONS"
export POLYKOV_ROOT="$ROOT"
SCRIPT="$ROOT/ArtSource/Tools/render_lobby_preview.py"

dotnet build "$ROOT/Tools/UiPreview/UiPreview.csproj" -nologo -v:q >/dev/null
FRAMING=$(dotnet run --no-build --project "$ROOT/Tools/UiPreview" -- framing)
render() { "$BLENDER" -b --python-exit-code 1 -P "$SCRIPT" -- "$@" 2>&1 | grep -E "^rendered|Error|Traceback" || true; }

while read -r view px py pz lx ly lz fov; do
  case $view in
    MainMenu) name=bg_main ;;
    Character) name=bg_character ;;
    *) continue ;;
  esac
  render stage "$BG/$name.png" 1920 1080 "$px" "$py" "$pz" "$lx" "$ly" "$lz" "$fov"
done <<< "$FRAMING"
render armorer "$BG/bg_armorer.png" 1920 1080
render armorer_suppressor "$BG/bg_armorer_suppressor.png" 1920 1080
render icon m1911a1 "$ICONS/m1911a1.png"
render icon m1911a1 "$ICONS/m1911a1_suppressed.png" suppressed
render icon suppressor_45 "$ICONS/suppressor_45.png"

dotnet run --no-build --project "$ROOT/Tools/UiPreview" -- render "$OUT/screens" "$BG" "$ICONS"
