#!/bin/bash
# Usage: bash copy_into_project.sh /full/path/to/MissionMohakash
# Copies Assets/ from this bundle into your Unity project (merges; never deletes anything).
set -e
if [ -z "$1" ] || [ ! -d "$1/Assets" ]; then
  echo "Give the path to your Unity project folder (the one containing Assets/ and ProjectSettings/)."
  echo "Example: bash copy_into_project.sh ~/Documents/MissionMohakash"
  exit 1
fi
HERE="$(cd "$(dirname "$0")" && pwd)"
cp -Rn "$HERE/Assets/." "$1/Assets/"
mkdir -p "$1/Docs" && cp -Rn "$HERE/Docs/." "$1/Docs/"
echo "Copied. Now switch to Unity and wait for it to compile."
