# Add or update a GitHub Action

Every `uses:` line in `.github/workflows/` names an action by its full commit SHA, not by a tag such as `v4`. A tag can
be moved to point at different code later, but a commit SHA can't. The repository settings enforce this: a workflow
that names an action by tag or branch fails to start.

```yaml
# Refused: the tag can move.
- uses: actions/checkout@v7
# Accepted: the full 40-character commit SHA, with the version as a comment for people reading it.
- uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
```

## Pin an action

1. Pick the release you want on the action's GitHub page, for example `v7.0.1`.
2. Look up the commit that tag points to:

   ```sh
   gh api repos/actions/checkout/commits/v7.0.1 --jq .sha
   ```

   For an action in a subfolder, such as `github/codeql-action/init`, look up the repository part only
   (`github/codeql-action`).
3. Write `uses: <owner>/<repo>@<sha> # <tag>`. The comment is the version Dependabot reads and updates.

Updates need no hand edits: Dependabot opens a weekly PR that bumps the SHA and its comment
([`dependabot.yml`](../../../.github/dependabot.yml)).

## Which actions are allowed

The repository runs only GitHub's own actions (`actions/*`, `github/*`) and `ossf/scorecard-action`. Any other action
fails to start until the maintainer adds it to the allowed list in the repository's Actions settings. Before you write
a workflow around a new action, open an issue that names the action and why it's needed.

A change under `.github/workflows/` is in the security-review scope
([security review](../reference/security-review.md#scope)), so it needs a review stamp
like any other guard file.
