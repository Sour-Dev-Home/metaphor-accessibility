#!/usr/bin/env bash
# Black-box tests for scripts/release/join-changelog.sh (#758): the release recipe that joins changelog.d/ fragments
# into CHANGELOG.md. Joining is deterministic, the fragments are removed, an empty set is handled, the kinds map to
# headings, and lines left under "## Unreleased" by older PRs are kept (both ways work during the transition).
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
SCRIPT="$ROOT/scripts/release/join-changelog.sh"

passed=0
failed=0
check() { local n="$1"; shift; if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $n"; fi; }

# A fresh changelog and fragment folder; $1 is what sits under "## Unreleased".
setup() {
  rm -rf "$WORK/d" "$WORK/CHANGELOG.md"
  mkdir -p "$WORK/d"
  printf '# Changelog\n\nIntro.\n\n## Unreleased\n%s\n## 0.1.0 - old\n\nOld text.\n' "$1" > "$WORK/CHANGELOG.md"
  export CHANGELOG_FILE="$WORK/CHANGELOG.md" CHANGELOG_DIR="$WORK/d"
}
frag() { printf '%s\n' "$2" > "$WORK/d/$1"; }
join() { out="$(bash "$SCRIPT" "$@" 2>&1)"; status=$?; }
has() { grep -qxF -- "$1" "$WORK/CHANGELOG.md"; }
line() { grep -nxF -- "$1" "$WORK/CHANGELOG.md" | head -1 | cut -d: -f1; }

setup ""
frag 5.added.md "- Five."
frag 12.added.md "- Twelve."
frag 7.fixed.md "- Seven fixed."
frag 3.changed.md "- Three changed."
frag 9.removed.md "- Nine removed."
frag +docs.added.md "- A slug."
join 0.2.0 2026-12-01
check "joining succeeds" [ "$status" -eq 0 ]
check "the version heading goes under Unreleased" [ "$(line '## Unreleased')" -lt "$(line '## 0.2.0 - 2026-12-01')" ]
check "the old release stays below" [ "$(line '## 0.2.0 - 2026-12-01')" -lt "$(line '## 0.1.0 - old')" ]
for h in Added Changed Fixed Removed; do check "kind heading $h" has "### $h"; done
check "Added comes before Changed" [ "$(line '### Added')" -lt "$(line '### Changed')" ]
check "Changed comes before Fixed" [ "$(line '### Changed')" -lt "$(line '### Fixed')" ]
check "Fixed comes before Removed" [ "$(line '### Fixed')" -lt "$(line '### Removed')" ]
check "the newest id comes first" [ "$(line '- Twelve.')" -lt "$(line '- Five.')" ]
check "a fragment lands under its own kind" [ "$(line '### Fixed')" -lt "$(line '- Seven fixed.')" ]
check "every fragment is in" has "- A slug."
check "the fragments are removed" [ -z "$(ls "$WORK/d")" ]
cp "$WORK/CHANGELOG.md" "$WORK/first.md"

# The same fragments give the same text.
setup ""
frag 5.added.md "- Five."
frag 12.added.md "- Twelve."
frag 7.fixed.md "- Seven fixed."
frag 3.changed.md "- Three changed."
frag 9.removed.md "- Nine removed."
frag +docs.added.md "- A slug."
join 0.2.0 2026-12-01
check "a rebuild matches" cmp -s "$WORK/CHANGELOG.md" "$WORK/first.md"

# An empty set changes nothing and is not an error.
setup ""
cp "$WORK/CHANGELOG.md" "$WORK/before.md"
join 0.2.0 2026-12-01
check "an empty set succeeds" [ "$status" -eq 0 ]
check "an empty set leaves the file alone" cmp -s "$WORK/CHANGELOG.md" "$WORK/before.md"

# Lines older PRs added under Unreleased are kept, above the kinds.
setup "
- **Old style.** Added by hand."
frag 5.added.md "- Five."
join 0.2.0 2026-12-01
check "an old-style line is kept" has "- **Old style.** Added by hand."
check "an old-style line sits above the kinds" [ "$(line '- **Old style.** Added by hand.')" -lt "$(line '### Added')" ]
check "Unreleased is left empty" [ "$(line '## Unreleased')" -lt "$(line '## 0.2.0 - 2026-12-01')" ]
setup "
- **Only old.** Nothing else."
join 0.2.0 2026-12-01
check "old-style lines alone are joined too" has "- **Only old.** Nothing else."

# Bad fragments and bad calls fail without touching anything.
setup ""
frag 5.fancy.md "- Five."
cp "$WORK/CHANGELOG.md" "$WORK/before.md"
join 0.2.0 2026-12-01
check "an unknown kind is refused" [ "$status" -ne 0 ]
check "a refused join changes nothing" cmp -s "$WORK/CHANGELOG.md" "$WORK/before.md"
check "a refused join keeps the fragment" [ -f "$WORK/d/5.fancy.md" ]
rm "$WORK/d/5.fancy.md"
frag 6.added.md "No bullet."
join --check
check "--check refuses a fragment that is not a bullet" [ "$status" -ne 0 ]
rm "$WORK/d/6.added.md"
frag 6.added.md "- Fine."
join --check
check "--check accepts good fragments" [ "$status" -eq 0 ]
check "--check keeps the fragments" [ -f "$WORK/d/6.added.md" ]
join 0.2 2026-12-01
check "a bad version is usage" [ "$status" -eq 2 ]

# The fragments in the repository itself are well formed.
unset CHANGELOG_FILE CHANGELOG_DIR
join --check
check "the repository's fragments are well formed" [ "$status" -eq 0 ]

printf 'join-changelog tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
