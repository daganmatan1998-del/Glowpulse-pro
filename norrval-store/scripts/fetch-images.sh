#!/usr/bin/env bash
# Download the Higgsfield generations into assets/source/, then build the
# responsive set. Run from the norrval-store folder:  bash scripts/fetch-images.sh
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p assets/source
BASE=https://d8j0ntlcm91z4.cloudfront.net/user_3JZ8NCelSVTokWHoniedyW5j2pH
while read -r slot file; do
  [ -z "$slot" ] && continue
  echo "fetching $slot"
  curl -fsSL -o "assets/source/$slot.png" "$BASE/$file"
done <<'LIST'
hero-desktop hf_20260926_164712_96b90dd5-7c98-4744-93b5-a5d80ae20da8.png
hero-mobile hf_20260926_164734_fc81b902-a25a-4391-a968-a9e97d75d568.png
lifestyle hf_20260926_164733_5f53cbdc-ac51-469e-a34d-0cd5b7c5b477.png
gift hf_20260926_164732_b1d4ac43-90b6-43d9-8ecd-5511293b4884.png
product hf_20260926_164734_a469b238-4336-4ef9-8c5e-691a477d151d.png
LIST
python3 scripts/images.py
