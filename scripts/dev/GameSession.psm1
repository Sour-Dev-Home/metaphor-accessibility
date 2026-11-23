# The game-session module (#437): everything a session in the real game needs, behind scripts/dev/session.ps1's verbs.
# It finds the game and Reloaded-II, checks the game build, installs the mod, launches and waits, adds opt-ins, reads
# the logs, and leaves the machine as it found it: Reloaded-II's root config restored, the game closed unless asked to
# keep it, temporary opt-ins removed, also after a failure or Ctrl+C (Invoke-InSession). Built on ReloadedDev.psm1.
# Doesn't ship. Loader facts: docs/raw-sources/reloaded-ii-config-location-2026-11-13.md,
# reloaded-ii-launch-and-logs-2026-11-13.md, reloaded-ii-app-and-mod-config-2026-11-15.md and
# reloaded-ii-clean-exit-2026-11-14.md.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReloadedDev.psm1')

$Repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$MessageLogFileName = 'message-log.txt' # Metaphor.Core.MessageLog.FileName
$TranscriptFileName = 'transcript.txt' # Metaphor.Core.Announcements.TranscriptAnnouncer.FileName
$ScanFileName = 'signature-scan.json' # Metaphor.Core.SignatureScan
$ProbesFileName = 'signature-probes.json' # Metaphor.Core.SignatureScan.ProbesFileName
$BackupSuffix = '.session-backup'
$SharedHooksId = 'reloaded.sharedlib.hooks'
# How long a run waits for the mod's load line in Reloaded-II's log once the mod has reported: it logs that line just
# after writing its status file.
$LoaderLogSeconds = 10
$PollMilliseconds = 100
# How long the game must stay gone before a wait counts it as exited: at least the grace that two missed polls 500 ms
# apart gave before #339 made polling faster.
$GameGoneMilliseconds = 1000
$InspectorPipeName = 'metaphor-accessibility-inspector' # LiveInspector.PipeName
$InspectorMaxLineBytes = 512 # Metaphor.Core.Inspector.InspectorSession.MaxLineBytes
$InspectorLogFileName = 'inspector-log.txt'

# The options each verb takes, as session.ps1 names them. Any other is refused, so a misplaced one never goes unnoticed.
$Launching = 'GameExe', 'LauncherExe', 'ReloadedConfig', 'KnownBuilds', 'TimeoutSeconds'
$VerbOptions = @{
    deploy  = $Launching + 'Project', 'Configuration', 'KeepGame'
    run     = $Launching + 'Project', 'Seconds', 'OptIn', 'SaveLog', 'KeepGame'
    smoke   = $Launching + 'ModFolders'
    spike   = $Launching + 'Project', 'OutFile', 'Probes'
    inspect = 'Request', 'PipeName', 'TimeoutMilliseconds', 'Batch', 'Label', 'WaitKey', 'SaveLog'
    status  = 'GameExe', 'LauncherExe', 'ReloadedConfig', 'KnownBuilds', 'Project', 'PipeName'
}

# A verb that stops: what went wrong and what to do. Exit code 2 while nothing has changed, 1 once the game was launched
# or something was left changed (Invoke-GameSession), unless given.
class SessionStop : Exception {
    [string] $Fix
    [int] $ExitCode
    SessionStop([string] $Message, [string] $Fix, [int] $ExitCode) : base($Message) { $this.Fix = $Fix; $this.ExitCode = $ExitCode }
}

function Stop-Session([string] $Message, [string] $Fix, [int] $ExitCode = 0) { throw [SessionStop]::new($Message, $Fix, $ExitCode) }

# Prints a line with local folders named as <game>, <mods>, ~ and so on (Hide-LocalPaths), so the output can go into an
# issue or a raw source as it is.
function Write-Line([string] $Text) { Write-Host (Hide-LocalPaths $script:Names $Text) }

function Get-Option([string] $Name, $Default) {
    $value = $script:Options[$Name]
    if ($null -eq $value -or ($value -is [string] -and $value -eq '')) { $Default } else { $value }
}

# The full path of -SaveLog, or nothing without it. The logs hold game text, so they never land where they could be
# committed: not by its path, nor through a junction or symlink, nor as git sees it (#502).
function Resolve-SaveFolder {
    $save = Get-Option 'SaveLog' $null
    if (-not $save) { return }
    $save = Get-FullPath $save
    $repo = Find-GitRepository $save
    if ($repo) { Stop-Session "-SaveLog $save is inside the git repository at $repo" 'the logs hold game text: save them outside any repository' }
    Add-PathName $script:Names '<save>' $save
    $save
}

# The repository PATH (a full path, which may not exist yet) is inside, or nothing. It checks the path as given, with
# every junction and symlink along it resolved, and as Windows finally names its deepest existing folder (which also
# undoes a subst drive or a mapped share), for a .git folder or a worktree's .git file in it or an ancestor, then asks
# git about that final folder, which also finds a bare repository.
function Find-GitRepository([string] $Path) {
    $real = Resolve-RealPath $Path
    for ($existing = $real; $existing -and -not [IO.Directory]::Exists($existing); $existing = Split-Path $existing) { }
    if (-not $existing) { Stop-Session "-SaveLog $Path is on a drive that doesn't exist" 'save the logs on a local drive' }
    $final = Get-FinalPath $existing
    foreach ($start in $Path, $real, $final) {
        for ($dir = $start; $dir; $dir = Split-Path $dir) {
            if (Test-Path -LiteralPath (Join-Path $dir '.git')) { return $dir }
        }
    }
    if (Test-InsideGit $final) { return $final }
}

# The name Windows gives the existing folder DIR once every junction, symlink, subst drive and mapped drive is undone
# (GetFinalPathNameByHandleW), without the \\?\ prefix. A folder it can't open stops the session, as nothing about it
# can be ruled out.
function Get-FinalPath([string] $Dir) {
    if (-not ('MetaphorDev.FinalPath' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace MetaphorDev {
    public static class FinalPath {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
        // Opens the folder for no access (0), sharing everything (7), as OPEN_EXISTING (3) with FILE_FLAG_BACKUP_SEMANTICS
        // (0x02000000), which a folder needs; asks for its DOS name (0). Null when either call fails.
        public static string Get(string dir) {
            using (SafeFileHandle handle = CreateFileW(dir, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero)) {
                if (handle.IsInvalid) return null;
                StringBuilder path = new StringBuilder(32768);
                uint length = GetFinalPathNameByHandleW(handle, path, (uint) path.Capacity, 0);
                return length == 0 || length >= path.Capacity ? null : path.ToString();
            }
        }
    }
}
'@
    }
    $final = [MetaphorDev.FinalPath]::Get($Dir)
    if (-not $final) { Stop-Session "-SaveLog can't be checked: Windows can't open $Dir" 'save the logs in a folder you can open' }
    if ($final.StartsWith('\\?\UNC\')) { return '\\' + $final.Substring(8) }
    if ($final.StartsWith('\\?\')) { return $final.Substring(4) }
    $final
}

# PATH with every junction and symlink in it, the leaf and each ancestor, replaced by its final target. A part that
# doesn't exist yet is kept as written. Another kind of reparse point (a OneDrive placeholder, say) has no target and
# doesn't redirect, so it stays. Gives up past 32 links, as a loop of them never ends.
function Resolve-RealPath([string] $Path, [int] $Depth = 0) {
    if ($Depth -gt 32) { Stop-Session "-SaveLog $Path goes through more than 32 links" 'save the logs in a plain folder' }
    $root = [IO.Path]::GetPathRoot($Path)
    $real = $root
    $parts = @($Path.Substring($root.Length).Split([char[]] '\/', [StringSplitOptions]::RemoveEmptyEntries))
    for ($i = 0; $i -lt $parts.Count; $i++) {
        $next = Join-Path $real $parts[$i]
        $info = [IO.DirectoryInfo]::new($next)
        if ([int] $info.Attributes -eq -1) { return [IO.Path]::Combine([string[]] (@($next) + @($parts | Select-Object -Skip ($i + 1)))) }
        if ($info.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint) -and $info.LinkTarget) {
            $target = $info.ResolveLinkTarget($true)
            $next = Resolve-RealPath $(if ($target) { $target.FullName } else { [IO.Path]::GetFullPath($info.LinkTarget, $real) }) ($Depth + 1)
        }
        $real = $next
    }
    $real
}

# Whether git counts DIR as inside a repository: its work tree, its .git folder or a bare one. Git runs without any
# GIT_ variable, so neither a hook's environment nor a contributor's can point it elsewhere or hide a repository
# (GIT_DIR, GIT_CEILING_DIRECTORIES, `git -c` overrides; scripts/check/run.sh's timed_step clears the same for #448),
# and trusts every folder's owner, so safe.directory can't hide one either. That trust is safe only because rev-parse
# runs nothing from the repository's config (no hook, fsmonitor or pager): never run another git command under it.
# Without git nothing is ruled out: it stops.
function Test-InsideGit([string] $Dir) {
    $git = Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $git) { Stop-Session "-SaveLog can't be checked: git wasn't found" 'install git, or put it on PATH' }
    $start = [Diagnostics.ProcessStartInfo]::new($git.Source)
    foreach ($arg in '-c', 'safe.directory=*', '-C', $Dir, 'rev-parse', '--is-inside-work-tree') { $start.ArgumentList.Add($arg) }
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($name in @($start.Environment.Keys)) { if ($name -like 'GIT_*') { $null = $start.Environment.Remove($name) } }
    $process = [Diagnostics.Process]::Start($start)
    $errors = $process.StandardError.ReadToEndAsync()
    $null = $process.StandardOutput.ReadToEnd()
    $process.WaitForExit()
    $null = $errors.Result
    $process.ExitCode -eq 0
}

function Get-FullPath([string] $Path) {
    [IO.Path]::GetFullPath($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path))
}

function Close-Program([string] $Exe) {
    Get-Game $Exe | ForEach-Object { $_.Kill(); $_.WaitForExit(10000) | Out-Null }
}

# --- Finding things ---------------------------------------------------------------------------------------------------

# The game, Reloaded-II and their records. Each path is the parameter when given, else the environment variable
# (METAPHOR_GAME_EXE, METAPHOR_RELOADED_EXE), else what the game locator finds (MetaphorInstall.psm1).
function Find-SessionPaths {
    $config = Get-Option 'ReloadedConfig' (Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Reloaded-Mod-Loader-II/ReloadedII.json')
    $game = Get-Option 'GameExe' $env:METAPHOR_GAME_EXE
    if (-not $game) { $game = @(Find-MetaphorGame -ConfigPath $config) | Select-Object -First 1 }
    $launcher = Get-Option 'LauncherExe' $null
    if (-not $launcher) { $launcher = Find-ReloadedLauncher -ConfigPath $config }
    $paths = [pscustomobject]@{
        Config      = Get-FullPath $config
        GameExe     = if ($game) { Get-FullPath $game }
        LauncherExe = if ($launcher) { Get-FullPath $launcher }
        KnownBuilds = Get-FullPath (Get-Option 'KnownBuilds' (Join-Path $Repo 'src/Metaphor.Core/known-game-builds.json'))
        Project     = Get-FullPath (Get-Option 'Project' (Join-Path $Repo 'src/Metaphor.Hooks'))
    }
    Add-PathName $script:Names '<reloaded>' (Split-Path $paths.Config)
    if ($paths.GameExe) { Add-PathName $script:Names '<game>' (Split-Path $paths.GameExe) }
    Add-PathName $script:Names '<project>' $paths.Project
    $paths
}

# The opt-in names the mod reads: each opt-in-<name>.txt that the project's sources name (HookLoader's OptInFile and
# LiveInspector.OptInFile).
function Get-OptInName([string] $Project) {
    $sources = Get-ChildItem -LiteralPath $Project -Filter *.cs -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object FullName -NotMatch '[\\/](bin|obj)[\\/]'
    @($sources | Select-String -Pattern '"opt-in-([a-z0-9][a-z0-9-]*)\.txt"' -AllMatches |
            ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
}

# The names asked for, split at commas, each one the mod reads. Refuses any other with the list of valid ones.
function Resolve-OptIn([string[]] $Names, [string] $Project) {
    $valid = Get-OptInName $Project
    $asked = @($Names | ForEach-Object { $_ -split ',' } | ForEach-Object Trim | Where-Object { $_ } | Select-Object -Unique)
    $unknown = @($asked | Where-Object { $_ -cnotin $valid })
    if ($unknown) {
        Stop-Session "unknown opt-in $($unknown -join ', ')" "use one of: $(if ($valid) { $valid -join ', ' } else { 'none (the mod reads no opt-in file)' })"
    }
    $asked
}

# --- Leaving the machine as found -------------------------------------------------------------------------------------

# A backup that a killed run left (a hard kill skips finally) goes back first.
function Restore-LeftoverConfig([string] $Config) {
    $backup = "$Config$BackupSuffix"
    if (Test-Path -LiteralPath $backup) {
        Move-Item -LiteralPath $backup $Config -Force
        Write-Line "restored $Config from an interrupted session (Reloaded-II settings changed since then are discarded)"
    }
}

function Invoke-Undo([string] $What, [scriptblock] $Undo) {
    try { & $Undo; $null }
    catch { Write-Line "cleanup failed: $What`: $($_.Exception.Message)"; $What }
}

# Runs BODY with Reloaded-II's root config saved, and undoes the session afterwards, whatever happened (an error, a
# stop, Ctrl+C): closes the launcher, closes the game unless KeepGame, removes the opt-ins it added and the temporary
# folders, and puts the config back byte for byte. A cleanup that fails turns a pass into a failure.
function Invoke-InSession($S, [scriptblock] $Body) {
    $config = $S.Paths.Config
    $saved = [IO.File]::ReadAllBytes($config)
    [IO.File]::WriteAllBytes("$config$BackupSuffix", $saved)
    $passed = $false
    try {
        $result = & $Body
        $passed = $true
        $result
    }
    finally {
        $failed = @(
            # The launcher first: one still running could save over the restored config.
            if ($S.Launched -and $S.Paths.LauncherExe) { Invoke-Undo 'close Reloaded-II' { Close-Program $S.Paths.LauncherExe } }
            if ($S.Launched -and -not $S.KeepGame) { Invoke-Undo 'close the game' { Close-Program $S.Paths.GameExe } }
            foreach ($file in $S.OptInFiles) { Invoke-Undo "remove $file" { [IO.File]::Delete($file) } }
            Invoke-Undo "restore $config" {
                [IO.File]::WriteAllBytes($config, $saved)
                [IO.File]::Delete("$config$BackupSuffix")
            }
            foreach ($folder in $S.TempFolders) {
                Invoke-Undo "remove $folder" { if (Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder -Recurse -Force } }
            }
        ) | Where-Object { $_ }
        if ($failed) { $S.Changed = $true }
        if ($failed -and $passed) {
            Stop-Session "cleanup failed ($($failed -join '; '))" 'see the lines above, then put those back by hand'
        }
    }
}

# --- Launching --------------------------------------------------------------------------------------------------------

# Everything a launching verb checks before it changes anything: the loader's config, both programs present and closed,
# a known game build, and the game registered in Reloaded-II.
function Initialize-Launch($S) {
    $paths = $S.Paths
    if (-not (Test-Path -LiteralPath $paths.Config)) { Stop-Session "no Reloaded-II config at $($paths.Config)" 'open Reloaded-II once, or pass -ReloadedConfig' }
    Restore-LeftoverConfig $paths.Config
    $S.Loader = Read-Json $paths.Config
    Add-PathName $script:Names '<mods>' ([string] $S.Loader.ModConfigDirectory)
    if (-not $paths.GameExe -or -not (Test-Path -LiteralPath $paths.GameExe)) {
        Stop-Session 'no game executable' 'register the game in Reloaded-II, or set METAPHOR_GAME_EXE'
    }
    if (-not $paths.LauncherExe -or -not (Test-Path -LiteralPath $paths.LauncherExe)) {
        Stop-Session 'no Reloaded-II.exe' 'open Reloaded-II once, or set METAPHOR_RELOADED_EXE'
    }
    # The session closes the game it started, and a running launcher can save over the config.
    if (Get-Game $paths.GameExe) { Stop-Session 'the game is already running' 'close it first' }
    if (Get-Game $paths.LauncherExe) { Stop-Session 'Reloaded-II is already running' 'close it first' }

    $build = Get-GameBuild $paths.GameExe $paths.KnownBuilds
    if (-not $build.Known) { Stop-Session "unknown game build $($build.Sha256)" 'add it: docs/contributors/how-to/add-a-game-build.md' }
    Write-Line "game build $($build.Sha256) (Steam build $($build.Known.steamBuildId))"
    $S.Build = $build.Sha256

    $S.Application = Find-Application $S.Loader $paths.GameExe
    if (-not $S.Application) { Stop-Session "the game isn't registered in Reloaded-II" "add $($paths.GameExe) as an application first" }
    $paths.GameExe = $S.Application.GameExe
}

# Builds PROJECT into OUT.
function Publish-Mod([string] $Project, [string] $Configuration, [string] $Out) {
    dotnet publish $Project -c $Configuration -o $Out -v q --nologo 2>&1 | ForEach-Object { Write-Line "$_" }
    if ($LASTEXITCODE -ne 0) { Stop-Session "the build of $Project failed" 'fix the errors above' }
}

# A mod ID names the folder that gets deleted or copied to, so it must not reach outside the mods folder, nor be a name
# Windows changes: trailing dots and spaces are dropped, and device names (CON, NUL, COM1, ...) are reserved with or
# without an extension.
function Assert-ModId([string] $ModId, [string] $Where) {
    if (-not $ModId -or $ModId -in '.', '..' -or $ModId.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or
        $ModId -match '[. ]$' -or $ModId -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\..*)?$') {
        Stop-Session "the mod ID '$ModId' in $Where is not a plain folder name" 'fix ModId in its ModConfig.json'
    }
}

# The session's switch for testing features (#783; ModSettings.TestingFile): an empty file in the mod's folder that a
# Debug build reads, removed when the session ends like the opt-ins. The player's settings.json is never touched.
function Add-TestingSwitch($S, $Mod, [string] $Configuration) {
    if ($Configuration -ne 'Debug') { return }
    $file = Join-Path $Mod.Folder 'opt-in-testing.txt'
    [IO.File]::WriteAllBytes($file, [byte[]] @())
    $S.OptInFiles.Add($file)
    Write-Line 'testing features on: opt-in-testing.txt (removed when the session ends)'
}

# Builds the mod and installs it: replaces its folder in Reloaded-II's mods folder and enables it for the game, keeping
# the mods already enabled. The install is what deploy is for, so it stays; the old app config is kept beside it as
# AppConfig.json.deploy-backup. Returns { Id; Folder }.
function Install-DevMod($S, [string] $Configuration) {
    $project = $S.Paths.Project
    $staging = Join-Path ([IO.Path]::GetTempPath()) "metaphor-deploy-$([guid]::NewGuid())"
    $S.TempFolders.Add($staging)
    Publish-Mod $project $Configuration $staging
    $modConfig = Join-Path $staging 'ModConfig.json'
    if (-not (Test-Path -LiteralPath $modConfig)) { Stop-Session "the build of $project has no ModConfig.json" 'copy it to the build output' }
    $modId = [string] (Read-Json $modConfig).ModId
    Assert-ModId $modId "the build of $project"

    $modsRoot = [string] $S.Loader.ModConfigDirectory
    if (-not [IO.Path]::IsPathFullyQualified($modsRoot) -or -not (Test-Path -LiteralPath $modsRoot -PathType Container)) {
        Stop-Session "ModConfigDirectory in $($S.Paths.Config) isn't an existing full path: '$modsRoot'" 'open Reloaded-II and check its mods folder'
    }
    $folder = Join-Path $modsRoot $modId
    if (Test-Path -LiteralPath $folder) {
        if ((Get-Item -LiteralPath $folder -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            Stop-Session "$folder is a link" 'replace it with a plain folder first'
        }
        $existing = Join-Path $folder 'ModConfig.json'
        if (-not (Test-Path -LiteralPath $existing) -or (Read-Json $existing).ModId -ne $modId) {
            Stop-Session "$folder holds something other than $modId" 'move it out of the way first'
        }
        # From here a failure leaves the install changed, so it can't exit 2.
        $S.Changed = $true
        Remove-Item -LiteralPath $folder -Recurse -Force
    }
    $S.Changed = $true
    Copy-Item -LiteralPath $staging $folder -Recurse
    Write-Line "deployed $modId to $folder"

    # The new config is swapped in whole, so a failed write can't leave a half-written one.
    $appFile = $S.Application.FullName
    $app = Read-Json $appFile
    if (-not $app.PSObject.Properties['EnabledMods']) { $app | Add-Member -NotePropertyName EnabledMods -NotePropertyValue @() }
    $enabled = @($app.EnabledMods | Where-Object { $null -ne $_ })
    if ($enabled -notcontains $modId) {
        $app.EnabledMods = $enabled + $modId
        Write-Json "$appFile.deploy-new" $app
        Copy-Item -LiteralPath $appFile "$appFile.deploy-backup" -Force
        Move-Item -LiteralPath "$appFile.deploy-new" $appFile -Force
        Write-Line "enabled $modId for $($S.Application.Directory.Name) (previous config: $appFile.deploy-backup)"
    }
    [pscustomobject]@{ Id = $modId; Folder = $folder }
}

# Launches through Reloaded-II (--launch) and waits for each mod's status file. True when every mod is ready on the
# game's build with all its hooks. One deadline covers launch and wait.
function Start-Game($S, $Mods) {
    $timeout = [int] (Get-Option 'TimeoutSeconds' 120)
    $S.Deadline = (Get-Date).AddSeconds($timeout)
    $S.Launched = $true
    # The launcher exits by itself once the game has started.
    $launcher = Start-Process -FilePath $S.Paths.LauncherExe -ArgumentList '--launch', "`"$($S.Paths.GameExe)`"" -PassThru
    if (-not $launcher.WaitForExit($timeout * 1000)) { Write-Line 'the launcher did not exit'; return $false }
    Wait-ModStatus $Mods $S.Paths.GameExe $S.Build $S.Deadline $timeout
}

# Waits for each mod's status file (each $Mods item has Id and Folder), the game exiting, or the deadline, then prints
# one line per mod. True when every mod is ready on $Build with all its hooks.
function Wait-ModStatus($Mods, [string] $GameExe, [string] $Build, [datetime] $Deadline, [int] $TimeoutSeconds) {
    $passed = $true
    $goneFor = $null
    do {
        $pending = @($Mods | Where-Object { -not (Test-Path -LiteralPath (Join-Path $_.Folder $StatusFileName)) })
        if ($pending.Count -eq 0) { break }
        if (Get-Game $GameExe) { $goneFor = $null }
        elseif (-not $goneFor) { $goneFor = [Diagnostics.Stopwatch]::StartNew() }
        elseif ($goneFor.ElapsedMilliseconds -ge $GameGoneMilliseconds) {
            Write-Line 'the game exited before every mod reported'; $passed = $false; break
        }
        Start-Sleep -Milliseconds $PollMilliseconds
    } while ((Get-Date) -lt $Deadline)

    foreach ($mod in $Mods) {
        $file = Join-Path $mod.Folder $StatusFileName
        if (-not (Test-Path -LiteralPath $file)) {
            Write-Line "$($mod.Id): no status within $TimeoutSeconds s"
            $passed = $false
            continue
        }
        $status = Read-Json $file
        if ($status.modId -ne $mod.Id) {
            Write-Line "$($mod.Id): status file names another mod, $($status.modId)"
            $passed = $false
        }
        elseif ($status.state -eq 'ready') {
            Write-Line "$($mod.Id): ready on $($status.gameBuildSha256), hooks $($status.hooksLoaded)/$($status.hooksExpected)"
            if ($status.hooksLoaded -ne $status.hooksExpected -or $status.gameBuildSha256 -ne $Build) { $passed = $false }
        }
        else {
            Write-Line "$($mod.Id): $($status.state) on $($status.gameBuildSha256) ($($status.reason))"
            $passed = $false
        }
    }
    $passed
}

# The mod's skipped, failed and ready lines in Reloaded-II's log of this run, read from the open file (the folder
# listing lags) until its load line is in it.
function Show-LoaderLog($S, [string] $ModId, [datetime] $LaunchedAt) {
    $logs = Join-Path (Split-Path $S.Paths.Config) 'Logs'
    # "{UTC time} ~ {exe}.txt", with " [n]" added when a log of the same second exists
    # (reloaded-ii-launch-and-logs-2026-11-13.md).
    $logName = "^\d{4}-\d{2}-\d{2} \d{2}\.\d{2}\.\d{2} ~ $([regex]::Escape([IO.Path]::GetFileNameWithoutExtension($S.Paths.GameExe)))( \[\d+\])?\.txt$"
    $prefix = "[$ModId] "
    $deadline = (Get-Date).AddSeconds($LoaderLogSeconds)
    $log = $null
    $lines = @()
    do {
        $log = Get-ChildItem -LiteralPath $logs -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match $logName -and $_.CreationTimeUtc -ge $LaunchedAt } | Sort-Object CreationTimeUtc |
            Select-Object -Last 1
        if ($log) {
            $lines = @((Read-SharedText $log.FullName) -split "\r?\n" | Where-Object { $_.Contains($prefix) })
            if ($lines | Where-Object { $_ -match ' on game build ' }) { break }
        }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    if (-not $log) { Write-Line "no Reloaded-II log of this run in $logs"; return }
    $shown = @($lines | Where-Object { $_ -match '\bskipped\b|\bfailed\b|\bReady on\b|\bRefused on\b' })
    if ($shown) { $shown | ForEach-Object { Write-Line $_ } } else { Write-Line "Reloaded-II's log has no load line from $ModId" }
}

# A file another process holds open for writing, as the game holds its logs.
function Open-SharedFile([string] $Path) { [IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite') }

function Read-SharedText([string] $Path) {
    $stream = Open-SharedFile $Path
    try { (New-Object IO.StreamReader($stream)).ReadToEnd() } finally { $stream.Dispose() }
}

function Copy-SharedFile([string] $Path, [string] $Target) {
    $source = Open-SharedFile $Path
    try {
        $out = [IO.File]::Create($Target)
        try { $source.CopyTo($out) } finally { $out.Dispose() }
    }
    finally { $source.Dispose() }
}

# The record counts in a message log, by the word after "<n> ms" (Metaphor.Core.MessageLog), and its requests by the
# function named in via=. Source lines ("  | ...") are counted, not shown, and no line is printed: the log holds game text.
function Get-MessageLogSummary([string] $Path) {
    $types = @{}
    $requests = @{}
    $sourceLines = 0
    $unrecognised = 0
    foreach ($line in (Read-SharedText $Path) -split "\r?\n") {
        if ($line -eq '') { continue }
        if ($line.StartsWith('  | ')) { $sourceLines++; continue }
        if ($line -match '^\d+ ms ([a-z]+)\b') {
            $types[$Matches[1]] = 1 + $types[$Matches[1]]
            if ($Matches[1] -eq 'request' -and $line -match ' via=([A-Za-z0-9_]+)') { $requests[$Matches[1]] = 1 + $requests[$Matches[1]] }
            continue
        }
        $unrecognised++
    }

    $counts = foreach ($type in $types.Keys | Sort-Object) {
        if ($type -eq 'compile') { "$($types[$type]) compile ($sourceLines source lines)" } else { "$($types[$type]) $type" }
    }
    if ($unrecognised) { $counts = @($counts) + "$unrecognised unrecognised line$(if ($unrecognised -ne 1) { 's' })" }
    "message log: $(if ($counts) { $counts -join ', ' } else { 'empty' })"
    if ($requests.Count) {
        "requests by function: $(($requests.Keys | Sort-Object | ForEach-Object { "$_ $($requests[$_])" }) -join ', ')"
    }
}

# --- Smoke runs -------------------------------------------------------------------------------------------------------

# The mods to test, each { Id; Source }, checked before anything changes.
function Get-TestMod([string[]] $Folders) {
    if (-not $Folders) { Stop-Session 'no test mods' 'pass -ModFolders with each built test mod''s folder' }
    $mods = @(foreach ($folder in $Folders) {
            $modConfig = Join-Path $folder 'ModConfig.json'
            if (-not (Test-Path -LiteralPath $modConfig)) { Stop-Session "no ModConfig.json in $folder" 'pass the folder of a built mod' }
            $modId = [string] (Read-Json $modConfig).ModId
            Assert-ModId $modId $modConfig
            [pscustomobject]@{ Id = $modId; Source = (Resolve-Path -LiteralPath $folder).Path; Folder = $null }
        })
    $duplicate = $mods | Group-Object Id | Where-Object Count -GT 1 | Select-Object -First 1
    if ($duplicate) { Stop-Session "two test mods have the mod ID $($duplicate.Name)" 'pass each mod once' }
    # Its folder in the throwaway profile holds Reloaded-II's own copy.
    if ($mods.Id -contains $SharedHooksId) { Stop-Session "a test mod has the mod ID $SharedHooksId, which is Reloaded-II's own" 'leave it out of -ModFolders' }
    $mods
}

# Points Reloaded-II's config at a throwaway profile with exactly MODS enabled, plus a copy of Reloaded-II's own
# reloaded.sharedlib.hooks, which the loader needs to start even with no other mod
# (docs/raw-sources/reloaded-ii-log-on-normal-run-2026-11-14.md); launches, waits for every status file, then for
# COLLECTFILES, which it copies to COLLECTTO/<mod ID>/. True when all are there and every mod is ready. Runs inside
# Invoke-InSession, which puts the config back.
function Invoke-SmokeRun($S, $Mods, [string[]] $CollectFiles = @(), [string] $CollectTo) {
    $loader = $S.Loader
    $sharedHooks = Get-ChildItem -LiteralPath $loader.ModConfigDirectory -Filter 'ModConfig.json' -Recurse -Depth 1 -ErrorAction SilentlyContinue |
        Where-Object { try { (Read-Json $_.FullName).ModId -eq $SharedHooksId } catch { $false } } |
        Select-Object -First 1
    if (-not $sharedHooks) { Stop-Session "Reloaded-II's own $SharedHooksId mod isn't in its mods folder" 'open Reloaded-II once so it downloads it' }

    $smokeProfile = Join-Path ([IO.Path]::GetTempPath()) "metaphor-smoke-$([guid]::NewGuid())"
    $S.TempFolders.Add($smokeProfile)
    $profileFolders = [ordered]@{
        ApplicationConfigDirectory = 'Apps'; ModConfigDirectory = 'Mods'; ModUserConfigDirectory = 'User/Mods'
        MiscConfigDirectory = 'User/Misc'; PluginConfigDirectory = 'Plugins'
    }
    foreach ($name in $profileFolders.Keys) {
        $folder = Join-Path $smokeProfile $profileFolders[$name]
        New-Item -ItemType Directory -Force $folder | Out-Null
        $loader | Add-Member -NotePropertyName $name -NotePropertyValue $folder -Force
    }
    $app = Read-Json $S.Application.FullName
    $app.AppLocation = $S.Paths.GameExe
    $app.EnabledMods = @($Mods.Id)
    Write-Json (Join-Path $smokeProfile "Apps/$($S.Application.Directory.Name)/AppConfig.json") $app
    Copy-Item -LiteralPath $sharedHooks.Directory.FullName -Destination (Join-Path $smokeProfile "Mods/$SharedHooksId") -Recurse
    foreach ($mod in $Mods) {
        # Named by mod ID, which is unique, so no two mods share a folder or a status file.
        $mod.Folder = Join-Path $smokeProfile "Mods/$($mod.Id)"
        Copy-Item -LiteralPath $mod.Source $mod.Folder -Recurse
        # A status file from an earlier run would pass without the game loading the mod.
        Remove-Item -Path (Join-Path $mod.Folder "$StatusFileName*") -Force
    }
    Write-Json $S.Paths.Config $loader

    if (-not (Start-Game $S $Mods)) { return $false }
    # Then the files to collect, which the throwaway profile would lose.
    $passed = $true
    foreach ($mod in $Mods) {
        foreach ($name in $CollectFiles) {
            $file = Join-Path $mod.Folder $name
            while (-not (Test-Path -LiteralPath $file) -and (Get-Date) -lt $S.Deadline -and (Get-Game $S.Paths.GameExe)) {
                Start-Sleep -Milliseconds 200
            }
            if (-not (Test-Path -LiteralPath $file)) { Write-Line "$($mod.Id): no $name"; $passed = $false; continue }
            $target = Join-Path $CollectTo $mod.Id
            New-Item -ItemType Directory -Force $target | Out-Null
            Copy-Item -LiteralPath $file -Destination $target
            Write-Line "$($mod.Id): collected $name"
        }
    }
    $passed
}

# --- The verbs --------------------------------------------------------------------------------------------------------
# Each returns its ok line's text, or stops with Stop-Session.

function Invoke-Deploy($S) {
    $S.KeepGame = [bool] (Get-Option 'KeepGame' $false)
    Initialize-Launch $S
    Invoke-InSession $S {
        $configuration = Get-Option 'Configuration' 'Debug'
        $mod = Install-DevMod $S $configuration
        Add-TestingSwitch $S $mod $configuration
        if (-not (Start-Game $S @($mod))) {
            Stop-Session "$($mod.Id) isn't ready with all its hooks" "read the lines above and Reloaded-II's log in $(Split-Path $S.Paths.Config)\Logs"
        }
        if ($S.KeepGame) { "deployed $($mod.Id); the game is left running" } else { "deployed $($mod.Id) and it loaded; closed the game" }
    }
}

function Invoke-Run($S) {
    $seconds = [int] (Get-Option 'Seconds' 120)
    if ($seconds -lt 1 -or $seconds -gt 86400) { Stop-Session "-Seconds $seconds is out of range" 'pass 1 to 86400' }
    $S.KeepGame = [bool] (Get-Option 'KeepGame' $false)
    $optIns = @(Resolve-OptIn (Get-Option 'OptIn' @()) $S.Paths.Project)

    $save = Resolve-SaveFolder

    Initialize-Launch $S
    Invoke-InSession $S {
        # Only Debug builds write message-log.txt (Metaphor.Core.MessageLog).
        $mod = Install-DevMod $S 'Debug'
        Add-TestingSwitch $S $mod 'Debug'
        foreach ($name in $optIns) {
            $file = Join-Path $mod.Folder "opt-in-$name.txt"
            [IO.File]::WriteAllBytes($file, [byte[]] @())
            $S.OptInFiles.Add($file)
            Write-Line "opted in: $name (removed when the session ends)"
        }

        $launchedAt = [DateTime]::UtcNow
        $ready = Start-Game $S @($mod)
        Show-LoaderLog $S $mod.Id $launchedAt
        if (-not $ready) {
            Stop-Session "$($mod.Id) isn't ready with all its hooks" 'read the lines above; fix a listener that did not go in before playing'
        }

        # With -SaveLog, the game ending is how a capture ends (you quit it); without, it means the game didn't survive.
        if ($save) { Write-Line "running for up to $seconds s: quit the game when you are done, and the logs are saved" }
        $started = Get-Date
        $survived = $true
        while (((Get-Date) - $started).TotalSeconds -lt $seconds) {
            if (-not (Get-Game $S.Paths.GameExe)) { $survived = $false; break }
            Start-Sleep -Milliseconds 250
        }
        $ran = [int] ((Get-Date) - $started).TotalSeconds
        Write-Line $(if ($survived) { "the game is still running after $seconds s" } else { "the game exited after $ran s" })
        if (-not $S.KeepGame -and $survived) { Close-Program $S.Paths.GameExe; Write-Line 'closed the game' }

        $log = Join-Path $mod.Folder $MessageLogFileName
        $hasLog = Test-Path -LiteralPath $log
        if ($hasLog) { Get-MessageLogSummary $log | ForEach-Object { Write-Line $_ } }
        if ($save) {
            $target = Join-Path $save (Get-Date -Format 'yyyy-MM-dd HH.mm.ss')
            New-Item -ItemType Directory -Force $target | Out-Null
            $saved = @(foreach ($file in $MessageLogFileName, $TranscriptFileName) {
                    $source = Join-Path $mod.Folder $file
                    if (Test-Path -LiteralPath $source) { Copy-SharedFile $source (Join-Path $target $file); $file }
                    else { Write-Line "no $file in $($mod.Folder)" }
                })
            if ($saved) { Write-Line "saved $($saved -join ' and ') to $target" }
        }

        if (-not $hasLog) { Stop-Session "no $MessageLogFileName from $($mod.Id)" 'check the Debug build writes it (Metaphor.Core.MessageLog)' }
        if (-not $survived -and -not $save) {
            Stop-Session "the game exited after $ran s" "read Reloaded-II's log in $(Split-Path $S.Paths.Config)\Logs"
        }
        $how = if ($save) { "logs saved to $target" } elseif ($S.KeepGame) { 'the game is left running' } else { 'closed the game' }
        "the game ran $(if ($survived) { "$seconds s" } else { "$ran s" }) with $($mod.Id) loaded; $how"
    }
}

function Invoke-Smoke($S) {
    $mods = Get-TestMod (Get-Option 'ModFolders' @())
    Initialize-Launch $S
    Invoke-InSession $S {
        if (-not (Invoke-SmokeRun $S $mods)) { Stop-Session 'a test mod is not ready with all its hooks' 'read the lines above' }
        "every test mod is ready with all its hooks; closed the game"
    }
}

# Issue #21's run: scans the exe on disk and the running game for the same signatures, compares them and writes a
# raw-source draft. Only reads the game (ADR 0002).
function Invoke-Spike($S) {
    $probes = Get-FullPath (Get-Option 'Probes' (Join-Path $Repo 'tools/Metaphor.SignatureCheck/signature-probes.json'))
    $outFile = Get-FullPath (Get-Option 'OutFile' (Join-Path $Repo "artifacts/signature-spike/signature-spike-$(Get-Date -Format yyyy-MM-dd-HHmmss).md"))
    if (Test-Path -LiteralPath $outFile) { Stop-Session "$outFile already exists" 'pass another -OutFile' }
    Initialize-Launch $S
    Invoke-InSession $S {
        # 1. The mod, asked for one scan, which it writes before it installs its own hooks.
        $work = Join-Path ([IO.Path]::GetTempPath()) "metaphor-spike-$([guid]::NewGuid())"
        $S.TempFolders.Add($work)
        $mod = Join-Path $work 'mod'
        Publish-Mod $S.Paths.Project 'Release' $mod
        Copy-Item -LiteralPath $probes (Join-Path $mod $ProbesFileName)

        # 2. On disk. 0: every probe matched once; 1: one didn't. Anything else (a build error, a crash) is output not
        # to keep.
        $diskOutput = @(dotnet run --project (Join-Path $Repo 'tools/Metaphor.SignatureCheck') -c Release -v q -- `
                --probes $S.Paths.GameExe $probes)
        if ($LASTEXITCODE -notin 0, 1) {
            $diskOutput | ForEach-Object { Write-Line "$_" }
            Stop-Session 'the disk check failed' 'read its output above'
        }
        $diskOutput | ForEach-Object { Write-Line "$_" }

        # 3. In the running game, with only our mod enabled, so no other mod's hooks change the bytes.
        $collected = Join-Path $work 'collected'
        $mods = Get-TestMod @($mod)
        if (-not (Invoke-SmokeRun $S $mods @($ScanFileName) $collected)) { Stop-Session 'the smoke run failed' 'read the lines above' }
        $reportFile = Join-Path $collected "$($mods[0].Id)/$ScanFileName"
        $reportJson = Get-Content -Raw -LiteralPath $reportFile
        $draft = New-SpikeDraft $diskOutput ($reportJson | ConvertFrom-Json) $reportJson $probes

        # The draft is meant for docs/raw-sources/, so it must not name local folders (AGENTS.md ground rule 6).
        $localPaths = @($HOME, [IO.Path]::GetTempPath(), $Repo, (Split-Path $S.Paths.GameExe)) |
            Where-Object { $_ } | ForEach-Object { $_.TrimEnd('\', '/') }
        if ($localPaths | Where-Object { $draft.Contains($_, [StringComparison]::OrdinalIgnoreCase) -or $draft.Contains($_.Replace('\', '/'), [StringComparison]::OrdinalIgnoreCase) }) {
            Stop-Session 'the draft would contain a local path; nothing was written' 'find which output names one and shorten it'
        }
        New-Item -ItemType Directory -Force (Split-Path $outFile) | Out-Null
        Set-Content -LiteralPath $outFile -Value $draft -Encoding utf8NoBOM
        "draft written: $outFile; review it, then save it under docs/raw-sources/"
    }
}

# One line of the disk check: "ok   NAME: 1 match at 0xOFF (rva 0xRVA)", "FAIL NAME: N matches, ..." or "no match".
function Read-DiskLine([string] $Line) {
    if ($Line -match '^(ok  |FAIL) (?<name>\S+): (?:1 match at 0x[0-9a-f]+(?: \(rva 0x(?<rva>[0-9a-f]+)\))?|(?<count>\d+) matches|no match)') {
        [pscustomobject]@{
            Name  = $Matches.name
            Count = if ($Matches['count']) { [int]$Matches.count } elseif ($Line -match 'no match') { 0 } else { 1 }
            Rva   = $Matches['rva']
        }
    }
}

function Get-Verdict($Disk, $Memory) {
    if (-not $Disk -or -not $Memory) { return 'missing from one side' }
    if ($Disk.Count -eq 0 -and $Memory.matches -eq 0) { return 'absent from both' }
    if ($Disk.Count -eq 1 -and $Memory.matches -eq 1) {
        $memoryRva = '{0:x}' -f [long]$Memory.firstRvas[0]
        return $(if ($Disk.Rva -eq $memoryRva) { 'same place' } else { 'both once, different places' })
    }
    "differs ($($Disk.Count) on disk, $($Memory.matches) in memory)"
}

function New-SpikeDraft([string[]] $DiskOutput, $Report, [string] $ReportJson, [string] $Probes) {
    $disk = @{}
    foreach ($line in $DiskOutput) { if ($parsed = Read-DiskLine $line) { $disk[$parsed.Name] = $parsed } }
    $rows = foreach ($probe in $Report.probes) {
        $d = $disk[$probe.name]
        # @($probe.firstRvas) and not $probe.firstRvas alone: PowerShell treats an array holding just 0 as false.
        $memoryAt = (@($probe.firstRvas) | ForEach-Object { '0x{0:x}' -f [long]$_ }) -join ', '
        "| ``$($probe.name)`` | $(if ($d) { $d.Count } else { '?' }) | $(if ($d -and $d.Rva) { "0x$($d.Rva)" }) | $($probe.matches) | $memoryAt | $(Get-Verdict $d $probe) |"
    }
    $commit = git -C $Repo rev-parse --short HEAD
    # The file's name only, never its full path (ground rule 6), and where its probes came from.
    $probeSources = (@(Get-Content -Raw -LiteralPath $Probes | ConvertFrom-Json) | ForEach-Object { $_.source } |
        Select-Object -Unique | ForEach-Object { "``$_``" }) -join ', '
    @"
# Signature scans of the game on disk and in memory

- **Source:** a capture of one run of ``scripts/dev/session.ps1 spike`` at commit ``$commit``, on the maintainer's
  machine. Probes: ``$(Split-Path -Leaf $Probes)`` ($probeSources).
- **Saved:** $(Get-Date -Format yyyy-MM-dd).
- **Game build:** ``$($Report.gameBuildSha256)``.

## Steps

1. ``pwsh scripts/dev/session.ps1 spike``, with the game and Reloaded-II closed. It ran the disk check
   (``tools/Metaphor.SignatureCheck --probes``), then a smoke run with only our mod enabled, whose mod copied the
   game's main module through ``ReadProcessMemory`` when Reloaded-II started it and scanned it (ADR 0002: read only).

## Result

The memory copy was made $($Report.millisecondsSinceGameStart) ms after the game started: $($Report.readableBytes) of
$($Report.moduleBytes) bytes read, $($Report.unreadableBytes) failed. Disk addresses are converted from file offsets
through the exe's section table.

| Probe | Disk matches | Disk RVA | Memory matches | Memory RVAs | Verdict |
|---|---|---|---|---|---|
$($rows -join "`n")

## Disk check output

``````text
$($DiskOutput -join "`n")
``````

## Memory scan report

``````json
$($ReportJson.Trim())
``````
"@
}

# Sends one request line to the inspector and returns its one response line.
function Send-InspectorRequest($Inspector, [string] $Line) {
    $Inspector.Writer.WriteLine($Line)
    $response = $Inspector.Reader.ReadLine()
    if ($null -eq $response) { Stop-Session 'the inspector closed the connection' 'check the game is still running' 2 }
    $response
}

# Reads an inspect batch file: one request per line, sent as written; blank lines and lines starting with # are
# skipped, and "[name]" starts a section, which inspect -WaitKey runs one per Enter. Requests before the first section
# form one with no name. Returns the sections in file order, each with its requests and their line numbers. Refuses a
# line the inspector would refuse unread, so a mistake shows before the game is played, not mid-session.
function Read-InspectorBatch([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { Stop-Session "no batch file at $Path" 'pass the path of a text file of requests' }
    $sections = [Collections.Generic.List[object]]::new()
    $section = $null
    $number = 0
    foreach ($raw in [IO.File]::ReadAllLines($Path)) {
        $number++
        $line = $raw.Trim()
        if (-not $line -or $line.StartsWith('#')) { continue }
        if ($line -match '^\[(.*)\]$') {
            $name = $Matches[1].Trim()
            if ($name -cnotmatch '^[\x21-\x7e]+$') { Stop-Session "line $($number): section [$name] needs a name of printable ASCII with no spaces" 'rename it, as in [title-screen]' }
            $section = [pscustomobject]@{ Name = $name; Number = $number; Requests = [Collections.Generic.List[object]]::new() }
            $sections.Add($section)
            continue
        }
        if ($line.Length -gt $InspectorMaxLineBytes -or $line -cmatch '[^\x20-\x7e]') {
            Stop-Session "line $($number) is not a request the inspector reads" "keep each to printable ASCII and at most $InspectorMaxLineBytes characters"
        }
        if (-not $section) {
            $section = [pscustomobject]@{ Name = ''; Number = $number; Requests = [Collections.Generic.List[object]]::new() }
            $sections.Add($section)
        }
        $section.Requests.Add([pscustomobject]@{ Line = $line; Number = $number })
    }
    $empty = @($sections | Where-Object { -not $_.Requests.Count })
    if ($empty) { Stop-Session "line $($empty[0].Number): section [$($empty[0].Name)] has no requests" 'add some, or remove it' }
    if (-not $sections.Count) { Stop-Session "no request in $Path" "write one per line, as in: base" }
    , $sections.ToArray()
}

# The command names in the inspector's help line, "ok help | base | read <addr> <count> | ...; <addr> is ...".
function Get-InspectorCommandName([string] $Help) {
    if (-not $Help.StartsWith('ok ', [StringComparison]::Ordinal)) { return @() }
    @(($Help.Substring(3) -split ';')[0] -split '\|' | ForEach-Object { (-split $_)[0] } | Where-Object { $_ })
}

# Runs the batch's sections in order on one connection: each request's response is printed and appended to LOG as a
# line "<local time>\t<label>\t<request>\t<response>", where a section's label is "LABEL/<name>". With -WaitKey, each
# section waits for Enter on the console first, and "q" (or the end of input) stops there. Every command word is first
# checked against the inspector's own help, so an unknown one stops the batch before it sends anything.
function Invoke-InspectBatch($Inspector, $Sections, [string] $Label, [string] $Log) {
    $known = Get-InspectorCommandName (Send-InspectorRequest $Inspector 'help')
    if (-not $known) { Stop-Session "the inspector didn't list its commands" 'check it with: session.ps1 inspect help' 2 }
    $unknown = @(foreach ($section in $Sections) {
            foreach ($request in $section.Requests) {
                $word = (-split $request.Line)[0]
                if ($word -cnotin $known) { "line $($request.Number): unknown command '$word'" }
            }
        })
    if ($unknown) { Stop-Session "$($unknown -join '; ')" "fix the batch file; the inspector knows $($known -join ', ')" 2 }

    $waitKey = [bool] (Get-Option 'WaitKey' $false)
    $sent = 0
    $refused = 0
    $ran = 0
    foreach ($section in $Sections) {
        $name = if ($section.Name) { "$Label/$($section.Name)" } else { $Label }
        if ($waitKey) {
            Write-Line "next: $name, $($section.Requests.Count) requests - press Enter to run it, or q and Enter to stop"
            $answer = [Console]::In.ReadLine()
            if ($null -eq $answer -or $answer.Trim() -eq 'q') { break }
        }
        foreach ($request in $section.Requests) {
            $response = Send-InspectorRequest $Inspector $request.Line
            $stamp = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss.fffzzz', [Globalization.CultureInfo]::InvariantCulture)
            [IO.File]::AppendAllText($Log, (Hide-LocalPaths $script:Names "$stamp`t$name`t$($request.Line)`t$response") + "`n")
            Write-Line "[$name] $($request.Line) => $response"
            $sent++
            if (-not $response.StartsWith('ok', [StringComparison]::Ordinal)) { $refused++ }
        }
        $ran++
    }
    $done = "$ran of $($Sections.Count) sections run$(if ($ran -lt $Sections.Count) { ' (stopped by you)' }), logged to $Log"
    if ($refused) { Stop-Session "$refused of $sent requests answered err; $done" 'read the responses above; the batch went on past them' 1 }
    "$sent requests answered; $done"
}

# Asks the live inspector in a running Debug build (#291) each request, on one connection, and prints each response:
# "ok ..." or "err ...". The client checks the pipe belongs to the current user (PipeOptions.CurrentUserOnly), and only
# identifies itself: the server can't act as this user (#393).
# https://learn.microsoft.com/dotnet/api/system.security.principal.tokenimpersonationlevel
# With -Batch, the requests come from a file instead (Read-InspectorBatch) and each answer is also logged
# (Invoke-InspectBatch).
function Invoke-Inspect {
    $requests = @(Get-Option 'Request' @())
    $batch = Get-Option 'Batch' $null
    if ($batch) {
        if ($requests) { Stop-Session '-Batch takes no requests as arguments' 'put them in the batch file' }
        $sections = Read-InspectorBatch (Get-FullPath $batch)
        $label = Get-Option 'Label' ([IO.Path]::GetFileNameWithoutExtension($batch))
        if ($label -cnotmatch '^[\x21-\x7e]+$') { Stop-Session "-Label '$label' isn't a label" 'use printable ASCII with no spaces' }
        $save = Resolve-SaveFolder
        if (-not $save) { Stop-Session '-Batch needs -SaveLog' 'pass a folder outside any repository for the inspector log: answers can hold game text' }
        New-Item -ItemType Directory -Force $save | Out-Null
    }
    else {
        $batchOnly = @('Label', 'WaitKey', 'SaveLog' | Where-Object { $script:Options.ContainsKey($_) })
        if ($batchOnly) { Stop-Session "-$($batchOnly -join ', -') works only with -Batch" 'pass -Batch <file>, or drop it' }
        if (-not $requests) { Stop-Session 'no request' "pass one or more, as in: session.ps1 inspect 'help'" }
    }
    $pipeName = Get-Option 'PipeName' $InspectorPipeName
    $pipe = [System.IO.Pipes.NamedPipeClientStream]::new(
        '.', $pipeName, [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::CurrentUserOnly,
        [System.Security.Principal.TokenImpersonationLevel]::Identification)
    try {
        try { $pipe.Connect([int] (Get-Option 'TimeoutMilliseconds' 2000)) }
        catch {
            Stop-Session "no live inspector on \\.\pipe\$pipeName ($($_.Exception.Message.Trim()))" 'start a Debug build with: session.ps1 run -OptIn inspector' 2
        }
        $ascii = [System.Text.Encoding]::ASCII
        $writer = [System.IO.StreamWriter]::new($pipe, $ascii, 1024, $true)
        $writer.NewLine = "`n"
        $writer.AutoFlush = $true
        $reader = [System.IO.StreamReader]::new($pipe, $ascii, $false, 1024, $true)
        $inspector = [pscustomobject]@{ Writer = $writer; Reader = $reader }
        if ($batch) { return Invoke-InspectBatch $inspector $sections $label (Join-Path $save $InspectorLogFileName) }
        $refused = 0
        foreach ($line in $requests) {
            $response = Send-InspectorRequest $inspector $line
            Write-Host $response
            if (-not $response.StartsWith('ok', [System.StringComparison]::Ordinal)) { $refused++ }
        }
        if ($refused) { Stop-Session "$refused of $($requests.Count) requests answered err" 'read the responses above; "help" lists the commands' 1 }
        "$($requests.Count) requests answered"
    }
    finally { $pipe.Dispose() }
}

# Read-only: what is deployed, the game build, active opt-ins, whether the inspector is listening, the valid opt-ins.
function Invoke-Status($S) {
    $paths = $S.Paths
    $loader = if (Test-Path -LiteralPath $paths.Config) { Read-Json $paths.Config }
    if (-not $loader) { Write-Line "Reloaded-II: no config at $($paths.Config)" }
    elseif (Test-Path -LiteralPath "$($paths.Config)$BackupSuffix") {
        Write-Line 'Reloaded-II: a session was interrupted; the next deploy, run, smoke or spike restores its config first'
    }
    Write-Line "Reloaded-II: $(if ($paths.LauncherExe -and (Test-Path -LiteralPath $paths.LauncherExe)) { $paths.LauncherExe } else { 'not found (open it once, or set METAPHOR_RELOADED_EXE)' })"

    if ($paths.GameExe -and (Test-Path -LiteralPath $paths.GameExe)) {
        $build = Get-GameBuild $paths.GameExe $paths.KnownBuilds
        $known = if ($build.Known) { "Steam build $($build.Known.steamBuildId)" } else { 'unknown: docs/contributors/how-to/add-a-game-build.md' }
        Write-Line "game: $($paths.GameExe), build $($build.Sha256) ($known)$(if (Get-Game $paths.GameExe) { ', running' })"
    }
    else { Write-Line 'game: not found (register it in Reloaded-II, or set METAPHOR_GAME_EXE)' }

    $modConfig = Join-Path $paths.Project 'ModConfig.json'
    $modId = if (Test-Path -LiteralPath $modConfig) { [string] (Read-Json $modConfig).ModId }
    $folder = if ($loader -and $modId) { Add-PathName $script:Names '<mods>' ([string] $loader.ModConfigDirectory); Join-Path ([string] $loader.ModConfigDirectory) $modId }
    if ($folder -and (Test-Path -LiteralPath (Join-Path $folder 'ModConfig.json'))) {
        $application = if ($paths.GameExe) { Find-Application $loader $paths.GameExe }
        $enabled = $application -and (@((Read-Json $application.FullName).EnabledMods) -contains $modId)
        $built = (Get-Item -LiteralPath (Join-Path $folder 'ModConfig.json')).LastWriteTime.ToString('yyyy-MM-dd HH:mm')
        Write-Line "deployed: $modId in $folder, $(if ($enabled) { 'enabled' } else { 'not enabled' }), from $built"
        $active = @(Get-ChildItem -LiteralPath $folder -Filter 'opt-in-*.txt' -File | ForEach-Object { $_.Name -replace '^opt-in-|\.txt$' })
        Write-Line "opt-ins active: $(if ($active) { $active -join ', ' } else { 'none' })"
    }
    else { Write-Line "deployed: $(if ($modId) { $modId } else { 'the mod' }) is not deployed" }

    # The pipe listing, not a connection: the inspector serves one client at a time.
    $pipeName = Get-Option 'PipeName' $InspectorPipeName
    $listening = [IO.Directory]::GetFiles('\\.\pipe\') -contains "\\.\pipe\$pipeName"
    Write-Line "inspector: $(if ($listening) { "listening on \\.\pipe\$pipeName" } else { 'not listening' })"
    $valid = Get-OptInName $paths.Project
    Write-Line "valid opt-ins: $(if ($valid) { $valid -join ', ' } else { 'none' })"
    'status read'
}

# Runs VERB with OPTIONS (session.ps1's parameters, by name), prints its lines and one summary line, "ok: ..." or
# "failed: ... - what to do", and returns the exit code: 0 ok; 1 failed after the game was launched or the mod was
# installed, or a cleanup failed (or, for inspect, a request answered err); 2 failed with nothing launched or changed.
function Invoke-GameSession {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [ValidateSet('deploy', 'run', 'smoke', 'spike', 'inspect', 'status')] [string] $Verb,
        [hashtable] $Options = @{}
    )
    $script:Names = [ordered]@{}
    $script:Options = $Options
    Add-PathName $script:Names '<repo>' $Repo
    Add-PathName $script:Names '<temp>' ([IO.Path]::GetTempPath())
    Add-PathName $script:Names '~' ([Environment]::GetFolderPath('UserProfile'))
    $S = [pscustomobject]@{
        Paths = $null; Loader = $null; Application = $null; Build = $null; Deadline = $null; Launched = $false; Changed = $false
        KeepGame = $false; OptInFiles = [Collections.Generic.List[string]]::new(); TempFolders = [Collections.Generic.List[string]]::new()
    }
    $line = $null
    $code = 0
    try {
        $extra = @($Options.Keys | Where-Object { $_ -notin $VerbOptions[$Verb] } | Sort-Object)
        if ($extra) { Stop-Session "$Verb doesn't take -$($extra -join ', -')" 'see: Get-Help scripts/dev/session.ps1 -Full' }
        if ($Verb -ne 'inspect') { $S.Paths = Find-SessionPaths }
        $message = switch ($Verb) {
            'deploy' { Invoke-Deploy $S }
            'run' { Invoke-Run $S }
            'smoke' { Invoke-Smoke $S }
            'spike' { Invoke-Spike $S }
            'inspect' { Invoke-Inspect }
            'status' { Invoke-Status $S }
        }
        $line = "ok: $message"
    }
    catch [SessionStop] {
        $code = if ($_.Exception.ExitCode) { $_.Exception.ExitCode } elseif ($S.Launched -or $S.Changed) { 1 } else { 2 }
        $line = "failed: $($_.Exception.Message) - $($_.Exception.Fix)"
    }
    catch {
        $changed = $S.Launched -or $S.Changed
        $code = if ($changed) { 1 } else { 2 }
        $line = "failed: $($_.Exception.Message) - $(if ($changed) { 'check the lines above, then run status' } else { 'fix it and run again' })"
    }
    finally {
        # Only a stop (Ctrl+C) leaves no line; the session's own cleanup has run by then.
        if (-not $line) { $line = 'failed: interrupted - the session cleaned up as the lines above say; run status to check' }
        Write-Line $line
    }
    $code
}

Export-ModuleMember -Function Invoke-GameSession
