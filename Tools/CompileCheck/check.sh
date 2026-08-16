#!/usr/bin/env bash
# Compile-verify every script under Assets/Scripts without the Unity Editor.
# See CompileCheck.csproj for how the reference assemblies are supplied.
set -euo pipefail
cd "$(dirname "$0")"

if [ ! -d /opt/unityrefs/pkg/lib/net45 ]; then
  echo "Unity reference assemblies missing. Run: Tools/CompileCheck/fetch-refs.sh" >&2
  exit 1
fi

dotnet build CompileCheck.csproj -v m --nologo "$@" 2>&1 \
  | grep -Ev '^(  Determining|  Restored|  CompileCheck ->)' \
  | sed '/^$/N;/^\n$/D'
