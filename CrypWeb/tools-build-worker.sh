#!/bin/sh
# Builds the engine worker and copies it into the web app (served at /worker/).
set -e
cd "$(dirname "$0")"
dotnet build CrypWeb.Worker -c Release -nologo -v:q
SRC=CrypWeb.Worker/bin/Release/net10.0/wwwroot
rm -rf CrypWeb.App/wwwroot/worker; mkdir -p CrypWeb.App/wwwroot/worker
cp -R "$SRC"/. CrypWeb.App/wwwroot/worker/
cp CrypWeb.Worker/wwwroot/main.js CrypWeb.App/wwwroot/worker/main.js
echo "worker copied: $(du -sh CrypWeb.App/wwwroot/worker | cut -f1)"
