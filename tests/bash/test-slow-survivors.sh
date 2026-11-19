#!/usr/bin/env bash
# Tests tools/slow/survivors.mjs: the Markdown list of surviving mutants the slow suite puts on its run page.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
SCRIPT="$ROOT/tools/slow/survivors.mjs"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
passed=0
failed=0

check() {
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}

# A report in Stryker's JSON shape (mutation-testing-report-schema): two survivors, one killed.
cat > "$tmp/report.json" <<'EOF'
{
  "schemaVersion": "1",
  "thresholds": { "high": 80, "low": 60 },
  "files": {
    "src/B.cs": { "language": "cs", "source": "", "mutants": [
      { "id": "3", "mutatorName": "Equality mutation", "replacement": "a != b", "status": "Survived",
        "location": { "start": { "line": 9, "column": 1 }, "end": { "line": 9, "column": 7 } } }
    ] },
    "src/A.cs": { "language": "cs", "source": "", "mutants": [
      { "id": "1", "mutatorName": "Boolean mutation", "replacement": "x | y", "status": "Survived",
        "location": { "start": { "line": 4, "column": 1 }, "end": { "line": 4, "column": 5 } } },
      { "id": "2", "mutatorName": "String mutation", "status": "Killed",
        "location": { "start": { "line": 5, "column": 1 }, "end": { "line": 5, "column": 5 } } }
    ] }
  }
}
EOF

node "$SCRIPT" "$tmp/report.json" > "$tmp/out.md"
expected="## Surviving mutants (2)

| File | Line | Mutator | Replacement |
|---|---|---|---|
| src/A.cs | 4 | Boolean mutation | \`x \\| y\` |
| src/B.cs | 9 | Equality mutation | \`a != b\` |"
check "lists each survivor by file and line, escaping pipes" [ "$(cat "$tmp/out.md")" = "$expected" ]

echo '{ "schemaVersion": "1", "thresholds": { "high": 80, "low": 60 }, "files": {} }' > "$tmp/none.json"
check "says so when no mutant survived" [ "$(node "$SCRIPT" "$tmp/none.json")" = "## Surviving mutants (0)

None." ]

# Stryker records absolute paths; the list shows them relative to the root given, so no local path reaches the page.
sed -e 's#"src/A.cs"#"C:\\\\work\\\\repo\\\\src\\\\A.cs"#' "$tmp/report.json" > "$tmp/absolute.json"
check "shows paths relative to the root, with forward slashes" \
  grep -qF '| src/A.cs | 4 |' <(node "$SCRIPT" "$tmp/absolute.json" 'C:\work\repo')

node "$SCRIPT" "$tmp/missing.json" > /dev/null 2>&1
check "fails when the report can't be read" [ $? -ne 0 ]

echo "slow-survivors tests: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
