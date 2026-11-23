#!/usr/bin/env bash
# Builds the players' zip (#308, #310) and installer exe (#608, #613): the one recipe CI's Windows gate and the release
# workflow both run, so a release is built exactly as every main build is.
#
#   scripts/release/build-release.sh [--no-build] NAME OUT_DIR
#
# 1. Packages the mod into OUT_DIR/mod (scripts/release/package-mod.sh; --no-build packages the build `check.sh full` made).
# 2. Fails unless Metaphor.Hooks.dll names this checkout's commit (HEAD) as a clean build.
# 3. Zips the package reproducibly into OUT_DIR/release/NAME.zip, stamped with the commit's time
#    (scripts/release/zip-package.ps1), the launcher players open at its top level (#604).
# 4. Builds the installer exe around that zip into OUT_DIR/release/NAME-installer.exe (scripts/release/installer-exe.ps1),
#    and writes OUT_DIR/release/SHA256SUMS with the zip's line and the exe's.
# 5. Rebuilds from scratch in a second checkout at another path, with that checkout's own scripts, and fails unless the
#    package, the zip and the exe all match.
#
# Prints the zip's SHA-256 last; progress goes to stderr, and to the run summary when GITHUB_STEP_SUMMARY is set.
# Exit codes: 0 built and matched; 1 a step failed or the rebuild differs; 2 usage (NAME must be a plain file name).
set -euo pipefail

build_flag=()
if [ "${1:-}" = --no-build ]; then
  build_flag=(--no-build)
  shift
fi
name="${1:-}"
out="${2:-}"
if [ -z "$out" ] || [[ ! "$name" =~ ^[A-Za-z0-9][A-Za-z0-9._-]*$ ]]; then
  echo "usage: scripts/release/build-release.sh [--no-build] NAME OUT_DIR (NAME: letters, digits, '.', '_', '-')" >&2
  exit 2
fi

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
summary="${GITHUB_STEP_SUMMARY:-/dev/null}"
say() { echo "$1" >&2; echo "$1" >> "$summary"; }

commit="$(git -C "$root" rev-parse HEAD)"
time="$(git -C "$root" log -1 --format=%ct HEAD)"
mkdir -p "$out"
out="$(cd "$out" && pwd)"

package_sha="$(bash "$root/scripts/release/package-mod.sh" "${build_flag[@]}" "$out/mod")"
if ! grep -aq "+$commit" "$out/mod/Metaphor.Hooks.dll" || grep -aq "+$commit-dirty" "$out/mod/Metaphor.Hooks.dll"; then
  echo "build-release: the packaged mod doesn't name commit $commit as a clean build" >&2
  exit 1
fi
say "Packaged mod SHA-256: \`$package_sha\` (commit \`$commit\`)"

rm -rf "$out/release"
zip_sha="$(pwsh -NoProfile -File "$root/scripts/release/zip-package.ps1" -Path "$out/mod" \
  -Destination "$out/release/$name.zip" -Timestamp "$time")"
say "Players' zip: \`$name.zip\`, SHA-256 \`$zip_sha\` (commit \`$commit\`)"
exe_sha="$(pwsh -NoProfile -File "$root/scripts/release/installer-exe.ps1" -Zip "$out/release/$name.zip" \
  -Destination "$out/release/$name-installer.exe")"
# The zip's line first: the players' how-to compares the zip with SHA256SUMS' first hash
# (docs/players/how-to/check-a-download.md).
printf '%s  %s\n%s  %s\n' "$zip_sha" "$name.zip" "$exe_sha" "$name-installer.exe" > "$out/release/SHA256SUMS"
say "Players' installer: \`$name-installer.exe\`, SHA-256 \`$exe_sha\` (commit \`$commit\`)"

# The second checkout shares this repository's objects, so it costs no fetch; it goes again whatever happens.
scratch="$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/build-release.XXXXXX")"
cleanup() {
  git -C "$root" worktree remove --force "$scratch/rebuild" > /dev/null 2>&1 || true
  rm -rf "$scratch"
}
trap cleanup EXIT
git -C "$root" worktree add -q --detach "$scratch/rebuild" "$commit" >&2
second_sha="$(bash "$scratch/rebuild/scripts/release/package-mod.sh" "$scratch/rebuild-out")"
if [ "$second_sha" != "$package_sha" ]; then
  echo "build-release: Rebuild differs: $package_sha then $second_sha" >&2
  exit 1
fi
second_zip="$(pwsh -NoProfile -File "$scratch/rebuild/scripts/release/zip-package.ps1" -Path "$scratch/rebuild-out" \
  -Destination "$scratch/rebuild.zip" -Timestamp "$time")"
if [ "$second_zip" != "$zip_sha" ]; then
  echo "build-release: Rebuild differs: the zip was $zip_sha, then $second_zip" >&2
  exit 1
fi
second_exe="$(pwsh -NoProfile -File "$scratch/rebuild/scripts/release/installer-exe.ps1" -Zip "$scratch/rebuild.zip" \
  -Destination "$scratch/rebuild-installer.exe")"
if [ "$second_exe" != "$exe_sha" ]; then
  echo "build-release: Rebuild differs: the installer exe was $exe_sha, then $second_exe" >&2
  exit 1
fi
say "A rebuild in a second checkout at another path matches, package, zip and installer exe."

echo "$zip_sha"
