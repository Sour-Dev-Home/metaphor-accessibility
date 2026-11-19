# The players' installer's steps (#303); install.ps1 runs them in order, says each one and stops at the first that
# fails. Each step returns { State (done, unchanged or failed); Line, spoken and printed, in plain words; Detail,
# printed only, with a path or code; Fix, what to do when it failed }, and changes nothing that is already right, so a
# second run repairs or updates an install and says what it left alone. It never deletes a file it didn't put there.
#
# Downloads come only from install-pins.json: an https URL (a local file:// one only for the tests, Test-TestSwitch), a
# file name, a SHA-256 and, for the Microsoft installers, the signer; a file that doesn't match is deleted unused. What is pinned and why (Reloaded-II's own Setup.exe
# downloads unpinned files): docs/raw-sources/spoken-installer-sources-2026-11-19.md. Ships in the release beside the
# mod, so it must load in Windows PowerShell 5.1: ASCII only.
#
# The idea of an installer that also installs Reloaded-II and turns the mod on without Reloaded-II's window comes from
# Persona-4-Golden-Access (https://github.com/AquaRose7/Persona-4-Golden-Access, GPL-3.0, at dcff255), whose installer's
# source isn't published. No code is taken (docs/raw-sources/persona-4-golden-access-2026-11-16.md).

Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ReloadedRead.psm1'), (Join-Path $PSScriptRoot 'GameLocator.psm1')

$ShortcutName = 'Metaphor ReFantazio with accessibility.lnk'
$PackageListName = 'package-files.txt'
# Windows Installer's codes for success, success after a restart, and a newer version already installed; and for a
# cancelled install.
$InstallerOk = 0, 3010, 1638
$RestartNeeded = 3010
$Cancelled = 1602

function New-Step([string] $State, [string] $Line, [string] $Detail, [string] $Fix) {
    [pscustomobject]@{ State = $State; Line = $Line; Detail = $Detail; Fix = $Fix }
}
function Done([string] $Line, [string] $Detail) { New-Step 'done' $Line $Detail $null }
function Unchanged([string] $Line, [string] $Detail) { New-Step 'unchanged' $Line $Detail $null }
function Failed([string] $Line, [string] $Fix, [string] $Detail) { New-Step 'failed' $Line $Detail $Fix }

function Test-Yes($Answer) { [bool]($null -ne $Answer -and $Answer -match '^\s*(y|yes)\s*$') }

function Get-Property($Object, [string] $Name) {
    if ($null -ne $Object -and $Object.PSObject.Properties[$Name]) { return $Object.$Name }
    return $null
}

# JSON as Reloaded-II writes it: UTF-8 without a byte order mark, which Windows PowerShell 5.1's Set-Content can't do.
# Written beside the file first and then swapped in, so a failed write never leaves half a file; a file replaced is
# kept as PATH.bak. A PATH.bak already there is kept: it is the file from before the installer first changed it (#408).
function Write-JsonFile([string] $Path, $Value) {
    New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
    $temporary = "$Path.tmp"
    [IO.File]::WriteAllText($temporary, ($Value | ConvertTo-Json -Depth 20), (New-Object Text.UTF8Encoding $false))
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        $backup = if (Test-Path -LiteralPath "$Path.bak") { [NullString]::Value } else { "$Path.bak" }
        [IO.File]::Replace($temporary, $Path, $backup)
    }
    else { [IO.File]::Move($temporary, $Path) }
}

# True when PATH names Reloaded-II's launcher by its file name; another program is never started or linked to.
function Test-LauncherName([string] $Path) { [bool]($Path -and (Split-Path -Leaf $Path) -eq 'Reloaded-II.exe') }

# The tests' switches, read from the environment (tests/smoke/install.Tests.ps1). METAPHOR_INSTALL_TEST_FILES lets a pin
# be a local file:// URL; METAPHOR_INSTALL_TEST_UNSIGNED, only for such a pin, skips the signature check. The shipped
# pins are https, so neither changes what a player's run downloads or checks.
function Test-TestSwitch([string] $Name) { [Environment]::GetEnvironmentVariable("METAPHOR_INSTALL_TEST_$Name") -eq '1' }

# True when this process runs as administrator. Reloaded-II isn't meant to run as admin, and nothing but the two
# runtime installers needs it (they ask for it themselves), so install.ps1 refuses. The tests set
# METAPHOR_INSTALL_TEST_ALLOW_ADMIN, since CI's runners are admin; it counts only with CI=true, as CI sets, so a player
# can't turn the refusal off by accident (#408).
function Test-Elevated {
    if ((Test-TestSwitch 'ALLOW_ADMIN') -and $env:CI -eq 'true') { return $false }
    Test-AdministratorRole
}

function Test-AdministratorRole {
    ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-StreamSha256([IO.Stream] $Stream) {
    $hash = [Security.Cryptography.SHA256]::Create()
    try { -join ($hash.ComputeHash($Stream) | ForEach-Object { $_.ToString('x2') }) } finally { $hash.Dispose() }
}

function Test-SamePath([string] $A, [string] $B) {
    [IO.Path]::GetFullPath($A).TrimEnd('\', '/') -eq [IO.Path]::GetFullPath($B).TrimEnd('\', '/')
}

# What install.ps1 passes every step, and what the steps find for the later ones (Launcher, Loader, ModsFolder,
# AppsFolder, GameExe). ASK takes a question and returns the answer typed, '' for Enter, or $null for none; SAY takes a
# printed line and a spoken one.
function New-InstallContext([string] $ReleaseFolder, [string] $PinsPath, [string] $ConfigPath, [string] $ReloadedFolder,
    [string] $SteamRoot, [string] $Desktop, [string] $DotnetRoot, [string] $VcRuntimeKey, [string] $WorkFolder,
    [scriptblock] $Ask, [scriptblock] $Say) {
    $modConfig = Read-Json (Join-Path $ReleaseFolder 'ModConfig.json')
    [pscustomobject]@{
        ReleaseFolder = $ReleaseFolder; Pins = (Read-Json $PinsPath); ConfigPath = $ConfigPath
        ReloadedFolder = $ReloadedFolder; SteamRoot = $SteamRoot; Desktop = $Desktop; DotnetRoot = $DotnetRoot
        VcRuntimeKey = $VcRuntimeKey; WorkFolder = $WorkFolder; Ask = $Ask; Say = $Say
        ModId = [string] $modConfig.ModId; ModName = [string] (Get-Property $modConfig 'ModName')
        ModVersion = [string] (Get-Property $modConfig 'ModVersion')
        Launcher = $null; Loader = $null; ModsFolder = $null; AppsFolder = $null; GameExe = $null; InstalledMod = $null
    }
}

# Downloads PIN into the work folder and checks it: { Path; Stream } on success, else { Path = $null; Step }, a failed
# step naming what went wrong. STREAM holds the file open, readable by others but not writable or deletable, from
# before it is hashed until the caller disposes it once tar or the installer has returned, so the file that runs is the
# file that was checked. A pin with a signer must also carry a valid Authenticode signature with exactly that subject.
function Get-PinnedFile($Context, $Pin) {
    $name = [string] $Pin.fileName
    if ($name -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw "install-pins.json: '$name' is not a plain file name" }
    $target = Join-Path $Context.WorkFolder $name
    $uri = [Uri] $Pin.url
    try {
        if ($uri.IsFile -and $uri.IsUnc) { throw "install-pins.json: no network file URLs, not $($Pin.url)" }
        elseif ($uri.IsFile -and (Test-TestSwitch 'FILES')) { Copy-Item -LiteralPath $uri.LocalPath $target -Force }
        elseif ($uri.Scheme -ne 'https') { throw "install-pins.json: only https downloads, not $($Pin.url)" }
        else {
            # Windows PowerShell 5.1 may not offer TLS 1.2 by default, which GitHub requires; its progress bar slows a
            # download many times over.
            [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
            $progress = $ProgressPreference
            $ProgressPreference = 'SilentlyContinue'
            try { Invoke-WebRequest -Uri $uri -OutFile $target -UseBasicParsing } finally { $ProgressPreference = $progress }
        }
    }
    catch {
        if ($_.Exception.Message -like 'install-pins.json:*') { throw }
        return [pscustomobject]@{ Path = $null; Step = (Failed "The download of $($Pin.name) failed." `
                    'Check that this computer is online, then run the installer again.' "$($Pin.url): $($_.Exception.Message)") }
    }
    $stream = [IO.File]::Open($target, 'Open', 'Read', 'Read')
    $problem = $null
    try { $sha256 = Get-StreamSha256 $stream } catch { $stream.Dispose(); throw }
    if ($sha256 -ne $Pin.sha256) { $problem = "SHA-256 $sha256, expected $($Pin.sha256)" }
    elseif ((Get-Property $Pin 'signer') -and -not ($uri.IsFile -and (Test-TestSwitch 'UNSIGNED'))) {
        $signature = $null
        try { $signature = Get-AuthenticodeSignature -LiteralPath $target } catch { $problem = "signature not checked: $($_.Exception.Message)" }
        if ($signature -and ([string] $signature.Status -ne 'Valid' -or -not $signature.SignerCertificate -or
                $signature.SignerCertificate.Subject -cne $Pin.signer)) {
            $subject = if ($signature.SignerCertificate) { $signature.SignerCertificate.Subject } else { 'none' }
            $problem = "signature $($signature.Status), signer $subject, expected $($Pin.signer)"
        }
    }
    if ($problem) {
        $stream.Dispose()
        Remove-Item -LiteralPath $target -Force
        return [pscustomobject]@{ Path = $null; Stream = $null; Step = (Failed "The downloaded $($Pin.name) is not the file this installer expects, so it was deleted unused." `
                    'Run the installer again later. If it happens again, report it with the alpha feedback form.' "$($Pin.url): $problem") }
    }
    [pscustomobject]@{ Path = $target; Stream = $stream; Step = $null }
}

# Unpacks ARCHIVE (zip or 7z) into DESTINATION with Windows' own tar.exe, by its full path. Without -P it refuses
# entries with .. or an absolute path (bsdtar's manual, in the raw source). True when it worked.
function Expand-PinnedArchive([string] $Archive, [string] $Destination) {
    $tar = Join-Path ([Environment]::SystemDirectory) 'tar.exe'
    if (-not (Test-Path -LiteralPath $tar -PathType Leaf)) { return $false }
    New-Item -ItemType Directory -Force $Destination | Out-Null
    $errors = Join-Path (Split-Path $Archive) 'tar-errors.txt'
    $process = Start-Process -FilePath $tar -ArgumentList "-x -f `"$Archive`" -C `"$Destination`"" -Wait -PassThru `
        -NoNewWindow -RedirectStandardError $errors
    return $process.ExitCode -eq 0
}

# Asks QUESTION, a yes/no one where Enter means no.
function Confirm-Install($Context, [string] $Question) { Test-Yes (& $Context.Ask "$Question [y/N]") }

# 2. Reloaded-II: the one Reloaded-II's settings name (GameLocator.psm1, without the developer scripts'
# METAPHOR_RELOADED_EXE), or one this installer put in its folder before; else the pinned Release.zip, unpacked there
# after a yes. No admin rights needed.
function Install-Reloaded($Context) {
    $pin = $Context.Pins.reloaded
    $launcher = Find-ReloadedLauncher -ConfigPath $Context.ConfigPath -IgnoreEnvironment
    $ours = Join-Path $Context.ReloadedFolder 'Reloaded-II.exe'
    if (-not $launcher -and (Test-Path -LiteralPath $ours -PathType Leaf)) { $launcher = $ours }
    if ($launcher -and -not (Test-LauncherName $launcher)) {
        return Failed 'Reloaded-II''s records name a program that is not Reloaded-II.exe, so the installer did not use it.' `
            'Start Reloaded-II once from its own Reloaded-II.exe, then run the installer again.' "Program: $launcher"
    }
    if ($launcher) {
        $Context.Launcher = [IO.Path]::GetFullPath($launcher)
        return Unchanged 'Reloaded-II is already installed.' "Program: $($Context.Launcher)"
    }
    if ((Test-Path -LiteralPath $Context.ReloadedFolder) -and @(Get-ChildItem -LiteralPath $Context.ReloadedFolder -Force).Count -gt 0) {
        return Failed 'The folder this installer puts Reloaded-II in already holds other files, so it was left alone.' `
            'Empty or move that folder, or install Reloaded-II yourself and start it once, then run the installer again.' "Folder: $($Context.ReloadedFolder)"
    }
    if (-not (Confirm-Install $Context "Reloaded-II, which loads the mod into the game, is not installed. Download Reloaded-II $($pin.version) from its GitHub page and install it in your user folder? This does not need administrator rights.")) {
        return Failed 'Reloaded-II was not installed, because the answer was no.' `
            "Run the installer again and answer yes, or install Reloaded-II $($pin.version) yourself and start it once."
    }
    & $Context.Say "Downloading Reloaded-II $($pin.version) from $($pin.url)" "Downloading Reloaded-II."
    $file = Get-PinnedFile $Context $pin
    if (-not $file.Path) { return $file.Step }
    try { $expanded = Expand-PinnedArchive $file.Path $Context.ReloadedFolder } finally { $file.Stream.Dispose() }
    if (-not $expanded -or -not (Test-Path -LiteralPath $ours -PathType Leaf)) {
        Remove-Item -LiteralPath $Context.ReloadedFolder -Recurse -Force -ErrorAction SilentlyContinue
        return Failed 'Reloaded-II could not be unpacked.' 'Run the installer again. If it fails again, report it with the alpha feedback form.' `
            "Archive: $($file.Path)"
    }
    $Context.Launcher = [IO.Path]::GetFullPath($ours)
    Done "Reloaded-II $($pin.version) is installed." "Folder: $($Context.ReloadedFolder)"
}

# 3. Reloaded-II's settings file. One that exists is read, never rewritten. Without one (a Reloaded-II that has never
# started), it writes the three entries the mod and the install check need, with the folders Reloaded-II uses by
# default beside its program; Reloaded-II fills in the rest when it starts the game
# (docs/raw-sources/reloaded-ii-config-location-2026-11-13.md; spoken-installer-sources-2026-11-19.md).
function Initialize-ReloadedConfig($Context) {
    $result = $null
    if (Test-Path -LiteralPath $Context.ConfigPath -PathType Leaf) {
        try { $Context.Loader = Read-Json $Context.ConfigPath } catch { $Context.Loader = $null }
        if ($null -eq $Context.Loader) {
            return Failed 'Reloaded-II''s settings file could not be read.' `
                'Start Reloaded-II once, so it writes its settings file again, then run the installer again.' "Settings file: $($Context.ConfigPath)"
        }
        $result = Unchanged 'Reloaded-II''s settings are already in place.' "Settings file: $($Context.ConfigPath)"
    }
    else {
        $folder = Split-Path $Context.Launcher
        $Context.Loader = [pscustomobject]@{
            LauncherPath = $Context.Launcher
            ApplicationConfigDirectory = (Join-Path $folder 'Apps')
            ModConfigDirectory = (Join-Path $folder 'Mods')
        }
        Write-JsonFile $Context.ConfigPath $Context.Loader
        $result = Done 'Reloaded-II''s settings file is written, with its own folders for mods and games.' "Settings file: $($Context.ConfigPath)"
    }
    $Context.ModsFolder = [string] (Get-Property $Context.Loader 'ModConfigDirectory')
    $Context.AppsFolder = [string] (Get-Property $Context.Loader 'ApplicationConfigDirectory')
    if (-not $Context.ModsFolder -or -not $Context.AppsFolder) {
        return Failed 'Reloaded-II''s settings file does not name its folders for mods and games.' `
            'Start Reloaded-II once, so it writes its settings file again, then run the installer again.' "Settings file: $($Context.ConfigPath)"
    }
    foreach ($folder in $Context.ModsFolder, $Context.AppsFolder) { New-Item -ItemType Directory -Force $folder | Out-Null }
    $result
}

# The .NET runtimes under DOTNETROOT: each of FRAMEWORKS at MINIMUM or a later version with the same major, as
# dotnet --list-runtimes would list them.
function Test-DotnetRuntime([string] $DotnetRoot, [string[]] $Frameworks, [version] $Minimum) {
    foreach ($framework in $Frameworks) {
        $folder = Join-Path $DotnetRoot "shared\$framework"
        $found = @(Get-ChildItem -LiteralPath $folder -Directory -ErrorAction SilentlyContinue | Where-Object {
                $version = $null
                [version]::TryParse($_.Name, [ref] $version) -and $version.Major -eq $Minimum.Major -and $version -ge $Minimum
            })
        if ($found.Count -eq 0) { return $false }
    }
    $true
}

# The Visual C++ runtime, by the registry key Microsoft documents for it.
function Test-VcRuntime([string] $Key) {
    $values = Get-ItemProperty -LiteralPath $Key -ErrorAction SilentlyContinue
    [bool]($values -and $values.PSObject.Properties['Version'] -and $values.Version)
}

# 4 and 5. A runtime Reloaded-II needs: already there, or the pinned Microsoft installer, run quietly after a yes. Its
# own admin prompt follows; this script never runs as admin itself.
function Install-Runtime($Context, $Pin, [scriptblock] $Test) {
    if (& $Test) { return Unchanged "$($Pin.name) is already installed." }
    $question = "$($Pin.name), which Reloaded-II needs, is not installed. Download version $($Pin.version) from Microsoft and install it? This needs administrator rights: Windows will ask for permission, and you answer yes there too."
    if (-not (Confirm-Install $Context $question)) {
        return Failed "$($Pin.name) was not installed, because the answer was no." `
            "Run the installer again and answer yes, or install $($Pin.name) yourself from Microsoft's website."
    }
    & $Context.Say "Downloading $($Pin.name) $($Pin.version) from $($Pin.url)" "Downloading $($Pin.name)."
    $file = Get-PinnedFile $Context $Pin
    if (-not $file.Path) { return $file.Step }
    & $Context.Say "Installing $($Pin.name). Windows asks for administrator permission now: answer yes." `
        "Installing $($Pin.name). Windows asks for administrator permission now. Answer yes."
    try { $code = (Start-Process -FilePath $file.Path -ArgumentList '/install', '/quiet', '/norestart' -Wait -PassThru -WindowStyle Hidden).ExitCode }
    catch { $code = $Cancelled }
    finally { $file.Stream.Dispose() }
    if ($code -eq $Cancelled) {
        return Failed "The install of $($Pin.name) was cancelled, maybe at Windows' permission prompt." `
            "Run the installer again, and answer yes at Windows' permission prompt."
    }
    if ($InstallerOk -notcontains $code -or -not (& $Test)) {
        return Failed "The install of $($Pin.name) did not work." `
            "Restart Windows and run the installer again. If it fails again, install $($Pin.name) yourself from Microsoft's website." "Exit code: $code"
    }
    if ($code -eq $RestartNeeded) { return Done "$($Pin.name) is installed. Windows asked for a restart to finish it: restart before you play." }
    Done "$($Pin.name) $($Pin.version) is installed."
}

function Install-DotnetRuntime($Context) {
    $pin = $Context.Pins.dotnetDesktop
    $check = { Test-DotnetRuntime $Context.DotnetRoot @($pin.frameworks) ([version] $pin.minimumVersion) }.GetNewClosure()
    Install-Runtime $Context $pin $check
}

function Install-VcRuntime($Context) {
    $key = $Context.VcRuntimeKey
    Install-Runtime $Context $Context.Pins.vcRedist { Test-VcRuntime $key }.GetNewClosure()
}

# The folder in the mods folder whose ModConfig.json names MODID, or $null.
function Find-ModFolder([string] $ModsFolder, [string] $ModId) {
    Get-ChildItem -LiteralPath $ModsFolder -Directory -ErrorAction SilentlyContinue | Where-Object {
        $config = Join-Path $_.FullName 'ModConfig.json'
        try { (Test-Path -LiteralPath $config -PathType Leaf) -and [string] (Get-Property (Read-Json $config) 'ModId') -eq $ModId } catch { $false }
    } | Select-Object -First 1
}

# 6. Reloaded-II's hooks library, which the loader loads on every start and Reloaded-II's window would otherwise
# download: already in the mods folder, or the pinned release unpacked there after a yes.
function Install-HooksLibrary($Context) {
    $pin = $Context.Pins.hooksLibrary
    if (Find-ModFolder $Context.ModsFolder $pin.modId) { return Unchanged "$($pin.name) is already installed." }
    $target = Join-Path $Context.ModsFolder $pin.modId
    if (Test-Path -LiteralPath $target) {
        return Failed "A folder for $($pin.name) is in Reloaded-II's mods folder but is not that library, so it was left alone." `
            'Move that folder out of the mods folder, then run the installer again.' "Folder: $target"
    }
    if (-not (Confirm-Install $Context "Reloaded-II's hooks library, which the mod needs, is not installed. Download version $($pin.version) from its GitHub page?")) {
        return Failed "$($pin.name) was not installed, because the answer was no." `
            'Run the installer again and answer yes, or open Reloaded-II once: it downloads the library itself.'
    }
    & $Context.Say "Downloading $($pin.name) $($pin.version) from $($pin.url)" "Downloading $($pin.name)."
    $file = Get-PinnedFile $Context $pin
    if (-not $file.Path) { return $file.Step }
    try { $expanded = Expand-PinnedArchive $file.Path $target } finally { $file.Stream.Dispose() }
    if (-not $expanded -or -not (Find-ModFolder $Context.ModsFolder $pin.modId)) {
        Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction SilentlyContinue
        return Failed "Windows could not unpack $($pin.name)." `
            'Open Reloaded-II once: it downloads the library itself. Then run the installer again.' "Archive: $($file.Path)"
    }
    Done "$($pin.name) $($pin.version) is installed." "Folder: $target"
}

# The game's AppConfig.json file in Reloaded-II's applications folder, or $null (also when LOADER is $null).
function Find-GameApplication($Loader) {
    $apps = [string] (Get-Property $Loader 'ApplicationConfigDirectory')
    if (-not $apps -or -not (Test-Path -LiteralPath $apps -PathType Container)) { return $null }
    Find-MetaphorApplication $Loader
}

# 1. The game, first, so nothing is downloaded for a computer without it: the one added to Reloaded-II, else one found
# in Steam (GameLocator.psm1), confirmed or picked, else a path typed in.
function Select-InstallGame($Context) {
    $loader = $null
    if (Test-Path -LiteralPath $Context.ConfigPath -PathType Leaf) { try { $loader = Read-Json $Context.ConfigPath } catch { $loader = $null } }
    $application = Find-GameApplication $loader
    if ($application) {
        $location = [string] (Get-Property (Read-Json $application.FullName) 'AppLocation')
        $registered = if ($location) { Resolve-AppLocation $application.FullName $location } else { $null }
        if ($registered -and (Test-Path -LiteralPath $registered -PathType Leaf)) {
            $Context.GameExe = $registered
            return Unchanged 'The game is already added to Reloaded-II.' "Game: $registered"
        }
    }
    $locate = @{ ConfigPath = $Context.ConfigPath }
    if ($Context.SteamRoot) { $locate.SteamRoot = $Context.SteamRoot }
    $found = @(Find-MetaphorGame @locate)
    $path = $null
    if ($found.Count -eq 1) {
        & $Context.Say "Found the game: $($found[0])" 'Found the game in Steam.'
        $answer = & $Context.Ask 'Use it? [Y/n]'
        if ($null -ne $answer -and $answer -match '^\s*(y|yes)?\s*$') { $path = $found[0] }
    }
    elseif ($found.Count -gt 1) {
        & $Context.Say 'Found the game in more than one place:' "Found the game in $($found.Count) places."
        for ($i = 0; $i -lt $found.Count; $i++) { & $Context.Say "  $($i + 1). $($found[$i])" "Number $($i + 1): $($found[$i])" }
        $answer = & $Context.Ask "Which one? Type its number. [1-$($found.Count)]"
        if ($answer -match '^\s*\d+\s*$' -and [int]$answer -ge 1 -and [int]$answer -le $found.Count) { $path = $found[[int]$answer - 1] }
    }
    else {
        $answer = & $Context.Ask 'Did not find the game in Steam. Type the full path to METAPHOR.exe, or press Enter to stop:'
        $typed = if ($answer) { $answer.Trim().Trim('"') } else { '' }
        if ($typed -and (Split-Path -Leaf $typed) -eq 'METAPHOR.exe' -and (Test-Path -LiteralPath $typed -PathType Leaf)) {
            $path = [IO.Path]::GetFullPath($typed)
        }
    }
    if (-not $path) {
        return Failed 'No game was chosen.' 'Run the installer again and choose the game, or type the full path to METAPHOR.exe in the game''s install folder.'
    }
    $Context.GameExe = $path
    Done 'The game is found.' "Game: $path"
}

# The package's own files, as package-files.txt lists them (scripts/package-mod.sh writes it), relative paths with
# forward slashes; the list itself included. $null when the list is missing or names a path outside the folder, or a
# file that isn't there.
function Get-PackageFile([string] $Folder) {
    $list = Join-Path $Folder $PackageListName
    if (-not (Test-Path -LiteralPath $list -PathType Leaf)) { return $null }
    $files = @($PackageListName)
    foreach ($line in Get-Content -LiteralPath $list) {
        if (-not $line) { continue }
        if ($line -cnotmatch '^[A-Za-z0-9._-]+(/[A-Za-z0-9._-]+)*$' -or $line -match '(^|/)\.\.?(/|$)') { return $null }
        if (-not (Test-Path -LiteralPath (Join-Path $Folder $line) -PathType Leaf)) { return $null }
        $files += $line
    }
    $files
}

# 7. The mod's files: the package's own (Get-PackageFile), copied into the mods folder from the folder this runs from,
# each only when it is missing or differs. Other files in either folder (the mod's status file, say) are left alone.
function Install-ModFiles($Context) {
    $existing = Find-ModFolder $Context.ModsFolder $Context.ModId
    $target = if ($existing) { $existing.FullName } else { Join-Path $Context.ModsFolder $Context.ModId }
    $Context.InstalledMod = $target
    if (Test-SamePath $target $Context.ReleaseFolder) {
        return Unchanged 'The installer is running from the installed mod, so its files are already in place.' "Folder: $target"
    }
    $source = [IO.Path]::GetFullPath($Context.ReleaseFolder).TrimEnd('\', '/')
    $files = Get-PackageFile $source
    if (-not $files) {
        return Failed 'The mod''s folder is missing its list of files, or the list does not match the folder.' `
            'Unzip the release zip again, and run install.ps1 from the new folder.' "Folder: $source"
    }
    $copied = 0
    foreach ($relative in $files) {
        $from = Join-Path $source $relative
        $destination = Join-Path $target $relative
        if ((Test-Path -LiteralPath $destination -PathType Leaf) -and (Get-Sha256 $destination) -eq (Get-Sha256 $from)) { continue }
        New-Item -ItemType Directory -Force (Split-Path $destination) | Out-Null
        Copy-Item -LiteralPath $from $destination -Force
        $copied++
    }
    if ($copied -eq 0) { return Unchanged "The mod's files are already up to date." "Folder: $target" }
    Done "Copied $copied of the mod's files into Reloaded-II's mods folder." "Folder: $target"
}

# 8. The game in Reloaded-II with the mod turned on: the AppConfig.json Reloaded-II has for the game, with the mod added
# to its EnabledMods and its other entries kept, or a new one (reloaded-ii-app-and-mod-config-2026-11-15.md).
function Enable-Mod($Context) {
    $application = Find-GameApplication $Context.Loader
    if ($application) {
        $path = $application.FullName
        $app = Read-Json $path
        $changes = @()
        $location = [string] (Get-Property $app 'AppLocation')
        if (-not $location -or -not (Test-SamePath (Resolve-AppLocation $path $location) $Context.GameExe)) {
            $app | Add-Member -NotePropertyName AppLocation -NotePropertyValue $Context.GameExe -Force
            $changes += 'pointed at the game'
        }
        $enabled = @(Get-Property $app 'EnabledMods' | Where-Object { $_ })
        if ($enabled -notcontains $Context.ModId) {
            $app | Add-Member -NotePropertyName EnabledMods -NotePropertyValue ([object[]] ($enabled + $Context.ModId)) -Force
            $changes += 'turned the mod on'
        }
        if ($changes.Count -eq 0) { return Unchanged 'The mod is already turned on for the game.' "Game settings: $path" }
        Write-JsonFile $path $app
        return Done "In Reloaded-II's settings for the game, this $($changes -join ' and ')." "Game settings: $path"
    }
    $path = Join-Path $Context.AppsFolder 'metaphor.exe\AppConfig.json'
    if (Test-Path -LiteralPath $path) {
        return Failed 'Reloaded-II has a settings file for another program where the game''s belongs, so it was left alone.' `
            'Move that folder out of Reloaded-II''s Apps folder, then run the installer again.' "File: $path"
    }
    Write-JsonFile $path ([pscustomobject]@{
            AppId = 'metaphor.exe'; AppName = 'METAPHOR'; AppLocation = $Context.GameExe; AppArguments = ''
            EnabledMods = [object[]] @($Context.ModId)
        })
    Done 'The game is added to Reloaded-II with the mod turned on.' "Game settings: $path"
}

# 9. A desktop shortcut that starts the game through Reloaded-II with its mods (--launch, as Reloaded-II's FAQ gives it:
# reloaded-ii-launch-and-logs-2026-11-13.md). Rewritten only when it points elsewhere.
function Install-Shortcut($Context) {
    $path = Join-Path $Context.Desktop $ShortcutName
    if (-not (Test-LauncherName $Context.Launcher)) { throw "the shortcut would start $($Context.Launcher), which is not Reloaded-II.exe" }
    $arguments = "--launch `"$($Context.GameExe)`""
    $folder = Split-Path $Context.Launcher
    $shell = New-Object -ComObject WScript.Shell
    try {
        $existed = Test-Path -LiteralPath $path -PathType Leaf
        $link = $shell.CreateShortcut($path)
        if ($existed -and $link.TargetPath -eq $Context.Launcher -and $link.Arguments -eq $arguments -and $link.WorkingDirectory -eq $folder) {
            return Unchanged "The desktop shortcut is already in place: $([IO.Path]::GetFileNameWithoutExtension($ShortcutName))." "Shortcut: $path"
        }
        $link.TargetPath = $Context.Launcher
        $link.Arguments = $arguments
        $link.WorkingDirectory = $folder
        $link.Description = 'Starts Metaphor: ReFantazio through Reloaded-II, with the accessibility mod.'
        New-Item -ItemType Directory -Force $Context.Desktop | Out-Null
        $link.Save()
    }
    finally { [void] [Runtime.InteropServices.Marshal]::ReleaseComObject($shell) }
    Done "The desktop shortcut is made: $([IO.Path]::GetFileNameWithoutExtension($ShortcutName))." "Shortcut: $path"
}

Export-ModuleMember -Function New-InstallContext, Install-Reloaded, Initialize-ReloadedConfig, Install-DotnetRuntime,
    Install-VcRuntime, Install-HooksLibrary, Select-InstallGame, Install-ModFiles, Enable-Mod, Install-Shortcut,
    Test-DotnetRuntime, Test-VcRuntime, Test-Elevated -Variable ShortcutName
