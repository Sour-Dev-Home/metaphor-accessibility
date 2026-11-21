#!/usr/bin/env bash
# Tests for the flake rule (#655): scripts/ci/retry-test.sh retries a failed test command once and records a pass on
# the retry; scripts/ci/flake-report.sh turns the records into #629's comment, as data; and the workflows use them only
# where they may: CI's test steps, never the build, security or repo-rules guards, and the report workflow with the
# least token it needs.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
cd "$ROOT" || exit 2
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
passed=0 failed=0
check() { local n="$1"; shift; if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $n"; fi; }
has() { [[ "$out" == *"$1"* ]]; }
lacks() { [[ "$out" != *"$1"* ]]; }

# A planted test: it fails its first FAILS runs, printing a check suite's and a coloured Pester failure line, then
# passes. Its run count is in $WORK/runs.
cat > "$WORK/planted.sh" << 'EOF'
#!/usr/bin/env bash
n=$(($(cat "$RUNS" 2> /dev/null || echo 0) + 1))
echo "$n" > "$RUNS"
if [ "$n" -le "$FAILS" ]; then
  echo "FAIL job count: the pool's default goes over"
  printf '\033[91m  [-] onboard.Visual C++ runtime: installed 12ms (10ms|2ms)\033[0m\n'
  exit 3
fi
echo "all passed"
EOF
retry() { # retry FAILS: run the planted test through retry-test.sh in a fresh folder; code in $code, output in $out
  rm -rf "$WORK/flake" "$WORK/runs"
  out="$(RUNS="$WORK/runs" FAILS="$1" FLAKE_DIR="$WORK/flake" \
    bash scripts/ci/retry-test.sh "pester (onboard)" bash "$WORK/planted.sh" 2>&1)"
  code=$?
  runs="$(cat "$WORK/runs")"
}

retry 0
check "a pass runs once" [ "$runs" = 1 ]
check "a pass passes" [ "$code" = 0 ]
check "a pass records no flake" [ ! -e "$WORK/flake/flake.txt" ]

retry 1
check "a flaky test runs twice" [ "$runs" = 2 ]
check "a flaky test passes on the retry" [ "$code" = 0 ]
check "the retry is announced" has "::warning title=Test failed once::pester (onboard) failed (exit 3)"
check "the flake is announced" has "::warning title=Flaky test::"
record="$(cat "$WORK/flake/flake.txt" 2> /dev/null)"
check "the record names the job" grep -qx "job: pester (onboard)" <<< "$record"
check "the record names the check suite's test" grep -qx "test: FAIL job count: the pool's default goes over" <<< "$record"
check "the record names the Pester test, colours gone" \
  grep -qx "test: \[-\] onboard.Visual C++ runtime: installed 12ms (10ms|2ms)" <<< "$record"

retry 2
check "two failures run twice, never more" [ "$runs" = 2 ]
check "two failures fail with the test's code" [ "$code" = 3 ]
check "two failures say so" has "::error title=Test failed twice::"
check "two failures record no flake" [ ! -e "$WORK/flake/flake.txt" ]

retry 5
check "more failures still stop at two runs" [ "$runs" = 2 ]
out="$(bash scripts/ci/retry-test.sh only-a-label 2>&1)"
check "no command is a usage error" [ $? = 2 ]

# The comment: one bullet per job, its tests in code spans, and the caller's run link.
url="https://github.com/Sour-Dev-Home/metaphor-accessibility/actions/runs/123/attempts/1"
retry 1
out="$(bash scripts/ci/flake-report.sh comment "$WORK/flake" "$url")"
check "the comment links the run" has "run $url:"
check "the comment names the job" has '- Job `pester (onboard)` failed once and passed on the retry.'
check "the comment names the test" has "  - \`FAIL job count: the pool's default goes over\`"
# A hostile record, as a fork's run could leave one.
mkdir -p "$WORK/hostile/1" "$WORK/hostile/2"
printf 'job: x` @maintainer <img src=x> [link](https://evil.example)\ntest: `@everyone` ![a](b)\n' > "$WORK/hostile/1/flake.txt"
for i in $(seq 1 30); do echo "test: case $i"; done | sed '1i job: many' > "$WORK/hostile/2/flake.txt"
out="$(bash scripts/ci/flake-report.sh comment "$WORK/hostile" "$url")"
check "a record can't mention anyone" lacks "@"
check "a record can't add HTML" lacks "<"
check "a record can't close its code span" [ "$(grep -o '`' <<< "$out" | wc -l)" = $((2 * $(grep -c '`' <<< "$out"))) ]
check "a record can't add an image" lacks "!["
check "at most 20 tests a job" [ "$(grep -c '`case ' <<< "$out")" = 20 ]
out="$(bash scripts/ci/flake-report.sh comment "$WORK/hostile" "https://evil.example/x" 2>&1)"
check "a run link that isn't a run is refused" has "not a run link"
mkdir -p "$WORK/none"
out="$(bash scripts/ci/flake-report.sh comment "$WORK/none" "$url")"
check "no record, no comment" [ -z "$out" ]

# The workflows. job_block FILE JOB: the job's lines, up to the next job.
job_block() { awk -v job="  $2:" '$0 == job { on = 1; print; next } on && /^  [A-Za-z0-9_-]+:[[:space:]]*$/ { exit } on' "$1"; }
CI=.github/workflows/ci.yml
for job in pester pester-varied-setup repo-rules; do
  block="$(job_block "$CI" "$job")"
  check "$job's test step retries through retry-test.sh" grep -qF 'bash scripts/ci/retry-test.sh' <<< "$block"
  check "$job uploads its flake record" grep -qE 'name: flake-\$\{\{ github\.job \}\}' <<< "$block"
done
check "the check-script suites are what repo-rules retries" \
  grep -qE 'retry-test\.sh .*scripts/check/run-check-suites\.sh' <<< "$(job_block "$CI" repo-rules)"
check "repo-rules retries nothing else" [ "$(grep -c 'retry-test.sh' <<< "$(job_block "$CI" repo-rules)")" = 1 ]
for job in build security attest scripts timing; do
  check "$job never retries" bash -c '! grep -q "retry-test" <<< "$1"' _ "$(job_block "$CI" "$job")"
done
check "no job asks for issues: write in ci.yml" bash -c '! grep -qE "issues:[[:space:]]*write" "$1"' _ "$CI"

REPORT=.github/workflows/flake-report.yml
triggers="$(awk '/^on:/ { on = 1; next } on && /^[^[:space:]#]/ { on = 0 } on && /^  [a-z_]+:/ { print $1 }' "$REPORT")"
check "the report runs on workflow_run only" [ "$triggers" = "workflow_run:" ]
check "the report follows CI" grep -qE '^    workflows: \[CI\]$' "$REPORT"
perms="$(job_block "$REPORT" flake-report | awk '/^    permissions:/ { on = 1; next } on && /^    [^ ]/ { exit } on { print $1 $2 }')"
check "the report's token: contents and actions read, issues write, nothing else" \
  [ "$(sort <<< "$perms" | tr '\n' ' ')" = "actions:read contents:read issues:write " ]
check "the report checks out only main's script, with no credentials" \
  bash -c 'grep -qF "persist-credentials: false" "$1" && ! grep -qE "^[[:space:]]+ref:" "$1"' _ "$REPORT"
check "the report reads no secret" bash -c '! grep -qF "secrets." "$1"' _ "$REPORT"
check "the report splices no event field into a script, only into env" \
  bash -c '! awk "/run: \\|/ { on = 1; next } on && /^      - / { on = 0 } on" "$1" | grep -qF "\${{"' _ "$REPORT"
check "the report comments on #629" grep -qF 'gh issue comment 629' "$REPORT"

echo "flake-rule tests: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
