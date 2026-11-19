# WSL's Debian install failing on a passing DNS error in a fresh VM

- **Source:** the maintainer's clean-VM run of `scripts/onboard.ps1`, recorded in issue #360's body and in #267's
  comments of 2026-11-19 (the clean-VM timing comment and its update). WSL as installed in that VM; its version wasn't
  recorded.
- **Saved:** 2026-11-19.
- **Game build:** none. Windows 11 Enterprise evaluation, build 26300 (from the evaluation ISO's name,
  `26300.9457…CLIENTENTERPRISEEVAL…`).

## Steps

1. A fresh Hyper-V VM from that ISO, with nested virtualization on, Git for Windows and WSL installed.
2. `scripts/onboard.ps1`, whose WSL step runs `scripts/setup-wsl.ps1`, which ran
   `wsl --install Debian --name metaphor-checks --version 2 --no-launch`.
3. That failed. The summary line was `creating metaphor-checks failed (wsl.exe exit code -1)`.
4. The same command, run again a few minutes later, succeeded, and onboarding then showed WSL as ready.

## WSL's output on the failure

As quoted in #360:

```text
Failed to fetch the distribution list from 'https://raw.githubusercontent.com/microsoft/WSL/master/distributions/DistributionInfo.json'. The server name or address could not be resolved
```

with the error code `Wsl/InstallDistro/WININET_E_NAME_NOT_RESOLVED`.

## What it shows

- A name-resolution failure while fetching the distribution list stops `wsl --install` with a nonzero exit code, and
  WSL names the WinINet error in its error code.
- The failure was passing: the same command later succeeded with no change to WSL.
- Whether WSL reports WinINet's other download errors (no connection, a timeout) with the same `WININET_E_` prefix
  wasn't seen here. [NEEDS VERIFICATION]
