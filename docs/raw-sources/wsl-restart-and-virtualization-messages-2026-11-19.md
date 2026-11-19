# WSL's restart-pending and virtualization-off messages in a fresh VM

- **Source:** the maintainer's clean-VM run of `scripts/onboard.ps1` on 2026-11-19 (#267's onboarding re-time),
  recorded in issue #402's body. WSL 3.0.5.0, Windows 10.0.26300.
- **Saved:** 2026-11-19.
- **Game build:** none. Windows 10.0.26300, in a Hyper-V VM.

## Steps

1. A fresh Hyper-V VM, where `Win32_ComputerSystem.HypervisorPresent` was true and Virtual Machine Platform wasn't yet
   enabled.
2. `scripts/onboard.ps1`, whose WSL step runs `scripts/setup-wsl.ps1`, which ran
   `wsl --install Debian --name metaphor-checks --version 2 --no-launch`.
3. That command enabled Virtual Machine Platform itself and printed the restart message below. No `metaphor-checks`
   distro existed afterwards, and `setup-wsl.ps1` said "WSL reported success but has no metaphor-checks distro".
4. After restarting Windows, `wsl --status` printed the virtualization message below. `setup-wsl.ps1` again said "WSL
   reported success but has no metaphor-checks distro".

## WSL's output

From `wsl --install` before the restart, as quoted in #402:

```text
The requested operation is successful. Changes will not be effective until the system is rebooted.
```

From `wsl --status` after the restart, as quoted in #402:

```text
WSL2 is unable to start since virtualization is not enabled on this machine.
```

## What it shows

- `wsl --install` can enable Virtual Machine Platform itself and then needs a restart before it creates the distro. The
  feature's install state already reads as enabled before that restart.
- Inside a Hyper-V guest `HypervisorPresent` is true even when the host doesn't expose virtualization to it, so only
  WSL's own message shows WSL 2 can't start.
- `wsl --install`'s exit code in step 3 wasn't recorded. [NEEDS VERIFICATION]
- The same wording on other WSL versions, and whether `wsl --install` prints the virtualization message too.
  [NEEDS VERIFICATION]
