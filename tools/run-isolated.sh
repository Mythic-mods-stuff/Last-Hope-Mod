#!/usr/bin/env bash
set -Eeuo pipefail
project=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
mode=${1:-check}
game="$HOME/.local/share/Steam/steamapps/common/tModLoader"
workshop="$HOME/.local/share/Steam/steamapps/workshop/content/1281930"
check_root=$(mktemp -d /tmp/last-hope-alt-check.XXXXXX)
mkdir -p "$check_root/Mods" "$project/verification"
python3 - "$workshop" "$check_root/Mods" "$project/artifacts/LastHopeAlt.tmod" "$mode" <<'PY'
from pathlib import Path
import sys,shutil,json
workshop,mods,artifact=map(Path,sys.argv[1:4]); mode=sys.argv[4]
for wid,name in [('2824688072','CalamityMod'),('2824688266','CalamityModMusic')]:
    def branch(p): return tuple(int(x) for x in p.parent.name.split('.') if x.isdigit())
    paths=[p for p in (workshop/wid).rglob(name+'.tmod') if not branch(p) or branch(p)<=(2026,8)]
    (mods/(name+'.tmod')).symlink_to(max(paths,key=branch))
shutil.copy2(artifact,mods/artifact.name)
enabled=['CalamityModMusic','CalamityMod','LastHopeAlt']
if mode=='check':
    shutil.copy2(Path.home()/'.local/share/Terraria/tModLoader/Mods/AccessorySpawner.tmod',mods/'AccessorySpawner.tmod')
    enabled.append('AccessorySpawner')
(mods/'enabled.json').write_text(json.dumps(enabled))
PY
cd "$game"
if [[ "$mode" == export ]]; then
    LAST_HOPE_ALT_EXPORT="$project/src/LastHopeAlt/Data/accessory-stats.json" \
        timeout 600 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory "$check_root" -modpath "$check_root/Mods" 2>&1 | tee "$project/verification/profile-export.log"
elif [[ "$mode" == check ]]; then
    LAST_HOPE_ALT_CHECK_REPORT="$project/verification/last-hope-alt-checks.json" \
        timeout 600 dotnet tModLoader.dll -server -nosteam -tmlsavedirectory "$check_root" -modpath "$check_root/Mods" 2>&1 | tee "$project/verification/check.log"
else
    echo 'Usage: bash tools/run-isolated.sh [check|export]' >&2
    exit 2
fi
