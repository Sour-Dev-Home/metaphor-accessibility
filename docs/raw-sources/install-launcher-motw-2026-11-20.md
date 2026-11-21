# What Windows does with the installer's launcher, `install.cmd`, from a downloaded zip (#574)

- **Source:** Microsoft Learn, [about_Execution_Policies](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_execution_policies?view=powershell-5.1)
  (PowerShell 5.1 page, `ms.date` 2026-08-31, PowerShell-Docs commit `9a8a7830dbbb55ab46555c79e800ca0b0bd0552e`);
  Microsoft Learn, [ADMX_AttachmentManager Policy CSP](https://learn.microsoft.com/windows/client-management/mdm/policy-csp-admx-attachmentmanager)
  (windows-docs-pr commit `6e5887a2f20ef9f4a897a01f069534e71a53d71e`); Microsoft Support,
  [Smart App Control has blocked an app with a dangerous file extension](https://support.microsoft.com/en-us/topic/smart-app-control-has-blocked-an-app-with-a-dangerous-file-extension-60343058-66f2-4548-b978-e484e13abe89);
  Eric Lawrence (formerly on Microsoft's SmartScreen and Edge teams),
  [Downloads and the Mark-of-the-Web](https://textslashplain.com/2016/04/04/downloads-and-the-mark-of-the-web/) (last
  updated 2026-09-01) and
  [SmartScreen Application Reputation, with Pictures](https://textslashplain.com/2023/08/23/smartscreen-application-reputation-in-pictures);
  Didier Stevens, SANS ISC diary [7-Zip & MoW](https://isc.sans.edu/diary/7-Zip+%26+MoW/28810) (2022-07-03). Each page
  was read on the date below; the quotes are from it. Plus a run on Windows 11 Pro 10.0.26200.9974, Windows PowerShell
  5.1 and PowerShell 7, steps below.
- **Saved:** 2026-11-20.
- **Game build:** none; this is about Windows, not the game.

## What happens, in short

1. Unzipping the downloaded zip in File Explorer marks every file it extracts as from the internet, `install.cmd`
   included (run 1). 7-Zip doesn't by default.
2. Opening the marked `install.cmd` with Enter goes through the shell, which checks the mark:
   - SmartScreen's app reputation check covers `.cmd` but answers "Unsupported" for it, so Windows shows the older
     Attachment Execution Services prompt instead, which asks before running the file. Its exact wording, and what
     NVDA reads, are `[NEEDS VERIFICATION]`.
   - With Smart App Control on (Windows 11), a marked `.cmd` is blocked outright.
3. Clearing the mark with "Unblock" in the file's Properties stops both. Whether that also lifts Smart App Control's
   block is `[NEEDS VERIFICATION]`.
4. Once `install.cmd` runs, nothing else is asked: it starts PowerShell directly, not through the shell, and
   `-ExecutionPolicy Bypass` runs the marked `install.ps1` and its module without prompts (run 2). A Group Policy
   execution policy still wins over `-ExecutionPolicy`.

## The mark, and archives

Lawrence: the mark is a `Zone.Identifier` stream; "The value `3` indicates that the file is from the Internet Zone".
Archive extractors must copy it to each extracted file; Windows Explorer "gets this right".

Stevens, on 7-Zip 22.0: "Version 22.0 of 7-Zip introduces support for the propagation of the Mark-of-Web." "This is not
done by default, it has to be configured": `WriteZoneIdExtract` under `HKEY_CURRENT_USER\SOFTWARE\7-Zip\Options`, absent
or 0 means no propagation; the command line needs `-snz`.

## The prompt: Attachment Manager

The Policy CSP, `AM_SetHighRiskInclusion`: "If the file attachment is in the list of high-risk file types and is from the
restricted zone, Windows blocks the user from accessing the file. If the file is from the Internet zone, Windows prompts
the user before accessing the file." `AM_SetFileRiskLevel`: moderate-risk types from the Internet zone are prompted
too, and moderate is the default risk level. The built-in high-risk list isn't published on that page.

## SmartScreen

Lawrence (2023): Explorer's app reputation check runs "When a MotW-adorned file is executed via the `ShellExecute()`
API"; its supported list begins "`.appref-ms .bat .cmd .com .cpl`", but "an `Unsupported` result is expected for all
`.cmd`, `.bat`, and some `.js` files", and the Attachment Execution Services prompt shows instead. Launching through
`CreateProcess`, as `cmd.exe` and PowerShell do, "does NOT trigger security prompts or SmartScreen Web Service checks
when the invoker does not call `ShellExecute()`". With Smart App Control on, "SmartScreen AppRep is disabled as
redundant."

## Smart App Control

Microsoft Support: Smart App Control "automatically blocks files with certain dangerous file extensions" downloaded
from the internet. Its list of 40 includes `.bat`, `.cmd` and `.ps1`. The page doesn't say how to unblock one.

Lawrence (2016, updated 2026): with Smart App Control on, "many dangerous file types are blocked from `ShellExecute()`
entirely". The mark can be removed with the Unblock button in the file's Properties.

## Execution policy

about_Execution_Policies: `Bypass`: "Nothing is blocked and there are no warnings or prompts." `powershell.exe
-ExecutionPolicy` "affects only the current session and child sessions"; it "isn't stored in the registry", and
"doesn't take precedence over the execution policy set by using a Group Policy". The default on Windows clients is
`Restricted`. Under `RemoteSigned`, unsigned scripts with the mark don't run unless unblocked.

## Runs

Windows 11 Pro 10.0.26200.9974, Smart App Control off
(`HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy`, `VerifiedAndReputablePolicyState` = 0), File Explorer's open
command for `.cmd` files `"%1" %*` (`HKCR\cmdfile\shell\open\command`).

1. **Explorer's unzip copies the mark.** In PowerShell 7: `Compress-Archive` a one-line `install.cmd` into `pkg.zip`;
   write `[ZoneTransfer]` / `ZoneId=3` to `pkg.zip`'s `Zone.Identifier` stream; extract with File Explorer's zip handler
   (`Shell.Application`, `Namespace(folder).CopyHere(Namespace(zip).Items(), 0x614)`). The extracted `install.cmd` had a
   `Zone.Identifier` stream with `ZoneId=3` and `ReferrerUrl` set to the zip's path. `Expand-Archive` in PowerShell 7
   wrote no stream.
2. **The launcher runs a marked installer without a prompt.** `tests/pester/install.Tests.ps1`, `Describe 'install.cmd'`:
   `install.cmd` and a stand-in `install.ps1`, both marked `ZoneId=3`, in a folder whose name has spaces, `&`, `é`, `ü`
   and `日本`, started as Explorer starts it (`CreateProcess` on the file). The stand-in ran in Windows PowerShell 5.1
   (`PSEdition` Desktop) with the `Process` scope's policy `Bypass` and `$PSScriptRoot` the folder, and its exit code
   came back through the launcher.
3. **5.1 needs a clear module path.** Started from a PowerShell 7 window, the launcher's Windows PowerShell 5.1 failed
   to load `Get-ExecutionPolicy`: "The 'Get-ExecutionPolicy' command was found in the module
   'Microsoft.PowerShell.Security', but the module could not be loaded." It inherited PowerShell 7's `PSModulePath`. With
   `PSModulePath` cleared, 5.1 used its own (`Documents\WindowsPowerShell\Modules`, `Program Files\WindowsPowerShell\
   Modules`, `system32\WindowsPowerShell\v1.0\Modules`).
4. **5.1 leaves `$PSScriptRoot` empty in an advanced script's parameter defaults.** A script of
   `[CmdletBinding()]` and `param([string]$P = (Join-Path $PSScriptRoot 'x'))`, run with
   `powershell.exe -NoProfile -ExecutionPolicy Bypass -File`, failed: "Cannot bind argument to parameter 'Path' because
   it is an empty string." The same script without `[CmdletBinding()]` printed the folder. `install.ps1`'s `-Pins`
   default had this form, so in 5.1 it stopped before its first line unless `-Pins` was given.

Not run: opening the marked file with Enter in File Explorer, which shows the prompt on the desktop, and Smart App
Control on.
