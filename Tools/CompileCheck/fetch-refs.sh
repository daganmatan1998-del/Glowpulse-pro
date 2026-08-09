#!/usr/bin/env bash
# Downloads Unity's official reference assemblies so the project can be
# type-checked without a Unity installation.
set -euo pipefail
DEST=${1:-/opt/unityrefs}
VER=2021.3.33
mkdir -p "$DEST"
cd "$DEST"
curl -sSL -o ue.nupkg \
  "https://api.nuget.org/v3-flatcontainer/unityengine.modules/${VER}/unityengine.modules.${VER}.nupkg"
rm -rf pkg
unzip -o -q ue.nupkg -d pkg
echo "Unity ${VER} reference assemblies installed to ${DEST}/pkg/lib/net45"
