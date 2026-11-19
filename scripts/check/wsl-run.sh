#!/usr/bin/env bash
# Runs the check-script suites (tests/check/test-check.sh, tests/bash/*.sh) in WSL when it's set up (#341): Linux
# starts processes far faster than Git Bash, and the suites start thousands. Three ways in:
#
#   source "$ROOT/scripts/check/wsl-run.sh"          a suite's first step, once ROOT is set: in Git Bash with the
#                                             metaphor-checks distro and its tools, it runs the suite there with the
#                                             same arguments and exits with its code; anywhere else it returns, and
#                                             the suite runs where it is. METAPHOR_NO_WSL=1 opts out.
#   bash scripts/check/wsl-run.sh inside SUITE ARGS  in the distro, from the checkout's root: check the tools, print their
#                                             versions, run SUITE. Exit 125 when a tool is missing.
#   bash scripts/check/wsl-run.sh install [--update] in the distro as root, from the checkout's root (scripts/setup/setup-wsl.ps1):
#                                             install what scripts/setup/wsl-tools.txt lists and is missing; --update
#                                             upgrades.
#
# The suites read the Windows checkout through /mnt/c and keep their temporary repos in the distro's /tmp. Nothing here
# changes a file in the checkout or git's global or repo config: what WSL's git needs to read a Windows checkout is
# set up for the suite's own processes only (wsl_git_view).

wsl_distro=metaphor-checks
wsl_missing_code=125

# wsl_fallback REASON: the one-line notice when a suite stays in Git Bash.
wsl_fallback() {
  echo "notice: running in Git Bash, not WSL: $1. Set WSL up with scripts/setup/setup-wsl.ps1" \
    "(docs/contributors/how-to/set-up-a-clone.md#run-the-check-script-suites-in-wsl)."
}

# wsl_reexec ARGS...: the Git Bash side. Run the calling suite in the distro and exit with its code, or return.
wsl_reexec() {
  case "${OSTYPE:-}" in msys* | cygwin*) ;; *) return 0 ;; esac
  [ -z "${METAPHOR_NO_WSL:-}" ] || { wsl_fallback "METAPHOR_NO_WSL is set"; return 0; }
  command -v wsl.exe > /dev/null || { wsl_fallback "WSL is not installed"; return 0; }
  local list suite win_root code live_dir live_env=""
  # WSL_UTF8: wsl.exe prints UTF-16 otherwise. Bash drops the NUL bytes of an older one that ignores it, with a warning.
  { list="$(WSL_UTF8=1 wsl.exe --list --quiet)"; } 2> /dev/null
  [[ $'\n'"${list//$'\r'/}"$'\n' == *$'\n'"$wsl_distro"$'\n'* ]] ||
    { wsl_fallback "WSL has no $wsl_distro distro"; return 0; }
  suite="$(cd "${BASH_SOURCE[2]%/*}" && pwd)/${BASH_SOURCE[2]##*/}"
  [[ "$suite" == "$ROOT/"* ]] || { wsl_fallback "$suite is outside $ROOT"; return 0; }
  win_root="$(cd "$ROOT" && { pwd -W 2> /dev/null || pwd; })" # pwd -W: Git Bash's C:/... form
  # MSYS_NO_PATHCONV, MSYS2_ARG_CONV_EXCL: pass the arguments to wsl.exe as they are, not as Windows paths. WSLENV
  # carries the job count over, and this shell's pid for the live log (scripts/check/live-log.sh), so that
  # scripts/check/check-status.sh, which checks it in Git Bash, sees the run as going on. A caller's live log (`full`, #336)
  # goes over in C:/... form, which WSL translates (/p). scripts/check/run-check-suites.sh decides which suite gets them.
  if [ -n "${METAPHOR_LIVE_LOG:-}" ] &&
    live_dir="$(cd "${METAPHOR_LIVE_LOG%/*}" 2> /dev/null && { pwd -W 2> /dev/null || pwd; })"; then
    METAPHOR_LIVE_LOG="$live_dir/${METAPHOR_LIVE_LOG##*/}" live_env=":METAPHOR_LIVE_LOG/p"
  fi
  # The worker pools' size (scripts/check/pool.sh) is set here, where the build slots' locks are visible; the distro
  # has its own /tmp and can't see them.
  if [ -z "${METAPHOR_TEST_JOBS:-}" ]; then
    # shellcheck source=pool.sh
    source "$ROOT/scripts/check/pool.sh" && METAPHOR_TEST_JOBS="$(pool_default_jobs)"
  fi
  METAPHOR_TEST_JOBS="${METAPHOR_TEST_JOBS:-}" METAPHOR_LIVE_PID=$$ MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*' \
    WSLENV="METAPHOR_TEST_JOBS:METAPHOR_LIVE_PID$live_env${WSLENV:+:$WSLENV}" \
    wsl.exe --distribution "$wsl_distro" --cd "$win_root" --exec /bin/bash scripts/check/wsl-run.sh inside \
    "${suite#"$ROOT/"}" "$@"
  code=$?
  # A missing tool: the distro side has printed the notice.
  [ "$code" -eq "$wsl_missing_code" ] || exit "$code"
}

# wsl_tools: print scripts/setup/wsl-tools.txt's "<command> <source>" lines, comments left out.
wsl_tools() {
  local cmd source
  while read -r cmd source; do
    [[ -z "$cmd" || "$cmd" == \#* ]] || echo "$cmd $source"
  done < scripts/setup/wsl-tools.txt
}

# wsl_versions: print one line naming the distro's Debian release and each tool's version.
wsl_versions() {
  local cmd source packages=() docs=() release=""
  while read -r cmd source; do
    if [ "$source" = install-docs-tools ]; then
      docs+=("$("$cmd" --version 2> /dev/null | head -n 1)")
    else
      packages+=("$source")
    fi
  done < <(wsl_tools)
  [ ! -r /etc/debian_version ] || read -r release < /etc/debian_version
  echo "wsl: $wsl_distro, Debian $release, bash $BASH_VERSION;" \
    "$(dpkg-query -W -f='${Package} ${Version}, ' "${packages[@]}" 2> /dev/null)${docs[*]/%/,}" | sed 's/,$//'
}

# wsl_git_view SUITE ARGS...: run SUITE so that WSL's git can read the checkout, changing nothing on disk. A main
# checkout needs nothing: .gitattributes makes every file LF and Git for Windows writes core.filemode=false into each
# clone's config, so git sees no change, and the distro's root owns /mnt/c, so safe.directory has nothing to refuse.
# A worktree's .git file names its git dir as a Windows path (gitdir: C:/...), which Linux git reads as relative and
# can't find; the suite then runs in its own mount namespace, where a Linux copy of that file is bound over .git.
# Nothing outside the suite's processes sees it, and Windows never does.
wsl_git_view() {
  local line gitdir file
  line=""
  [ ! -f .git ] || read -r line < .git
  gitdir="${line#gitdir: }"
  [[ "$gitdir" =~ ^[A-Za-z]:/ ]] || exec bash "$@"
  file="$(mktemp)" && printf 'gitdir: %s\n' "$(wslpath -u "$gitdir")" > "$file" || exit 2
  exec unshare --mount --propagation private /bin/bash -c \
    'mount --bind "$1" .git && rm -f "$1" && shift && exec bash "$@"' bind "$file" "$@"
}

# wsl_inside SUITE ARGS...: the distro side. Run SUITE, from the checkout's root, when every tool is present.
wsl_inside() {
  local cmd source missing=()
  # Linux's own folders only: the Windows PATH WSL appends would be slow to search, and has Windows tools in it.
  export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
  while read -r cmd source; do
    command -v "$cmd" > /dev/null || missing+=("$cmd")
  done < <(wsl_tools)
  if [ "${#missing[@]}" -gt 0 ]; then
    wsl_fallback "$wsl_distro lacks ${missing[*]}"
    exit "$wsl_missing_code"
  fi
  wsl_versions
  # The job count is the Git Bash side's (wsl_reexec); run directly, the pool's default applies (pool.sh).
  wsl_git_view "$@"
}

# wsl_install [--update]: install the listed tools that are missing, as root in the distro; --update upgrades them all.
wsl_install() {
  local cmd source packages=() docs=""
  [ "$(id -u)" -eq 0 ] || { echo "wsl-run install: run it as root" >&2; exit 2; }
  export DEBIAN_FRONTEND=noninteractive
  while read -r cmd source; do
    if [ "$1" = --update ] || ! command -v "$cmd" > /dev/null; then
      if [ "$source" = install-docs-tools ]; then docs=1; else packages+=("$source"); fi
    fi
  done < <(wsl_tools)
  if [ "$1" = --update ]; then
    apt-get update -q && apt-get upgrade -y -q || exit 1
  elif [ "${#packages[@]}" -gt 0 ]; then
    apt-get update -q || exit 1
  fi
  if [ "${#packages[@]}" -gt 0 ]; then
    apt-get install -y -q --no-install-recommends "${packages[@]}" || exit 1
  fi
  if [ -n "$docs" ]; then
    bash scripts/setup/install-docs-tools.sh --bin /usr/local/bin || exit 1
  fi
  [ "${#packages[@]}" -gt 0 ] || [ -n "$docs" ] || echo "wsl-run install: every tool is already there"
}

if [ "${BASH_SOURCE[0]}" != "$0" ]; then
  wsl_reexec "$@"
else
  set -uo pipefail
  case "${1:-}" in
    inside) shift; wsl_inside "$@" ;;
    install) shift; wsl_install "${1:-}" ;;
    *) echo "usage: wsl-run.sh inside SUITE [ARGS...] | install [--update]" >&2; exit 2 ;;
  esac
fi
