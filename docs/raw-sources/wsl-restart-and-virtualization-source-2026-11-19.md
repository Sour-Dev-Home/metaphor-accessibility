# How WSL decides "restart pending" and "virtualization off", from its source

- **Source:** WSL's source, `https://github.com/microsoft/WSL` (MIT), commit `f54590663ff3de8313e50bc794774da2723114e3`
  (`master` on 2026-11-19), read with `gh api repos/microsoft/WSL/contents/<path>?ref=<commit>`. Files:
  `src/windows/common/WslClient.cpp`, `src/windows/common/helpers.cpp`, `localization/strings/de-DE/Resources.resw`.
  The behaviour of WSL 3.0.5.0, the version in `wsl-restart-and-virtualization-messages-2026-11-19.md`, is assumed to
  match this commit. [NEEDS VERIFICATION]
- **Saved:** 2026-11-19.
- **Game build:** none.

## What the source does

- **`wsl --install` with a reboot pending.** `Install()` (`WslClient.cpp#L421`) calls `InstallPrerequisites`, whose
  result is `rebootRequired` (`#L526`). It installs a distro only `if (!noDistribution && (legacy || !rebootRequired))`
  (`#L533`), then, when `rebootRequired`, prints `PrintSystemError(ERROR_SUCCESS_REBOOT_REQUIRED)` (`#L565-L568`):
  Windows' own text for error 3010, "The requested operation is successful. Changes will not be effective until the
  system is rebooted.", in the user's language. It then returns 0 (`#L597`). So with a distro named and without
  `--legacy`, exit code 0 and no new distro means a restart is pending.
- **`wsl --status` with WSL 2 unavailable.** `Status()` (`#L1163`) prints `MessageEnableVirtualization` when
  `!IsServicePresent(L"vmcompute")` (`#L1188-L1191`), and returns 0. `IsServicePresent` (`helpers.cpp#L493`) is true
  when `OpenService` opens the service: the Hyper-V Host Compute Service, `vmcompute`, is installed.
- **The message is localized.** `MessageEnableVirtualization` in `de-DE/Resources.resw` (`#L321`): "WSL2 kann nicht
  gestartet werden, da die Virtualisierung auf diesem Computer nicht aktiviert ist."

## What it shows

- Both causes can be told apart without reading WSL's text: a restart pending is `wsl --install` exiting 0 without
  creating the named distro; virtualization off is the `vmcompute` service missing.
