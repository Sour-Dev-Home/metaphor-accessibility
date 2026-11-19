#!/usr/bin/env bash
# Black-box tests for scripts/ci/vuln-audit.sh scan (#324): the vulnerability audit reads only committed lock files, with
# our own scanner config, so a project file or a config the PR plants can't change what it reports. A fake scanner
# records its arguments; the real one runs only in CI.
set -uo pipefail
unset GITHUB_ACTIONS

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
AUDIT="$ROOT/scripts/ci/vuln-audit.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

passed=0
failed=0

# The fake scanner: prints its arguments one per line to $WORK/args and exits with $FAKE_OSV_CODE.
SCANNER="$WORK/osv-scanner"
cat > "$SCANNER" <<'EOF'
#!/usr/bin/env bash
printf '%s\n' "$@" > "$FAKE_OSV_ARGS"
exit "${FAKE_OSV_CODE:-0}"
EOF
chmod +x "$SCANNER"
export FAKE_OSV_ARGS="$WORK/args"

# A fake dotnet that leaves a mark, so a test can tell the audit never built or restored anything.
mkdir -p "$WORK/bin"
printf '#!/usr/bin/env bash\ntouch "%s/dotnet-ran"\n' "$WORK" > "$WORK/bin/dotnet"
chmod +x "$WORK/bin/dotnet"
PATH="$WORK/bin:$PATH"

run() { # run ARGS...: run the audit in the test repo, capturing its exit code and output
  rm -f "$FAKE_OSV_ARGS"
  out="$(cd "$WORK/repo" && bash "$AUDIT" "$@" 2>&1)"
  code=$?
}

expect() { # expect NAME CODE [TEXT...]: the last run exited with CODE and printed every TEXT
  local name="$1" want="$2" text ok=1
  shift 2
  [ "$code" -eq "$want" ] || ok=0
  for text in "$@"; do [[ "$out" == *"$text"* ]] || ok=0; done
  if [ "$ok" -eq 1 ]; then
    passed=$((passed + 1))
  else
    failed=$((failed + 1))
    printf 'FAIL %s\n  want exit %s and: %s\n  got exit %s:\n%s\n' "$name" "$want" "$*" "$code" "$out"
  fi
}

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}

lock='{"version": 2, "dependencies": {"net8.0": {}}}'

# new_repo: a repo with two projects and their committed lock files.
new_repo() {
  rm -rf "$WORK/repo"
  mkdir -p "$WORK/repo/src/App" "$WORK/repo/tests/App.Tests"
  cd "$WORK/repo" || exit 1
  git init -q
  git config user.email t@example.invalid
  git config user.name t
  git config core.autocrlf false
  echo '<Project Sdk="Microsoft.NET.Sdk" />' > src/App/App.csproj
  echo '<Project Sdk="Microsoft.NET.Sdk" />' > tests/App.Tests/App.Tests.csproj
  echo "$lock" > src/App/packages.lock.json
  echo "$lock" > tests/App.Tests/packages.lock.json
  git add .
  git commit -q -m init
  cd "$ROOT" || exit 1
}

# scanned: the lock files and the config the last scan was given, one per line.
scanned() { grep -E '^--(lockfile|config)=' "$FAKE_OSV_ARGS" 2>/dev/null | sed -E 's/^--config=.*/--config/' | sort; }

new_repo
run scan "$SCANNER"
expect "a clean scan passes" 0
check "a clean scan prints no shell error" bash -c '[[ "$1" != *"unbound variable"* ]]' _ "$out"
baseline="$(scanned)"
check "the scan reads each committed lock file" \
  [ "$(grep -c '^--lockfile=' <<<"$baseline")" -eq 2 ]
check "the scan names src/App's lock file" grep -qxF -- '--lockfile=src/App/packages.lock.json' <<<"$baseline"
check "the scan runs scan source" grep -qxF 'source' "$FAKE_OSV_ARGS"
config="$(sed -nE 's/^--config=(.*)$/\1/p' "$FAKE_OSV_ARGS")"
check "the scan passes its own config, from outside the repository" \
  bash -c '[ -n "$1" ] && [[ "$1" != "$2"* ]]' _ "$config" "$WORK/repo"

# What a PR could plant outside the security-review scope: a target that rewrites the restore output, a scanner config
# next to a lock file that ignores everything, and an untracked lock file.
new_repo
cat > "$WORK/repo/src/App/App.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <Target Name="Hide" AfterTargets="Restore">
    <WriteLinesToFile File="obj/project.assets.json" Lines="{}" Overwrite="true" />
  </Target>
</Project>
EOF
printf '[[PackageOverrides]]\nignore = true\n' > "$WORK/repo/src/App/osv-scanner.toml"
mkdir -p "$WORK/repo/stray"
echo "$lock" > "$WORK/repo/stray/packages.lock.json"
git -C "$WORK/repo" add src
git -C "$WORK/repo" commit -q -m plant
rm -f "$WORK/dotnet-ran"
run scan "$SCANNER"
expect "a planted target and scanner config leave a clean scan passing" 0
check "a planted target and scanner config don't change what the scan reads" [ "$(scanned)" = "$baseline" ]
check "the audit never runs dotnet" [ ! -e "$WORK/dotnet-ran" ]
check "the planted scanner config isn't the one passed" \
  bash -c '! grep -q "osv-scanner.toml" "$1"' _ "$FAKE_OSV_ARGS"

new_repo
FAKE_OSV_CODE=1 run scan "$SCANNER"
expect "a known vulnerability fails the audit" 1 "known vulnerability"
FAKE_OSV_CODE=127 run scan "$SCANNER"
expect "a scanner error fails the audit" 1 "exit 127"
FAKE_OSV_CODE=128 run scan "$SCANNER"
expect "a scan that finds no packages fails the audit" 1 "exit 128"

# A project without a committed lock file would leave its packages out of the audit.
new_repo
mkdir -p "$WORK/repo/tools/Extra"
echo '<Project Sdk="Microsoft.NET.Sdk" />' > "$WORK/repo/tools/Extra/Extra.csproj"
git -C "$WORK/repo" add tools
run scan "$SCANNER"
expect "a project without a committed lock file fails the audit" 1 "tools/Extra/Extra.csproj" "packages.lock.json"
check "the scanner doesn't run when a lock file is missing" [ ! -e "$FAKE_OSV_ARGS" ]

# Only the root Directory.Build.props, in the review scope, may set the lock-file properties: elsewhere one could point
# restore at another lock file, or turn lock files off, while the audit reads a stale packages.lock.json.
new_repo
printf '<Project>\n  <PropertyGroup>\n    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>\n  </PropertyGroup>\n</Project>\n' \
  > "$WORK/repo/Directory.Build.props"
git -C "$WORK/repo" add .
run scan "$SCANNER"
expect "the root Directory.Build.props may set the lock-file properties" 0
for plant in "src/App/App.csproj:<NuGetLockFilePath>other.json</NuGetLockFilePath>" \
  "src/Directory.Build.props:<RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>" \
  "tests/App.Tests/App.Tests.csproj:<restorelockedmode>false</restorelockedmode>" \
  "build/Extra.targets:<NuGetLockFilePath>x</NuGetLockFilePath>" \
  "build/lock.xml:<NuGetLockFilePath>deps.json</NuGetLockFilePath>" \
  "src/App/App.csproj.user:<NuGetLockFilePath>deps.json</NuGetLockFilePath>"; do
  new_repo
  file="${plant%%:*}"
  mkdir -p "$WORK/repo/$(dirname "$file")"
  printf '<Project><PropertyGroup>%s</PropertyGroup></Project>\n' "${plant#*:}" > "$WORK/repo/$file"
  git -C "$WORK/repo" add .
  run scan "$SCANNER"
  expect "a lock-file property in $file fails the audit" 1 "$file" "lock-file"
  check "the scanner doesn't run when $file sets a lock-file property" [ ! -e "$FAKE_OSV_ARGS" ]
done

# MSBuild loads any XML file a project imports, whatever its name, and reads UTF-16 too.
new_repo
printf '\xff\xfe' > "$WORK/repo/build.xml"
printf '<Project><PropertyGroup><RestoreLockedMode>false</RestoreLockedMode></PropertyGroup></Project>\n' |
  sed 's/./&\x00/g' >> "$WORK/repo/build.xml"
git -C "$WORK/repo" add .
run scan "$SCANNER"
expect "a lock-file property in a UTF-16 XML file fails the audit" 1 "build.xml" "lock-file"

# A script or Markdown file may name the properties: MSBuild can't import what isn't XML.
new_repo
mkdir -p "$WORK/repo/scripts" "$WORK/repo/docs"
printf '#!/usr/bin/env bash\n# NuGetLockFilePath RestoreLockedMode\n' > "$WORK/repo/scripts/note.sh"
printf '# Notes\n\nSet `<RestorePackagesWithLockFile>` in Directory.Build.props.\n' > "$WORK/repo/docs/notes.md"
git -C "$WORK/repo" add .
run scan "$SCANNER"
expect "a script or Markdown file naming a lock-file property passes" 0

# Visual Studio's per-user project files are imported by every build, and never belong in the repository.
new_repo
echo '<Project />' > "$WORK/repo/src/App/App.csproj.user"
git -C "$WORK/repo" add .
run scan "$SCANNER"
expect "a committed .user file fails the audit" 1 "src/App/App.csproj.user"

# MSBuild applies a response file's switches as global properties, over Directory.Build.props.
for rsp in Directory.Build.rsp MSBuild.rsp src/App/Directory.Build.rsp src/App/msbuild.RSP; do
  new_repo
  echo '-p:NuGetLockFilePath=other.lock.json' > "$WORK/repo/$rsp"
  git -C "$WORK/repo" add .
  run scan "$SCANNER"
  expect "a committed $rsp fails the audit" 1 "$rsp" "response file"
done

# OSV-Scanner reads --lockfile=a:b as parser a for file b, so a colon in a path could switch the parser.
new_repo
# Staged without a file, and with core.protectNTFS off: Git for Windows refuses a colon in a path otherwise.
blob="$(git -C "$WORK/repo" hash-object -w src/App/packages.lock.json)"
git -C "$WORK/repo" -c core.protectNTFS=false update-index --add --cacheinfo "100644,$blob,odd:dir/packages.lock.json"
blob="$(git -C "$WORK/repo" hash-object -w src/App/App.csproj)"
git -C "$WORK/repo" -c core.protectNTFS=false update-index --add --cacheinfo "100644,$blob,odd:dir/Odd.csproj"
run scan "$SCANNER"
expect "a lock file path with a colon fails the audit" 1 "odd:dir/packages.lock.json"
check "the scanner doesn't run on a lock file path with a colon" [ ! -e "$FAKE_OSV_ARGS" ]

new_repo
git -C "$WORK/repo" rm -q -r src tests
run scan "$SCANNER"
expect "no lock files at all fails the audit" 1 "no committed packages.lock.json"

new_repo
run scan
expect "scan without a scanner is a usage error" 2 "usage"
run nope
expect "an unknown command is a usage error" 2 "usage"
run scan "$WORK/missing-scanner"
expect "a missing scanner is a usage error" 2 "missing-scanner"

printf 'vuln-audit tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
