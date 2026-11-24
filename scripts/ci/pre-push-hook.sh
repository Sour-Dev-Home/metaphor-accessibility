#!/usr/bin/env bash
# CI's pre-push-hook job (#655): the repository's pre-push hook, run by a real `git push` as on a contributor's machine.
# Git then gives the hook its own environment (its exec path first on PATH, its GIT_* variables, the pushed refs on
# stdin), which the checks CI runs directly never see: #672's bug passed CI and failed every local push.
#
#   pre-push-hook.sh BASE HEAD   push BASE to a scratch bare remote with no hooks, then HEAD on top of it through
#                                scripts/githooks, so the hook takes BASE..HEAD as the pushed change. An empty BASE (the
#                                nightly run) starts from HEAD's root commit, so the hook takes every file as changed.
#
# Run from the repository root. Exits with the push's code, so the hook's failure fails the job; 2 when the push can't
# be set up. The remote lives in a temporary folder this script makes and removes; nothing is pushed anywhere else.
set -uo pipefail

die() { echo "pre-push-hook: $*" >&2; exit 2; }

[ "$#" -eq 2 ] && [ -n "$2" ] || die "usage: pre-push-hook.sh BASE HEAD"
base="$1"
head="$(git rev-parse --verify --quiet "$2^{commit}")" || die "HEAD $2 is not a commit here"
if [ -z "$base" ]; then
  base="$(git rev-list --max-parents=0 "$head" | tail -n 1)" || die "could not find the root commit of $head"
fi
base="$(git rev-parse --verify --quiet "$base^{commit}")" || die "BASE $1 is not a commit here (fetch the history)"
# A push that isn't a fast-forward is refused by the remote, which would read as the hook failing.
git merge-base --is-ancestor "$base" "$head" || die "BASE $base is not an ancestor of HEAD $head"
[ -f scripts/githooks/pre-push ] || die "no scripts/githooks/pre-push here; run from the repository root"

scratch="$(mktemp -d)" || die "could not make a temporary folder"
trap 'rm -rf "$scratch"' EXIT
remote="$scratch/remote.git"
ref=refs/heads/pre-push-hook
git init -q --bare "$remote" || die "could not make the scratch remote"
# --no-verify: the base is what the remote already has, not a push under test.
git push -q --no-verify "$remote" "$base:$ref" || die "could not push BASE to the scratch remote"
if [ "$base" = "$head" ]; then
  echo "pre-push-hook: BASE is HEAD, so the push changes nothing; pushing it to a second branch instead"
  ref=refs/heads/pre-push-hook-same
fi
echo "pre-push-hook: git push of $head over $base, through scripts/githooks"
# core.hooksPath on the command line, so no git config of the runner's or a contributor's changes which hook runs.
git -c core.hooksPath=scripts/githooks push "$remote" "$head:$ref"
