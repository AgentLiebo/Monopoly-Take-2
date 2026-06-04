#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEFAULT_UNITY_EDITOR="$HOME/Unity/Hub/Editor/6000.4.10f1/Editor/Unity"
UNITY_EDITOR="${UNITY_EDITOR:-$DEFAULT_UNITY_EDITOR}"
UNITY_BUILD_OUTPUT="${UNITY_BUILD_OUTPUT:-$PROJECT_ROOT/Builds/Windows/MonopolyTake2.exe}"
export UNITY_BUILD_OUTPUT

if [[ ! -x "$UNITY_EDITOR" ]]; then
  echo "Unity Editor not found at $UNITY_EDITOR" >&2
  echo "Install Unity 6000.4.10f1 or set UNITY_EDITOR=/path/to/Unity." >&2
  exit 1
fi

mkdir -p "$(dirname "$UNITY_BUILD_OUTPUT")"
"$UNITY_EDITOR" \
  -batchmode \
  -nographics \
  -quit \
  -projectPath "$PROJECT_ROOT" \
  -executeMethod MonopolyTake2.Editor.UnityBuild.BuildWindowsPlayer \
  -logFile "$PROJECT_ROOT/Builds/unity-build.log"

echo "Windows Unity player requested at: $UNITY_BUILD_OUTPUT"
echo "Unity build log: $PROJECT_ROOT/Builds/unity-build.log"
