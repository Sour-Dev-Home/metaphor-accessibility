# Tests for scripts/release/installer-exe.ps1 (#608, #613) and the exe it builds, with a stand-in release zip whose
# install.ps1 records how it was started. Builds the launcher with the pinned SDK; no network beyond its restore.

BeforeAll {
    $script:buildScript = (Resolve-Path "$PSScriptRoot/../../scripts/release/installer-exe.ps1").Path
    $script:project = (Resolve-Path "$PSScriptRoot/../../installer/Metaphor.Installer/Metaphor.Installer.csproj").Path
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    function Get-Sha256([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

    # A release-shaped zip: the mod's folder with an install.ps1 that writes its PowerShell version, folder, switch,
    # other arguments and module path to $env:INSTALLER_EXE_RECORD, then exits 3.
    function New-ReleaseZip([string] $Path, [hashtable] $More = @{}) {
        $files = @{
            'Install Metaphor accessibility.cmd' = '@echo off'
            'm/ModConfig.json'                   = '{ "ModId": "m" }'
            'm/install.ps1'                      = @'
param([switch] $FromLauncher, [Parameter(ValueFromRemainingArguments)] $Rest)
[pscustomobject]@{ Version = $PSVersionTable.PSVersion.Major; Folder = $PSScriptRoot; FromLauncher = [bool] $FromLauncher
    Rest = @($Rest); ModulePath = $env:PSModulePath } | ConvertTo-Json | Set-Content -LiteralPath $env:INSTALLER_EXE_RECORD
exit 3
'@
        }
        foreach ($name in $More.Keys) { $files[$name] = $More[$name] }
        $stream = [IO.File]::Open($Path, [IO.FileMode]::Create)
        try {
            $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
            try {
                foreach ($name in $files.Keys) {
                    $writer = [IO.StreamWriter]::new($zip.CreateEntry($name).Open())
                    try { $writer.Write($files[$name]) } finally { $writer.Dispose() }
                }
            } finally { $zip.Dispose() }
        } finally { $stream.Dispose() }
    }

    function Invoke-Build([string] $Zip, [string] $Destination) {
        $output = & pwsh -NoProfile -File $script:buildScript -Zip $Zip -Destination $Destination 2> (Join-Path $TestDrive 'build-err.txt')
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n"); Errors = (Get-Content -Raw (Join-Path $TestDrive 'build-err.txt')) }
    }

    # Runs EXE with ARGS, with its own temp folder and no key to wait for; returns its exit code, what it printed, what
    # the stand-in installer recorded and what is left in the temp folder.
    function Invoke-Exe([string] $Exe, [string[]] $Arguments = @()) {
        $run = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $temp = (New-Item -ItemType Directory (Join-Path $run 'temp')).FullName
        $record = Join-Path $run 'record.json'
        $saved = @{ TMP = $env:TMP; TEMP = $env:TEMP; PSModulePath = $env:PSModulePath }
        try {
            $env:TMP = $temp
            $env:TEMP = $temp
            $env:INSTALLER_EXE_RECORD = $record
            $env:PSModulePath = 'C:\from-the-parent-shell'
            # CI's runners are administrator accounts; the exe takes the installer's test switch there only.
            $env:METAPHOR_INSTALL_TEST_ALLOW_ADMIN = '1'
            $printed = '' | & $Exe @Arguments
            $code = $LASTEXITCODE
        } finally {
            $env:TMP = $saved.TMP
            $env:TEMP = $saved.TEMP
            $env:PSModulePath = $saved.PSModulePath
            Remove-Item Env:INSTALLER_EXE_RECORD, Env:METAPHOR_INSTALL_TEST_ALLOW_ADMIN -ErrorAction SilentlyContinue
        }
        [pscustomobject]@{
            ExitCode = $code; Printed = ($printed -join "`n")
            Record   = $(if (Test-Path -LiteralPath $record) { Get-Content -Raw $record | ConvertFrom-Json })
            Left     = @(Get-ChildItem -LiteralPath $temp -Force)
        }
    }

    $script:zip = Join-Path $TestDrive 'release.zip'
    New-ReleaseZip $script:zip
    $script:exe = Join-Path $TestDrive 'release-installer.exe'
    $script:built = Invoke-Build $script:zip $script:exe
}

Describe 'installer-exe.ps1' {
    It 'builds the exe and prints its SHA-256' {
        $script:built.ExitCode | Should -Be 0 -Because $script:built.Errors
        $script:built.Output | Should -Be (Get-Sha256 $script:exe)
    }

    It 'builds the same bytes from the same zip at another path (#613)' {
        $other = Join-Path $TestDrive 'another folder'
        New-Item -ItemType Directory $other | Out-Null
        Copy-Item -LiteralPath $script:zip -Destination (Join-Path $other 'release.zip')
        $again = Invoke-Build (Join-Path $other 'release.zip') (Join-Path $other 'again.exe')
        $again.ExitCode | Should -Be 0 -Because $again.Errors
        $again.Output | Should -Be $script:built.Output
    }

    It 'builds the list of the zip''s files and their SHA-256 values into the exe' {
        $archive = [IO.Compression.ZipFile]::OpenRead($script:zip)
        try {
            $entry = $archive.GetEntry('m/install.ps1')
            $stream = $entry.Open()
            try { $hash = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash.ToLowerInvariant() } finally { $stream.Dispose() }
        } finally { $archive.Dispose() }
        [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($script:exe)) | Should -Match "$hash  m/install\.ps1`n"
    }

    It 'refuses a destination that isn''t an exe' {
        $result = Invoke-Build $script:zip (Join-Path $TestDrive 'installer.zip')
        $result.ExitCode | Should -Be 1
        $result.Errors | Should -Match '\.exe'
    }

    It 'refuses a zip that isn''t there' {
        $result = Invoke-Build (Join-Path $TestDrive 'missing.zip') (Join-Path $TestDrive 'x.exe')
        $result.ExitCode | Should -Be 1
        $result.Errors | Should -Match 'no zip'
    }
}

Describe 'the installer exe' {
    It 'runs the installer it carries in Windows PowerShell 5.1, as install.cmd does, and returns its exit code' {
        $result = Invoke-Exe $script:exe -Arguments '-NoSpeech', 'two words'
        $result.ExitCode | Should -Be 3
        $result.Record.Version | Should -Be 5
        $result.Record.FromLauncher | Should -BeTrue
        $result.Record.Rest | Should -Be @('-NoSpeech', 'two words')
        $result.Record.ModulePath | Should -Not -Match 'from-the-parent-shell'
        $result.Printed | Should -Match 'Download checked'
    }

    It 'unpacks into a new folder in the temp folder and deletes it afterwards' {
        $result = Invoke-Exe $script:exe -Arguments '-NoSpeech'
        Split-Path -Leaf (Split-Path $result.Record.Folder) | Should -Match '^metaphor-installer-[0-9a-f]{32}$'
        $result.Left | Should -BeNullOrEmpty
    }

    It 'installs nothing from a payload with one byte changed, and says the download is damaged' {
        # The same build as installer-exe.ps1's, with a list made before the zip changed.
        $folder = (New-Item -ItemType Directory (Join-Path $TestDrive 'damaged')).FullName
        $manifest = Join-Path $folder 'payload.sha256'
        $archive = [IO.Compression.ZipFile]::OpenRead($script:zip)
        try {
            $lines = foreach ($entry in $archive.Entries) {
                $stream = $entry.Open()
                try { "$((Get-FileHash -InputStream $stream -Algorithm SHA256).Hash.ToLowerInvariant())  $($entry.FullName)" }
                finally { $stream.Dispose() }
            }
        } finally { $archive.Dispose() }
        [IO.File]::WriteAllText($manifest, (($lines | ForEach-Object { "$_`n" }) -join ''))
        $changed = Join-Path $folder 'changed.zip'
        New-ReleaseZip $changed @{ 'm/ModConfig.json' = '{ "ModId": "n" }' }
        & dotnet build $script:project -c Release -f net48 -v q --nologo -o (Join-Path $folder 'out') `
            "-p:BaseIntermediateOutputPath=$folder\obj\" "-p:InstallerPayload=$changed" "-p:InstallerManifest=$manifest" | Out-Null
        $LASTEXITCODE | Should -Be 0

        $result = Invoke-Exe (Join-Path $folder 'out/Metaphor.Installer.exe') -Arguments '-NoSpeech'
        $result.ExitCode | Should -Be 1
        $result.Record | Should -BeNullOrEmpty
        $result.Printed | Should -Match 'This download is damaged\. Download it again from the Releases page\.'
        $result.Left | Should -BeNullOrEmpty
    }
}
