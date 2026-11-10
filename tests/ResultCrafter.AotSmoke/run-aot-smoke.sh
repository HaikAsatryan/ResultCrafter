#!/usr/bin/env bash
# Publishes the smoke app as NativeAOT for each target framework, fails on any publish warning that comes
# from ResultCrafter code, then runs the native binary, which checks itself and exits 0 or 1.
# Usage: run-aot-smoke.sh [tfm ...]   (default: net10.0 net11.0). Needs clang on Linux, MSVC on Windows.
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
project="$root/tests/ResultCrafter.AotSmoke/ResultCrafter.AotSmoke.csproj"
tfms="${*:-net10.0 net11.0}"

for tfm in $tfms; do
  out="$root/artifacts/aot-smoke/$tfm"
  log="$root/artifacts/aot-smoke/publish-$tfm.log"
  mkdir -p "$out"

  echo "== Publishing $tfm"
  # One summary warning per third-party assembly keeps the log readable.
  dotnet publish "$project" -c Release -f "$tfm" -o "$out" -p:TrimmerSingleWarn=true 2>&1 | tee "$log"

  # MSBuild ends each line with " [project path]", which always contains ResultCrafter; drop it first.
  warnings="$(grep -E "warning (IL|AOT)[0-9]+" "$log" | sed -E 's/ \[[^]]*\]$//' | sort -u || true)"
  ours="$(printf '%s\n' "$warnings" | grep "ResultCrafter" || true)"
  if [ -n "$ours" ]; then
    echo "Publish warnings from ResultCrafter code ($tfm):"
    printf '%s\n' "$ours"
    exit 1
  fi
  if [ -n "$warnings" ]; then
    echo "Third-party publish warnings ($tfm), not ours:"
    printf '%s\n' "$warnings"
  fi

  echo "== Running $tfm"
  "$out/ResultCrafter.AotSmoke"
done
