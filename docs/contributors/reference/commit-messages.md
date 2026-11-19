# Commit messages

The shape every commit message must have, as `scripts/check.sh` enforces it. The `commit-msg` hook checks one message
before the commit; CI's `commits` stage checks every non-merge commit in a PR. The code is `check_message` in
[`scripts/check/message.sh`](../../../scripts/check/message.sh). For the stages around it, see [checks](checks.md).

## Subject

The first line. Both rules below apply.

- **A type prefix.** One of `feat`, `fix`, `docs`, `test`, `refactor`, `perf`, `build`, `ci`, `chore`, `style` or
  `revert`, then an optional scope in parentheses, an optional `!` for a breaking change, a colon, a space, and text
  that starts with a non-space character.
  - `feat: add the dialogue window hook`
  - `fix(check): count UTF-8 characters, not bytes`
  - `refactor(hooks)!: rename the loader entry point`
  - Rejected: `Add the hook`, `Fix: x` (type is case-sensitive), `feat(): x` (empty scope), `feat:x` (no space).
- **At most 72 characters.** Counted in UTF-8 characters, not bytes, so an accent costs one.
- **Subjects git writes itself pass whole**, with no prefix or length check: those starting with `Revert "`, `fixup!`,
  `squash!` or `amend!` (each followed by a space).

## Body

Optional. After the subject, one blank line, then the body.

- **The blank line is required.** Text on the line right after the subject fails with "leave a blank line after the
  subject".
- **At most 5 lines.** Only non-blank lines count, so blank lines between paragraphs are free. The body says why; link
  rather than restate.
- **Comment lines and below are ignored.** Lines starting with `#` and everything under a `# ---- >8 ----` scissors line
  are dropped first, as git does. So are carriage returns and trailing spaces.
- **An empty message fails.**

## Trailers

A final paragraph in which every line reads `Key: value` is a trailer block, and it doesn't count towards the 5 lines,
even when it is the whole body.

```text
docs(contributors): commit-message reference page (#418)

The rules lived only in check.sh's header.
Linked from the workflow and contributing pages.

Co-Authored-By: <co-author name and address>
```

- **`Co-Authored-By:`** records an AI or human co-author. The check doesn't require it. The agent workflow asks agents to
  add the trailer their session's attribution reminder gives ([workflow](../../agents/workflow.md#commit)).
- **`Security-Review: <hash>`** is added by the `commit-msg` hook, not by hand, on a commit that touches the
  security-review scope (`scripts/security-stamp.sh`). It needs the `security-reviewer` agent's stamp to match the
  staged scope. The hook replaces any such trailer already in the message. Details:
  [CONTRIBUTING](../../../.github/CONTRIBUTING.md#security-review-and-deny-rules).
- **`Security-Review-Skipped: <reason>`** is written instead when the maintainer commits with `SECURITY_REVIEW_SKIP`. On
  any PR not by the maintainer, CI ignores a skip trailer.
- A clean merge from `main` gets no review trailer ([ADR 0012](../../adr/0012-review-trailer-covers-clean-merges.md)).

## Merge commits

- Locally, the `commit-msg` hook checks a merge's message like any other, so git's default `Merge branch 'x'` fails for
  lacking a type prefix. Give it one: `chore: merge main into 418-commit-messages-page`.
- In CI, `commits` skips merge commits (`git rev-list --no-merges`), since PRs are squash-merged and never reach `main`.
  CI still judges a PR's HEAD for the security-review trailer, merge or not.
