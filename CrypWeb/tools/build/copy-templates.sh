#!/bin/sh
# Copies CT2 templates into the web app and writes templates/index.json.
set -e
cd "$(dirname "$0")/../.."
DEST=CrypWeb.App/wwwroot/templates
rm -rf "$DEST"; mkdir -p "$DEST"
( cd ../Templates && find . -name '*.cwm' | sort ) | while read -r f; do
  mkdir -p "$DEST/$(dirname "$f")"; cp "../Templates/$f" "$DEST/$f"; x="${f%.cwm}.xml"; [ -f "../Templates/$x" ] && cp "../Templates/$x" "$DEST/$x" || true
  i="${f%.cwm}.png"; [ -f "../Templates/$i" ] && cp "../Templates/$i" "$DEST/$i" || true
done
# experimental templates (Experimental/Templates) go to their own folder; the app shows it only when experimental
# plugins are switched on (View tab)
mkdir -p "$DEST/Experimental"
( cd ../Experimental/Templates && find . -name '*.cwm' | sort ) | while read -r f; do
  mkdir -p "$DEST/Experimental/$(dirname "$f")"; cp "../Experimental/Templates/$f" "$DEST/Experimental/$f"
  x="${f%.cwm}.xml"; [ -f "../Experimental/Templates/$x" ] && cp "../Experimental/Templates/$x" "$DEST/Experimental/$x" || true
  i="${f%.cwm}.png"; [ -f "../Experimental/Templates/$i" ] && cp "../Experimental/Templates/$i" "$DEST/Experimental/$i" || true
done
python3 -I - "$DEST" <<'PY'
import os,sys,json
d=sys.argv[1]; out=[]
for r,_,fs in os.walk(d):
    for f in fs:
        if f.endswith('.cwm'):
            p=os.path.relpath(os.path.join(r,f),d)
            out.append(p)
json.dump(sorted(out),open(os.path.join(d,'index.json'),'w'))
# templates that come with an icon (<name>.png next to <name>.cwm)
icons=[p for p in out if os.path.exists(os.path.join(d,p[:-4]+'.png'))]
json.dump(sorted(icons),open(os.path.join(d,'icons.json'),'w'))
print(len(out),'templates,',len(icons),'with icon')
PY
dotnet run --project CrypWeb.Tests -- ../Templates palette "$DEST/palette.json" | tail -1
