# GitHub ruleset workflows ("Require workflows to pass before merging")

- **Source:** GitHub's documentation source, repository `github/docs`, commit
  `39c1e014c75e972ab1fe531a81bc5bd42b543b07`, read with `gh api repos/github/docs/contents/<path>`:
  - `content/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets.md`
    (section "Require workflows to pass before merging")
  - `content/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/troubleshooting-rules.md`
    (section "Troubleshooting ruleset workflows")
  - `content/organizations/managing-organization-settings/creating-rulesets-for-repositories-in-your-organization.md`
  - `data/reusables/repositories/rulest-workflows-intro-paragraph.md`, `data/reusables/repositories/ruleset-workflow-event-triggers.md`,
    `data/reusables/actions/merge-group-event-with-required-checks.md`
  - Rendered at <https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets>.
- **Also:** `gh api orgs/Sour-Dev-Home/rulesets` (read only), the same day.
- **Saved:** 2026-11-18, for issue #330.
- **Game build:** none; this is about GitHub.

## What the docs say

Quoted from the files above.

- Where the rule lives: "Ruleset workflows can be configured at the organization or enterprise level to require
  workflows to pass before merging pull requests." "When you add this rule to a ruleset, in your organization settings,
  you specify the source repository and the workflow you want to enforce."
- Who can create organization rulesets: "For customers on GitHub Team or GitHub Enterprise plans you can create rulesets
  in your organization".
- Events: "Ruleset workflows support using the `pull_request`, `pull_request_target` and `merge_group` events." "Any
  filters you specify for the supported events are ignored - for example, `branches`, `branches-ignore`, `paths`,
  `types` and so on. The workflow is only triggered, and is always triggered, by the default activity types of the
  supported events." The defaults: `pull_request` and `pull_request_target` run on `opened`, `synchronize`, `reopened`;
  `merge_group` on `checks_requested`.
- Merge queue: workflows required "via organization rulesets" need `merge_group` as an added trigger, or "status
  checks will not be triggered" for the queue.
- Limits: "Ruleset workflows do not run on events triggered by the `GITHUB_TOKEN`." They can't use CodeQL's `init` and
  `analyze` actions. A rule added while a PR is open doesn't run on it until a new push, a branch update, or a close and
  reopen.
- Status checks, by contrast: "Required status checks do not take workflow, matrix, or event trigger types into
  account." A check named `<job name>` from any workflow counts.

The pages don't say which ref's copy of the workflow file a ruleset workflow runs, what token permissions it gets, or
whether a same-named job from the PR's own workflow satisfies it. [NEEDS VERIFICATION]

## What the API says for this organization

`gh api orgs/Sour-Dev-Home/rulesets` returned HTTP 403: "Upgrade to GitHub Team to enable this feature." The
organization is on the Free plan (`gh api orgs/Sour-Dev-Home --jq .plan.name` prints `free`), so it can't create an
organization ruleset, and the "Require workflows to pass before merging" rule exists only in organization (or
enterprise) rulesets.
