#!/bin/sh
# Copies CT2 templates into the web app and writes templates/index.json.
set -e
cd "$(dirname "$0")"
DEST=CrypWeb.App/wwwroot/templates
rm -rf "$DEST"; mkdir -p "$DEST"
( cd ../Templates && find . -name '*.cwm' | sort ) | while read -r f; do
  mkdir -p "$DEST/$(dirname "$f")"; cp "../Templates/$f" "$DEST/$f"; x="${f%.cwm}.xml"; [ -f "../Templates/$x" ] && cp "../Templates/$x" "$DEST/$x" || true
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
print(len(out),'templates')
PY
dotnet run --project CrypWeb.Tests -- ../Templates palette "$DEST/palette.json" | tail -1
