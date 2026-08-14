#!/usr/bin/env bash
# Builds a Shopify-uploadable zip containing only the theme directories.
# Usage: ./scripts/package.sh [output-name]

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT="${1:-glowpulse-theme.zip}"

cd "$ROOT"

if ! command -v zip >/dev/null 2>&1; then
  echo "error: 'zip' is not installed." >&2
  exit 1
fi

rm -f "$OUTPUT"

# Shopify only accepts these top-level directories in a theme zip.
zip -r -q "$OUTPUT" \
  assets \
  config \
  layout \
  locales \
  sections \
  snippets \
  templates \
  -x '*.DS_Store' -x '__MACOSX/*'

echo "Built $OUTPUT ($(du -h "$OUTPUT" | cut -f1))"
echo "Upload it via Shopify admin: Online Store > Themes > Add theme > Upload zip file"
