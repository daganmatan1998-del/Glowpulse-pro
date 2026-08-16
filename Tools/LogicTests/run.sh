#!/usr/bin/env bash
# Runs the Glowpulse logic test suite. See LogicTests.csproj for what it covers.
set -euo pipefail
cd "$(dirname "$0")"
exec dotnet run --project LogicTests.csproj -v q --nologo "$@"
