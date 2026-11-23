# Set up a clone

From a fresh clone to passing checks in about 30 minutes, most of it installing the .NET SDK. You don't need the game
for this. The tools and their pins are listed in the [start-here map](../README.md#tool-stack).

## One command

On a fresh Windows install, Windows PowerShell's default execution policy blocks local scripts. Allow them once for
your user (no admin needed; `RemoteSigned` is enough because `git clone` doesn't mark files as downloaded). See
Microsoft's
[about_Execution_Policies](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_execution_policies):

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

Optional: install WSL 2 first, so `check.sh full` runs its check-script suites in a Linux distro instead of Git Bash
(about 4 minutes faster). In PowerShell run as administrator, then restart Windows
([Microsoft's guide](https://learn.microsoft.com/windows/wsl/install)):

```powershell
wsl --install
```

Onboard then sets up the distro the checks use. Without it, everything still works, only slower.

Then, in PowerShell, from the root of your clone:

```powershell
.\scripts\setup\onboard.ps1
```

It does steps 1 to 3 below and the rest of the setup, asks before installing anything, and ends with a ready summary
and a prompt to paste into Claude. It's safe to run again. Its header in
[`scripts/setup/onboard.ps1`](../../../scripts/setup/onboard.ps1) lists each step; the steps below are for doing them by
hand.

## 1. Install the tools

On Windows:

- [Git for Windows](https://git-scm.com/download/win), which includes Git Bash. Every script runs in Git Bash.
  In PowerShell, plain `bash` may start WSL's Linux bash instead, which has no git and fails with
  `git: command not found`. Open a Git Bash window, or call it by path. Git names its own install, wherever it is
  (a user-scope, Scoop or Chocolatey install too): `git --exec-path` is its `libexec\git-core` folder, three levels
  below the Git root that holds `bin\bash.exe`:

  ```powershell
  $bash = Join-Path (git --exec-path) '..\..\..\bin\bash.exe'
  & $bash scripts/check.sh full
  ```

  The same applies to every `bash scripts/...` command in these docs. The [`metaphor-checks` distro](#run-the-check-script-suites-in-wsl) doesn't change this: the suites
  reach it from Git Bash on their own. If it's your only distro, WSL makes it the default, and plain `bash` then opens
  it as root; still use Git Bash.
- The .NET SDK version pinned in [`global.json`](../../../global.json), from
  [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet). Roll-forward is off, so a newer SDK won't do.
  `dotnet --list-sdks` shows what you have.
- Pester 5 or later, for the scripts' tests; CI runs the version
  [`onboard.ps1`](../../../scripts/setup/onboard.ps1) pins (`$pesterVersion`), and another one only gets a warning.
  Windows PowerShell's built-in Pester 3 doesn't run them. Onboarding installs the pin for your user with
  [`install-pester.ps1`](../../../scripts/setup/install-pester.ps1), which refuses a package not signed by Pester's
  publisher; it prints the command to run it yourself.
- Optional: the [GitHub CLI](https://cli.github.com/) (`gh`) for issues and pull requests.

## 2. Clone and set up

In Git Bash:

```sh
git clone https://github.com/Sour-Dev-Home/metaphor-accessibility.git
cd metaphor-accessibility
git config core.hooksPath scripts/githooks
cp .claude/pii-patterns.example.txt .claude/pii-patterns.txt
```

The hooks check each commit and push, running only what the change needs; [checks](../reference/checks.md) lists what
each hook runs. The first push after a pull builds from cold and takes longer.

Keep `core.hooksPath` relative, as above: git reads it from each worktree's root, so each worktree runs its own branch's
hooks. `check.sh rules` and `full` warn about an absolute one.

Agent worktrees pile up in `.claude/worktrees/`: `bash scripts/dev/prune-worktrees.sh [--dry-run]` removes the clean,
unlocked ones whose work is on `origin` or in a merged PR, and keeps the rest with the reason.

CI runs everything again, so it stays the authority. In `.claude/pii-patterns.txt`, replace the two example lines with
your own private strings, such as your name and email, one per line. It's git-ignored, and the checks block any commit
that contains one. Don't leave the example lines in: they match the example file itself, so the checks would fail.

Known limitation: each line matches anywhere in a line, ignoring case, with no exceptions. A short pattern, such as a
first name alone, can match a game character, a credited person or part of a longer word, and block a commit that holds
no personal information. Use specific strings (full name, email, user name, phone number). If a real collision blocks
you, reword that line, for example name a character by role; don't weaken or remove the pattern.

## 3. Run the checks

```sh
bash scripts/check.sh fast
bash scripts/check.sh full
```

`fast` checks the staged files, so with nothing staged it passes at once. `full` checks the files changed since
`origin/main`, then builds and runs the tests and script suites. Both end with exit code 0 when everything passes.
Run `full`, then push: the pre-push hook reuses its [recorded pass](../reference/checks.md#recorded-pass).

If you change docs, also run their stage:

```sh
bash scripts/check.sh docs
```

A broken link fails it; everything else is a warning. It runs lychee, Vale and markdownlint (which needs Node.js), and
so do the check-script tests. `check.sh full` installs them when they're missing, once per clone, and every worktree
shares them. On Windows, lychee needs the Visual C++ Redistributable (x64), which `.\scripts\setup\onboard.ps1` offers
to install. Running `bash scripts/setup/install-docs-tools.sh` yourself is optional; its header says how the pins are
kept ([`install-docs-tools.sh`](../../../scripts/setup/install-docs-tools.sh)). To flag another filler word, add it to
[`tools/docs/.vale/styles/Project/Filler.yml`](../../../tools/docs/.vale/styles/Project/Filler.yml).

[Checks](../reference/checks.md) lists every stage, how CI runs them, and how to run the `RoundTrip` tests on your game
data.

## Run the check-script suites in WSL

The check-script suites (`tests/check/test-check.sh` and `tests/bash/*.sh`) start thousands of processes, which is
slow in Git Bash: `test-check.sh` takes minutes there and seconds on Linux
([`run-check-suites.sh`](../../../scripts/check/run-check-suites.sh) runs them all). With WSL 2 on your machine, one
command sets up a distro for them. In PowerShell, from the repository root:

```powershell
.\scripts\setup\setup-wsl.ps1
```

It asks once, then creates a WSL distro named `metaphor-checks` from Debian (a download of about 100 MB) and installs
the tools listed in [`scripts/setup/wsl-tools.txt`](../../../scripts/setup/wsl-tools.txt) inside it, as the distro's
root. Your own distros, WSL's settings and your git config stay as they are. Running it again installs only what's
missing; `-Update` upgrades the distro's packages. On a machine without WSL 2 (no WSL, WSL 1 only, or virtualization
off) it stops with the reason and changes nothing. When WSL is waiting for Windows to restart, or WSL 2 can't start
because virtualization is off (in a Hyper-V VM, nested virtualization), it says what to do.

From then on, a suite started in Git Bash runs itself in `metaphor-checks`, from your checkout through `/mnt/c`, with
its temporary repositories in the distro's `/tmp`, and exits with the suite's code. Its first line names the tools'
versions. The suites, and `test-check.sh`'s tests, run in a bounded pool ([`pool.sh`](../../../scripts/check/pool.sh))
of the same size in WSL and Git Bash; `METAPHOR_TEST_JOBS` overrides it. CI already runs the suites on Linux and
doesn't use WSL.

When WSL, the distro or a tool is missing, the suite runs in Git Bash as before and prints a one-line notice naming the
setup command. `METAPHOR_NO_WSL=1` makes it run in Git Bash on purpose. `full` then runs `test-check.sh` only when a
check script or check test changed.

To remove the distro and everything in it: `wsl --unregister metaphor-checks`.
