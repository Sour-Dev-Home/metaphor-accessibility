# GitHub code-owner review, ruleset bypass and the merge queue

- **Source:** the files and pages below.
  - GitHub's documentation source, repository `github/docs`, commit `39c1e014c75e972ab1fe531a81bc5bd42b543b07`, read
    with `gh api repos/github/docs/contents/<path>`:
    - `data/reusables/gated-features/repo-rules.md`
    - `content/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners.md`
    - `content/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets.md`
    - `data/reusables/repositories/rulesets-bypass-step.md`, `data/reusables/repositories/rulesets-branch-tag-bypass-optional-step.md`
    - `content/migrations/troubleshooting/setting-ruleset-bypasses-for-repository-migrations.md`
  - Community reports, not GitHub documentation:
    - <https://github.com/github/docs/issues/45265>, opened 2026-07-23, open, labelled "Waiting on SME review".
    - <https://github.com/orgs/community/discussions/208718>, posted 2026-09-24.
  - `gh api repos/Sour-Dev-Home/metaphor-accessibility/rulesets` (read only): rulesets `main` and `v tags`, both active.
- **Saved:** 2026-11-18, for issue #330.
- **Game build:** none; this is about GitHub.

## What GitHub's docs say

- Availability: "Rulesets are available in public repositories with GitHub Free and GitHub Free for organizations".
  This repository's `main` ruleset is active.
- Code-owner review: "Optionally, you can choose to require reviews from code owners. If you do, any pull request that
  modifies content with a code owner must be approved by that code owner before the pull request can be merged into
  the protected branch." The docs don't say whether this holds with 0 required approvals. [NEEDS VERIFICATION]
- CODEOWNERS comes from the base branch: "To trigger review requests, pull requests use the version of `CODEOWNERS`
  from the base branch of the pull request." For a PR from a fork, "the pull request will use the `CODEOWNERS` file
  from that branch in the upstream repository".
- Bypass: repository admins can be bypass actors. With "For pull requests only", "The actor can then choose to bypass
  any branch protections and merge that pull request."
- Bypass modes: "**Always allow**: The ruleset will be evaluated and the selected actor(s) will be prompted to bypass".
  "**Exempt**: The ruleset will not be evaluated and no bypass prompt will be shown". This page is about migrations
  and is versioned for GitHub Enterprise Cloud only.
- None of the pages says how a bypass interacts with auto-merge or the merge queue.

## What the community reports say

- docs#45265: "the bypass grant is only honored by a synchronous, direct merge call"; auto-merge doesn't honour it. A
  GitHub Docs writer replied on 2026-08-05 that they were "asking the repositories team"; there's no answer since.
- Discussion #208718: a bypass actor's PR under auto-merge "stays blocked with "Review required" forever"; enqueueing
  is refused ("At least 1 approving review is required by reviewers with write access"). The author relays,
  second-hand, that GitHub support called this intended. Commenters report the "Exempt" bypass mode works for
  auto-merge. [NEEDS VERIFICATION]

## What this means for #330

A PR author can't approve their own PR, so the maintainer can't give a code-owner review on their own or on agents'
PRs (they're opened from the maintainer's account). Under an "Always allow" bypass, those PRs would merge only by a
direct admin merge that skips the merge queue, not by auto-merge or the queue.
