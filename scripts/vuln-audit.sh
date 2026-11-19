#!/usr/bin/env bash
# The vulnerability audit in CI's security job (#324): OSV-Scanner on the committed NuGet lock files, transitive
# packages included. It reads only those files, never a project, so it runs no MSBuild: a project or target a PR adds
# outside the security-review scope can't change what it reports. The build job restores in locked mode, which fails
# when a lock file no longer matches its project.
# Sources: docs/raw-sources/osv-scanner-and-nuget-lock-files-2026-11-19.md.
#
#   scripts/vuln-audit.sh install DIR    download the pinned OSV-Scanner for Linux x86-64 to DIR/osv-scanner, checked
#                                        against its SHA-256.
#   scripts/vuln-audit.sh scan SCANNER   from the repository: scan every committed packages.lock.json with SCANNER and
#                                        an empty config of our own, so no osv-scanner.toml in the tree can ignore a
#                                        vulnerability.
#
# Exit 0 when the scan is clean; 1 on a known vulnerability, a failed scan (fails closed on any scanner exit but 0), a
# project without a committed packages.lock.json beside it, a lock file under another name, or an MSBuild file other
# than the root Directory.Build.props that sets a lock-file property, or a committed .user or .rsp file; 2 on a usage
# error.
#
# To update OSV-Scanner: change the version and the hash below. GitHub shows each asset's SHA-256 on the release page.
set -uo pipefail

OSV_VERSION=2.7.1
OSV_SHA256_LINUX=fb7dab19a8bd9bbe5c3189f09d4b7ee1820c9553a23bac202648a56efd2f6ee8

die() {
  echo "vuln-audit: $*" >&2
  exit 2
}

usage() { die "usage: vuln-audit.sh install DIR | scan SCANNER"; }

fail() {
  if [ -n "${GITHUB_ACTIONS:-}" ]; then echo "::error::vuln-audit: $*"; else echo "vuln-audit: $*" >&2; fi
  exit 1
}

install() {
  local dir="$1" url="https://github.com/google/osv-scanner/releases/download/v$OSV_VERSION/osv-scanner_linux_amd64"
  mkdir -p "$dir" || die "could not create $dir"
  curl -fsSL --retry 3 -o "$dir/osv-scanner" "$url" || fail "could not download $url"
  echo "$OSV_SHA256_LINUX  $dir/osv-scanner" | sha256sum -c --quiet - || fail "SHA-256 mismatch for $url"
  chmod +x "$dir/osv-scanner"
  echo "installed OSV-Scanner $OSV_VERSION into $dir"
}

# lock_properties: fail when a committed file other than the root Directory.Build.props, in the review scope, could set
# a lock-file property: elsewhere one could point restore at another lock file (NuGetLockFilePath) or turn lock files
# off, while this audit reads a stale packages.lock.json. A project can import XML under any name, so every committed
# file is searched, binary ones (UTF-16) with their NUL and byte-order-mark bytes dropped. A file that names a property
# but isn't XML (a script, Markdown) passes: XML starts with "<" after an optional byte-order mark and white space, and
# MSBuild loads nothing else. A property name built at evaluation time would pass this text match; that's the review's.
lock_properties() {
  local pattern='NuGetLockFilePath|RestorePackagesWithLockFile|RestoreLockedMode' entry file first found=()
  while IFS= read -r file; do found+=("$file"); done < <(git grep --cached -l -I -i -E "$pattern")
  while IFS= read -r -d '' entry; do
    file="${entry#*$'\t'}"
    [[ "${entry%%$'\t'*}" == i/-text* ]] || continue
    git cat-file blob ":$file" | tr -d '\000\376\377' | grep -qiE "$pattern" && found+=("$file")
  done < <(git ls-files --eol -z)
  for file in "${found[@]}"; do
    [ "$file" != Directory.Build.props ] || continue
    # Byte-order-mark bytes (UTF-8 EF BB BF, UTF-16 FE FF) and white space go before the first character.
    first="$(git cat-file blob ":$file" | LC_ALL=C tr -d '\000\357\273\277\376\377 \t\r\n' | head -c 1)"
    [ "$first" != "<" ] || fail "$file sets a lock-file property; only the root Directory.Build.props may (#324)"
  done
}

scan() {
  # config stays global: the EXIT trap that removes it runs after scan returns.
  local scanner="$1" root file dir status locks=() args=() missing=()
  [ -x "$scanner" ] || die "no scanner at $scanner"
  root="$(git rev-parse --show-toplevel)" || die "not in a git repository"
  cd "$root" || die "could not enter $root"

  while IFS= read -r -d '' file; do locks+=("$file"); done < <(git ls-files -z -- ':(glob)**/packages.lock.json')
  # NuGet can be pointed at another lock file name; the audit reads only packages.lock.json, so it refuses the rest.
  while IFS= read -r -d '' file; do
    [ "${file##*/}" = packages.lock.json ] ||
      fail "$file: a lock file the audit doesn't read; name it packages.lock.json"
  done < <(git ls-files -z -- ':(glob,icase)**/packages*.lock.json')
  # A project without a committed lock file would leave its packages out of the audit.
  while IFS= read -r -d '' file; do
    dir="$(dirname "$file")"
    git ls-files --error-unmatch -- "$dir/packages.lock.json" > /dev/null 2>&1 || missing+=("$file")
  done < <(git ls-files -z -- ':(glob,icase)**/*.csproj' ':(glob,icase)**/*.fsproj' ':(glob,icase)**/*.vbproj')
  # A colon would make OSV-Scanner read --lockfile=a:b as parser a for file b.
  for file in "${locks[@]}"; do
    [[ "$file" != *:* ]] || fail "$file: a colon in a lock file's path; OSV-Scanner would read it as a parser name"
  done
  lock_properties
  # Visual Studio's per-user project files are imported by every build and never belong in the repository.
  while IFS= read -r -d '' file; do
    fail "$file: a per-user project file, which every build imports; don't commit it"
  done < <(git ls-files -z -- ':(glob,icase)**/*.user')
  # MSBuild applies a response file's switches as global properties, over Directory.Build.props; none is committed.
  while IFS= read -r -d '' file; do
    fail "$file: an MSBuild response file, whose -p: switches override the lock-file properties; don't commit it"
  done < <(git ls-files -z -- ':(glob,icase)**/*.rsp')
  [ "${#missing[@]}" -eq 0 ] ||
    fail "no committed packages.lock.json beside ${missing[*]}; restore it and commit the lock file"
  [ "${#locks[@]}" -gt 0 ] || fail "no committed packages.lock.json to audit"

  config="$(mktemp)" || die "could not create a temporary file"
  trap 'rm -f "$config"' EXIT
  for file in "${locks[@]}"; do args+=("--lockfile=$file"); done
  "$scanner" scan source "--config=$config" "${args[@]}"
  status=$?
  case "$status" in
    0) echo "vuln-audit: no known vulnerabilities in ${#locks[@]} lock files" ;;
    1) fail "a package has a known vulnerability; see the report above" ;;
    *) fail "the audit failed (OSV-Scanner exit $status); see the output above" ;;
  esac
}

case "${1:-}" in
  install) [ $# -eq 2 ] || usage; install "$2" ;;
  scan) [ $# -eq 2 ] || usage; scan "$2" ;;
  *) usage ;;
esac
