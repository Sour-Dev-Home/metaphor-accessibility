#!/usr/bin/env bash
# Joins the changelog fragments in changelog.d/ into CHANGELOG.md for a release (#758), then removes them.
#   scripts/release/join-changelog.sh --check             check the fragments' names and shape; change nothing
#   scripts/release/join-changelog.sh VERSION YYYY-MM-DD  put a "## VERSION - DATE" section under "## Unreleased"
#
# A fragment is changelog.d/<id>.<kind>.md holding one bullet, written as it should appear. <kind> is added, changed,
# fixed or removed, and becomes the heading "### Added" and so on. Within a kind the newest id (highest number) comes
# first, so the same fragments always give the same section. Lines still under "## Unreleased" in CHANGELOG.md, from PRs
# that predate fragments, go at the top of the new section, unchanged; both ways work during the transition.
# CHANGELOG_FILE and CHANGELOG_DIR override the two paths (the tests use them).
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
file="${CHANGELOG_FILE:-$root/CHANGELOG.md}"
dir="${CHANGELOG_DIR:-$root/changelog.d}"
kinds=(added changed fixed removed)

fragments() { # prints "sortkey<TAB>name" per fragment, newest first; fails on a bad name or body
  local f name id kind bad=0
  shopt -s nullglob
  for f in "$dir"/*.md; do
    name="$(basename "$f")"
    [ "$name" = README.md ] && continue
    if [[ ! "$name" =~ ^([A-Za-z0-9_+-]+)\.(added|changed|fixed|removed)\.md$ ]]; then
      echo "join-changelog: $name: name it <id>.<added|changed|fixed|removed>.md" >&2
      bad=1
      continue
    fi
    id="${BASH_REMATCH[1]}" kind="${BASH_REMATCH[2]}"
    if ! grep -q '[^[:space:]]' "$f" || [ "$(head -c 2 "$f")" != "- " ]; then
      echo "join-changelog: $name: it must hold one bullet, starting with \"- \"" >&2
      bad=1
      continue
    fi
    if [[ "$id" =~ ^[0-9]+$ ]]; then printf '%s%010d\t%s\n' "$kind" "$id" "$name"; else printf '%s%s\t%s\n' "$kind" "$id" "$name"; fi
  done
  return "$bad"
}

if [ "${1:-}" = --check ]; then
  fragments > /dev/null
  exit
fi

version="${1:-}" date="${2:-}"
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][A-Za-z0-9.]+)?$ ]] || [[ ! "$date" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}$ ]]; then
  echo "usage: scripts/release/join-changelog.sh --check | VERSION YYYY-MM-DD" >&2
  exit 2
fi

list="$(fragments)" # a bad fragment stops here
unreleased="$(awk '/^## /{on = ($0 == "## Unreleased"); next} on && NF' "$file")"
if [ -z "$list" ] && [ -z "$unreleased" ]; then
  echo "join-changelog: nothing to join; $file is unchanged"
  exit 0
fi

out="$(mktemp)"
trap 'rm -f "$out"' EXIT
{
  awk '{print} $0 == "## Unreleased" {exit}' "$file"
  printf '\n## %s - %s\n\n' "$version" "$date"
  [ -n "$unreleased" ] && printf '%s\n\n' "$unreleased"
  for kind in "${kinds[@]}"; do
    names="$(printf '%s\n' "$list" | { grep -a "^$kind" || true; } | LC_ALL=C sort -r | cut -f2)"
    [ -z "$names" ] && continue
    printf '### %s\n\n' "$(tr '[:lower:]' '[:upper:]' <<< "${kind:0:1}")${kind:1}"
    while IFS= read -r name; do
      sed -e '$a\' "$dir/$name"
    done <<< "$names"
    printf '\n'
  done
  awk 'seen && /^## /{go = 1} /^## Unreleased$/{seen = 1; next} go' "$file"
} > "$out"
cp "$out" "$file"
[ -z "$list" ] || printf '%s\n' "$list" | cut -f2 | while IFS= read -r name; do rm -f "$dir/$name"; done
echo "join-changelog: $version joined into $file"
