#!/usr/bin/env bash
# Checks CI's release dry run (#308) and the release workflow (#310) against the rules they exist for: every main build
# and every release zips the package with one recipe, a separate job that runs none of the repository's code attests
# the zip and verifies it with the command the players' how-to gives, no other job can sign, and only a tag on `main`
# drafts a release, from a job no pull request reaches. Reads the repository, writes nothing.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/wsl-run.sh
source "$ROOT/scripts/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
CI="$ROOT/.github/workflows/ci.yml"
RELEASE="$ROOT/.github/workflows/release.yml"
HOWTO="$ROOT/docs/players/how-to/check-a-download.md"
REPO="Sour-Dev-Home/metaphor-accessibility"

passed=0
failed=0

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}

job() { # job NAME [FILE]: the lines of one job (ci.yml by default), from its id to the next job's
  awk -v id="  $1:" '/^  [A-Za-z0-9_-]+:[[:space:]]*$/ { on = ($0 == id) } on' "${2:-$CI}" 2> /dev/null
}
permissions_of() { # permissions_of JOB_TEXT: the job's permissions, one "scope: level" per line, sorted
  awk '/^    permissions:/ { on = 1; next } on && /^    [^ ]/ { on = 0 } on' <<<"$1" |
    sed -E 's/[[:space:]]*#.*//; s/^[[:space:]]+//' | grep -v '^$' | sort
}
has() { grep -qE -- "$2" <<<"$1"; }     # has TEXT REGEX
lacks() { ! grep -qE -- "$2" <<<"$1"; } # lacks TEXT REGEX

build="$(job build)"
attest="$(job attest)"

# The build job packages the tested build with the release recipe (scripts/build-release.sh: clean-commit check,
# reproducible zip, SHA256SUMS, rebuild and compare; tests/scripts/test-build-release.sh), and uploads the zip.
check "the build job runs the release recipe on the tested build" \
  has "$build" '^ +run: bash scripts/build-release\.sh --no-build "metaphor-accessibility-\$GITHUB_SHA" artifacts$'
check "the build job uploads the zip and SHA256SUMS" has "$build" 'path: artifacts/release'

# The signing job: only on a push to main, only the three permissions signing needs, and none of our code.
check "ci.yml has an attest job" [ -n "$attest" ]
check "attest needs the build job" has "$attest" '^    needs: \[?build\]?$'
check "attest runs only on a push to main" \
  has "$attest" "^    if: github.event_name == 'push' && github.ref == 'refs/heads/main'$"
SIGNING="$(printf '%s\n' 'attestations: write' 'contents: read' 'id-token: write')"
check "attest asks for contents: read, id-token: write and attestations: write, nothing else" \
  [ "$(permissions_of "$attest")" = "$SIGNING" ]
check "attest checks out no code" lacks "$attest" 'actions/checkout@'
check "attest runs no repository script" lacks "$attest" 'scripts/'
check "attest checks the zip against SHA256SUMS before signing" has "$attest" 'sha256sum --check --strict SHA256SUMS'
check "attest signs with actions/attest" has "$attest" 'uses: actions/attest@'
verify="gh attestation verify \"\\\$zip\" --repo \"\\\$GITHUB_REPOSITORY\" --signer-workflow \"\\\$GITHUB_REPOSITORY/\\.github/workflows/ci\\.yml\" --source-ref refs/heads/main --deny-self-hosted-runners$"
check "attest verifies the zip as the how-to does" has "$attest" "$verify"

# The release workflow (#310): a `v*` tag, or a dry run by hand from main, and nothing a pull request can start.
check "release.yml exists" [ -f "$RELEASE" ]
release_text="$(cat "$RELEASE" 2> /dev/null)"
release_code="$(grep -vE '^[[:space:]]*#' <<<"$release_text")"
triggers="$(awk '/^on:/ { on = 1; next } on && /^[^[:space:]#]/ { on = 0 } on' <<<"$release_code")"
check "release.yml runs on a pushed tag and by hand only" \
  [ "$(grep -E '^  [a-z_]+:' <<<"$triggers" | sort | tr -d ' \n')" = "push:workflow_dispatch:" ]
check "release.yml's push trigger takes v* tags and no branch" \
  [ "$(awk '/^  push:/ { on = 1; next } /^  [a-z]/ { on = 0 } on' <<<"$triggers" | tr -d ' \n')" = "tags:[\"v*\"]" ]
check "release.yml's top-level token reads contents only" \
  [ "$(awk '/^permissions:/ { on = 1; next } on && /^[^[:space:]]/ { on = 0 } on' <<<"$release_code" | tr -d ' ')" = "contents:read" ]
release_jobs="$(sed -nE 's/^  ([A-Za-z0-9_-]+):[[:space:]]*$/\1/p' <<<"$(sed -n '/^jobs:/,$p' <<<"$release_code")" | tr '\n' ' ')"
check "release.yml's jobs are gate, build, attest and release" [ "$release_jobs" = "gate build attest release " ]
gate="$(job gate "$RELEASE")"
rbuild="$(job build "$RELEASE")"
rattest="$(job attest "$RELEASE")"
rrelease="$(job release "$RELEASE")"
check "release.yml's gate and build jobs read contents only" \
  [ "$(permissions_of "$gate")|$(permissions_of "$rbuild")" = "contents: read|contents: read" ]
check "release.yml's attest job asks for the signing permissions only" [ "$(permissions_of "$rattest")" = "$SIGNING" ]
check "release.yml's release job asks for contents: write only" [ "$(permissions_of "$rrelease")" = "contents: write" ]

# The gate: before anything is built, a tag's commit must be on main, and a run by hand must be on main itself.
check "gate fails a tag whose commit isn't on main" has "$gate" 'git merge-base --is-ancestor "\$commit" origin/main'
check "gate takes a run by hand from main only" has "$gate" '"\$GITHUB_REF" != refs/heads/main'
check "gate takes only version tags" has "$gate" '\^refs/tags/v\[0-9\]\+\\\.\[0-9\]\+\\\.\[0-9\]\+'
check "build waits for the gate" has "$rbuild" '^    needs: \[?gate\]?$'
# Every checkout takes the run's own commit: a ref computed in the run is code a job's output chose, which CodeQL's
# cache-poisoning query flags. The gate pins the tag to the run's commit instead, and release checks it again.
check "no checkout in release.yml takes a ref" lacks "$release_code" '^[[:space:]]+ref:'
check "gate peels the run's commit" has "$gate" 'commit="\$\(git rev-parse "\$GITHUB_SHA\^\{commit\}"\)"'
check "gate fails a checkout that isn't the run's commit" has "$gate" '"\$\(git rev-parse HEAD\)" != "\$commit"'
check "gate fails a tag that doesn't resolve to the run's commit" \
  has "$gate" '"\$\(git rev-parse "\$GITHUB_REF\^\{commit\}"\)" != "\$commit"'
check "build builds from scratch with the release recipe" \
  has "$rbuild" '^ +run: bash scripts/build-release\.sh "\$NAME" artifacts$'
check "release.yml uses no cache" lacks "$release_code" 'actions/cache|cache:'
check "every checkout in release.yml keeps no credentials" \
  [ "$(grep -c 'uses:[[:space:]]*actions/checkout@' <<<"$release_code")" = "$(grep -cE 'persist-credentials:[[:space:]]*false' <<<"$release_code")" ]

# Signing and publishing run none of our code: no checkout, no repository script.
for part in rattest rrelease; do
  text="${!part}"
  check "release.yml's ${part#r} job checks out no code" lacks "$text" 'actions/checkout@'
  check "release.yml's ${part#r} job runs no repository script" lacks "$text" 'scripts/'
  check "release.yml's ${part#r} job checks the zip against SHA256SUMS" \
    has "$text" 'sha256sum --check --strict SHA256SUMS'
done
check "attest needs the gate and the build" has "$rattest" '^    needs: \[gate, build\]$'
check "attest signs with actions/attest" has "$rattest" 'uses: actions/attest@'
check "attest verifies the zip as the how-to does, pinned to the tag" has "$rattest" \
  "gh attestation verify \"release/\\\$NAME\\.zip\" --repo \"\\\$GITHUB_REPOSITORY\" --signer-workflow \"\\\$GITHUB_REPOSITORY/\\.github/workflows/release\\.yml\" --source-ref \"\\\$GITHUB_REF\" --deny-self-hosted-runners$"
check "release waits for the signed, verified zip" has "$rrelease" '^    needs: \[gate, attest\]$'
check "release runs on a pushed v* tag only" \
  has "$rrelease" "^    if: github\\.event_name == 'push' && startsWith\\(github\\.ref, 'refs/tags/v'\\)$"
check "release drafts, never publishes, and only for a tag that exists" \
  has "$rrelease" 'gh release create "\$TAG" .*--draft --verify-tag'
tag_check="$(grep -nF 'repos/$GITHUB_REPOSITORY/git/ref/tags/$TAG' <<<"$rrelease" | head -n1 | cut -d: -f1)"
create_line="$(grep -nF 'gh release create' <<<"$rrelease" | head -n1 | cut -d: -f1)"
check "release checks the tag still names the gate's commit before it drafts" \
  [ "${tag_check:-9999}" -lt "${create_line:-0}" ]
check "release compares the tag's commit with the gate's" has "$rrelease" '"\$tagged" != "\$COMMIT"'
check "only the release job in release.yml creates a release" \
  lacks "$(grep -v -F -x -f <(printf '%s\n' "$rrelease") <<<"$release_code")" 'gh release'
check "release.yml splices no expression into a script" \
  bash -c '! grep -F "\${{" <<<"$1" | grep -vE "^[[:space:]]*(-[[:space:]]+)?[A-Za-z_][A-Za-z0-9_-]*:[[:space:]]" | grep -q . &&
    ! grep -F "\${{" <<<"$1" | grep -qE "^[[:space:]]*(-[[:space:]]+)?run:"' _ "$release_code"
check "release.yml reads no secrets" lacks "$release_code" 'secrets\.'

# Nothing else can sign: no other job in any workflow asks for an OIDC token or to write attestations.
for workflow in "$ROOT"/.github/workflows/*.yml "$ROOT"/.github/workflows/*.yaml; do
  [ -f "$workflow" ] || continue
  name="${workflow##*/}"
  if [ "$name" = ci.yml ] || [ "$name" = release.yml ]; then
    others="$(awk '/^  [A-Za-z0-9_-]+:[[:space:]]*$/ { skip = ($0 == "  attest:") } !skip' "$workflow")"
  else
    others="$(cat "$workflow")"
  fi
  check "$name: only the attest jobs of ci.yml and release.yml ask for id-token or attestations write" \
    lacks "$others" '^[[:space:]]*(id-token|attestations):[[:space:]]*write'
done

# The players' how-to gives the same check, for this repository, and a hash check for players without gh.
check "the players' how-to exists" [ -f "$HOWTO" ]
howto="$(cat "$HOWTO" 2> /dev/null)"
check "the how-to gives the gh attestation verify command CI runs" has "$howto" \
  "gh attestation verify [^ ]+\\.zip --repo $REPO --signer-workflow $REPO/\\.github/workflows/ci\\.yml --source-ref refs/heads/main --deny-self-hosted-runners$"
check "the how-to gives the release's command, pinned to its tag" has "$howto" \
  "gh attestation verify metaphor-accessibility-VERSION\\.zip --repo $REPO --signer-workflow $REPO/\\.github/workflows/release\\.yml --source-ref refs/tags/VERSION --deny-self-hosted-runners$"
# By ear, a player can't compare 64 hex characters: the hash check prints True or False against SHA256SUMS.
check "the how-to's hash check compares with SHA256SUMS and prints True or False" has "$howto" \
  "^ *\\(Get-FileHash [^ ]+\\.zip -Algorithm SHA256\\)\\.Hash -eq \\(Get-Content \\.\\\\SHA256SUMS\\)\\.Split\\(' '\\)\\[0\\]$"

printf 'release-integrity tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
