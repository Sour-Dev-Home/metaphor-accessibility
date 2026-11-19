#!/usr/bin/env bash
# Checks CI's release dry run (#308) against the rules it exists for: every main build zips the package reproducibly,
# a separate job that runs none of the repository's code attests the zip and verifies it with the command the players'
# how-to gives, and no other job can sign. Reads the repository, writes nothing.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/wsl-run.sh
source "$ROOT/scripts/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
CI="$ROOT/.github/workflows/ci.yml"
HOWTO="$ROOT/docs/players/how-to/check-a-download.md"
REPO="Sour-Dev-Home/metaphor-accessibility"

passed=0
failed=0

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}

job() { # job NAME: the lines of one ci.yml job, from its id to the next job's
  awk -v id="  $1:" '/^  [A-Za-z0-9_-]+:[[:space:]]*$/ { on = ($0 == id) } on' "$CI"
}
has() { grep -qE -- "$2" <<<"$1"; }     # has TEXT REGEX
lacks() { ! grep -qE -- "$2" <<<"$1"; } # lacks TEXT REGEX

build="$(job build)"
attest="$(job attest)"

# The build job zips the tested package with the reproducible zip script, and the rebuild must give the same zip.
check "the build job zips the package with zip-package.ps1" has "$build" 'scripts/zip-package\.ps1 -Path artifacts/mod '
check "the build job writes SHA256SUMS" has "$build" 'SHA256SUMS'
check "the rebuild zips its own package and compares the zips" has "$build" 'rebuild/scripts/zip-package\.ps1'
check "the build job uploads the zip and SHA256SUMS" has "$build" 'path: artifacts/release'

# The signing job: only on a push to main, only the three permissions signing needs, and none of our code.
check "ci.yml has an attest job" [ -n "$attest" ]
check "attest needs the build job" has "$attest" '^    needs: \[?build\]?$'
check "attest runs only on a push to main" \
  has "$attest" "^    if: github.event_name == 'push' && github.ref == 'refs/heads/main'$"
permissions="$(awk '/^    permissions:/ { on = 1; next } on && /^    [^ ]/ { on = 0 } on' <<<"$attest" | sed -E 's/[[:space:]]*#.*//; s/^[[:space:]]+//' | sort)"
check "attest asks for contents: read, id-token: write and attestations: write, nothing else" \
  [ "$permissions" = "$(printf '%s\n' 'attestations: write' 'contents: read' 'id-token: write')" ]
check "attest checks out no code" lacks "$attest" 'actions/checkout@'
check "attest runs no repository script" lacks "$attest" 'scripts/'
check "attest checks the zip against SHA256SUMS before signing" has "$attest" 'sha256sum --check --strict SHA256SUMS'
check "attest signs with actions/attest" has "$attest" 'uses: actions/attest@'
verify="gh attestation verify \"\\\$zip\" --repo \"\\\$GITHUB_REPOSITORY\" --signer-workflow \"\\\$GITHUB_REPOSITORY/\\.github/workflows/ci\\.yml\" --source-ref refs/heads/main --deny-self-hosted-runners$"
check "attest verifies the zip as the how-to does" has "$attest" "$verify"

# Nothing else can sign: no other job in any workflow asks for an OIDC token or to write attestations.
for workflow in "$ROOT"/.github/workflows/*.yml "$ROOT"/.github/workflows/*.yaml; do
  [ -f "$workflow" ] || continue
  name="${workflow##*/}"
  if [ "$name" = ci.yml ]; then
    others="$(awk '/^  [A-Za-z0-9_-]+:[[:space:]]*$/ { skip = ($0 == "  attest:") } !skip' "$workflow")"
  else
    others="$(cat "$workflow")"
  fi
  check "$name: only ci.yml's attest job asks for id-token or attestations write" \
    lacks "$others" '^[[:space:]]*(id-token|attestations):[[:space:]]*write'
done

# The players' how-to gives the same check, for this repository, and a hash check for players without gh.
check "the players' how-to exists" [ -f "$HOWTO" ]
howto="$(cat "$HOWTO" 2> /dev/null)"
check "the how-to gives the gh attestation verify command CI runs" has "$howto" \
  "gh attestation verify [^ ]+\\.zip --repo $REPO --signer-workflow $REPO/\\.github/workflows/ci\\.yml --source-ref refs/heads/main --deny-self-hosted-runners$"
# By ear, a player can't compare 64 hex characters: the hash check prints True or False against SHA256SUMS.
check "the how-to's hash check compares with SHA256SUMS and prints True or False" has "$howto" \
  "^ *\\(Get-FileHash [^ ]+\\.zip -Algorithm SHA256\\)\\.Hash -eq \\(Get-Content \\.\\\\SHA256SUMS\\)\\.Split\\(' '\\)\\[0\\]$"

printf 'release-integrity tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
