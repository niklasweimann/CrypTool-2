#!/bin/sh
# Builds everything the web app needs and publishes it to ./dist (served by Cloudflare Workers static assets).
# Pass --skip-worker to reuse the already built engine worker / templates.
set -e
cd "$(dirname "$0")"
if [ "$1" != "--skip-worker" ]; then
  ./tools-copy-templates.sh
  ./tools-build-worker.sh
fi
rm -rf dist
dotnet publish CrypWeb.App -c Release -nologo -v:q -o dist/_publish
mv dist/_publish/wwwroot/* dist/ 2>/dev/null || true
rm -rf dist/_publish
# Cloudflare compresses on the fly; the precompressed Blazor copies would only be extra files.
find dist \( -name '*.br' -o -name '*.gz' \) -not -path 'dist/vfs/*' -not -path 'dist/templates/*' -delete
echo "dist ready: $(find dist -type f | wc -l | tr -d ' ') files, $(du -sh dist | cut -f1)"
