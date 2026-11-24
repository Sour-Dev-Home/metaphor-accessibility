#!/usr/bin/env bash
# CI's pre-push-hook job (#655): scripts/ci/pre-push-hook.sh runs the pre-push hook through a real `git push`, so a
# failure only git's hook environment causes (#672: git's exec path first on PATH) fails it; the job runs when what-runs
# says so (cases in tests/bash/test-what-runs.sh) and the required `scripts` check reads its result. Writes only to its
# own temporary folder.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
cd "$ROOT" || exit 2
CI=.github/workflows/ci.yml
DRIVER="$ROOT/scripts/ci/pre-push-hook.sh"
passed=0 failed=0
check() { local n="$1"; shift; if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $n"; fi; }
has() { grep -qF -- "$1" <<< "$out"; }

# The workflow. job_block JOB: the job's lines in ci.yml, up to the next job.
job_block() { awk -v job="  $1:" '$0 == job { on = 1; print; next } on && /^  [A-Za-z0-9_-]+:[[:space:]]*$/ { exit } on' "$CI"; }
hook_job="$(job_block pre-push-hook)"
check "the job exists" [ -n "$hook_job" ]
check "it skips pull requests only" grep -qxF "    if: github.event_name != 'pull_request'" <<< "$hook_job"
check "it runs on Windows, as contributors push" grep -qxF '    runs-on: windows-latest' <<< "$hook_job"
check "it asks what-runs.sh" grep -qF 'what-runs.sh --job pre-push-hook "$BASE" "$HEAD"' <<< "$hook_job"
check "its checkout has the history to diff" grep -qF 'fetch-depth: 0' <<< "$hook_job"
check "it diffs the queue's merge" grep -qF 'github.event.merge_group.base_sha' <<< "$hook_job"
check "it pushes through the driver" grep -qF 'scripts/ci/pre-push-hook.sh "$BASE" "$HEAD"' <<< "$hook_job"
steps_after="$(awk '/id: changes/ { on = 1; next } on && /^      - / { n++ } END { print n + 0 }' <<< "$hook_job")"
gated="$(awk '/id: changes/ { on = 1; next } on && /^        if: steps\.changes\.outputs\.run == .true.$/ { n++ } END { print n + 0 }' <<< "$hook_job")"
check "every step after the change check waits on it ($gated of $steps_after)" [ "$steps_after" -gt 0 ] && [ "$gated" = "$steps_after" ]
check "the required scripts check waits on it" grep -qE '^    needs: \[.*pre-push-hook.*\]' <<< "$(job_block scripts)"

# The aggregator, run as CI runs it, with the Pester jobs passed.
gate="$(job_block scripts | awk '/^        run: \|/ { on = 1; next } on && /^        [^ ]/ { exit } on { sub(/^          /, ""); print }')"
check "the scripts job's script was found" [ -n "$gate" ]
scripts_gate() { # scripts_gate EVENT HOOK: exit 0 when `scripts` passes
  local result=success
  [ "$1" != schedule ] || result=skipped
  EVENT_NAME="$1" RESULT="$result" VARIED=success HOOK="$2" bash -c "$gate" > /dev/null 2>&1
}
gate_fails() { ! scripts_gate "$@"; }
check "scripts passes: a pull request skips the hook" scripts_gate pull_request skipped
check "scripts passes: the queue's hook passed" scripts_gate merge_group success
check "scripts passes: the nightly hook passed" scripts_gate schedule success
check "scripts fails: the queue's hook failed" gate_fails merge_group failure
check "scripts fails: the queue skipped the hook" gate_fails merge_group skipped
check "scripts fails: main skipped the hook" gate_fails push skipped
check "scripts fails: the nightly skipped the hook" gate_fails schedule skipped
check "scripts fails: the hook was cancelled" gate_fails push cancelled
check "scripts fails: no hook result" gate_fails merge_group ''

# The driver, against a throwaway repository with a planted hook.
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
repo="$WORK/repo"
mkdir -p "$repo/scripts/githooks" "$WORK/tmp" "$WORK/other-hooks" || exit 2
# The bug #672 fixed, planted: a hook that fails only when git's exec path leads PATH, as git sets it for hooks.
cat > "$repo/scripts/githooks/pre-push" << 'EOF'
#!/usr/bin/env bash
cat > "${RECORD:-/dev/null}"
case "${PATH%%:*}" in *git-core) echo "planted: failed inside the hook"; exit 1 ;; esac
EOF
printf '#!/usr/bin/env bash\nexit 0\n' > "$WORK/other-hooks/pre-push"
chmod +x "$repo/scripts/githooks/pre-push" "$WORK/other-hooks/pre-push"
(cd "$repo" && git init -q && git add -A && git commit -q -m "chore: root" &&
  echo a > a.txt && git add a.txt && git commit -q -m "chore: base" &&
  echo b > b.txt && git add b.txt && git commit -q -m "chore: head") || exit 2
root="$(git -C "$repo" rev-list --max-parents=0 HEAD)" base="$(git -C "$repo" rev-parse HEAD~1)"
head="$(git -C "$repo" rev-parse HEAD)"
drive() { (cd "$repo" && TMPDIR="$WORK/tmp" RECORD="$WORK/record" bash "$DRIVER" "$@" 2>&1); }

out="$(cd "$repo" && bash scripts/githooks/pre-push < /dev/null 2>&1)"
check "the planted hook passes when run directly, as CI's other jobs run checks ($out)" [ $? -eq 0 ]
out="$(drive "$base" "$head")"
code=$?
check "the planted hook-only failure fails the driver (exit $code)" [ "$code" -ne 0 ] && [ "$code" -ne 2 ]
check "the hook's own message reaches the log" has "planted: failed inside the hook"
read -r _ local_sha _ remote_sha < "$WORK/record"
check "the hook takes HEAD as pushed" [ "$local_sha" = "$head" ]
check "the hook takes BASE as the remote's" [ "$remote_sha" = "$base" ]
check "the scratch remote is removed" [ -z "$(ls -A "$WORK/tmp")" ]

git -C "$repo" config core.hooksPath "$WORK/other-hooks"
out="$(drive "$base" "$head")"
check "a configured core.hooksPath doesn't change which hook runs" has "planted: failed inside the hook"
git -C "$repo" config --unset core.hooksPath

# A passing hook: the same plant, without the failure.
sed -i '/planted/d' "$repo/scripts/githooks/pre-push"
out="$(drive "$base" "$head")"
check "a passing hook passes the driver ($out)" [ $? -eq 0 ]
out="$(drive "" "$head")"
check "an empty BASE passes too" [ $? -eq 0 ]
read -r _ _ _ remote_sha < "$WORK/record"
check "an empty BASE starts from the root commit" [ "$remote_sha" = "$root" ]
rm -f "$WORK/record"
out="$(drive "$head" "$head")"
check "BASE equal to HEAD still runs the hook" [ $? -eq 0 ] && [ -s "$WORK/record" ]
check "the repository's own config is left alone" bash -c '! git -C "$1" config core.hooksPath' _ "$repo"

out="$(drive "$head" "$base")"
check "BASE not before HEAD is refused (exit $?)" has "is not an ancestor"
out="$(drive "$base" nope)"
check "a HEAD that isn't a commit is refused" has "is not a commit"
out="$(drive "$base")"
check "a missing argument is a usage error" has "usage: pre-push-hook.sh BASE HEAD"

echo "pre-push CI tests: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
