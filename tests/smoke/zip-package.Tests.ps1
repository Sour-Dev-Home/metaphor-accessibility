# Tests for scripts/zip-package.ps1 (#308) with a stand-in package folder: no build, no network.

BeforeAll {
    $script:zipScript = (Resolve-Path "$PSScriptRoot/../../scripts/zip-package.ps1").Path
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    function Get-Sha256([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

    # A package-shaped folder. -Reverse writes the files in the opposite order, to show creation order doesn't matter.
    function New-Package([string] $Dir, [string] $ModId = 'metaphor.accessibility', [switch] $Reverse) {
        $files = [ordered]@{
            'ModConfig.json'               = "{ `"ModId`": `"$ModId`" }"
            'Metaphor.Hooks.dll'           = 'pretend mod'
            'prism.dll'                    = 'pretend prism'
            'LICENSES/prism/mpl-2.0.txt'   = 'MPL'
            'LICENSES/SOURCE.md'           = 'source'
            'Zeta.dll'                     = 'sorts last'
            'a-lower.txt'                  = 'sorts after upper case in ordinal order'
        }
        $keys = @($files.Keys)
        if ($Reverse) { [array]::Reverse($keys) }
        foreach ($key in $keys) {
            $target = Join-Path $Dir $key
            New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
            Set-Content -LiteralPath $target -Value $files[$key] -NoNewline
        }
    }

    function Invoke-Zip([string] $Path, [string] $Destination, [long] $Timestamp = 1763251200) {
        $output = & pwsh -NoProfile -File $script:zipScript -Path $Path -Destination $Destination -Timestamp $Timestamp 2>&1
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
    }

    function Get-Entries([string] $Zip) {
        $archive = [IO.Compression.ZipFile]::OpenRead($Zip)
        try {
            foreach ($entry in $archive.Entries) {
                $reader = [IO.StreamReader]::new($entry.Open())
                try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
                [pscustomobject]@{
                    Name = $entry.FullName; Text = $text; Length = $entry.Length
                    CompressedLength = $entry.CompressedLength; LastWriteTime = $entry.LastWriteTime
                }
            }
        } finally { $archive.Dispose() }
    }
}

Describe 'zip-package.ps1' {
    BeforeEach {
        $script:root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $script:package = Join-Path $script:root 'mod'
        New-Package $script:package
    }

    It 'zips every file under a folder named for the mod ID, in ordinal path order, and prints the zip''s SHA-256' {
        $zip = Join-Path $script:root 'out.zip'
        $result = Invoke-Zip $script:package $zip
        $result.ExitCode | Should -Be 0
        $result.Output | Should -Be (Get-Sha256 $zip)

        $entries = @(Get-Entries $zip)
        $entries.Name | Should -Be @(
            'metaphor.accessibility/LICENSES/SOURCE.md'
            'metaphor.accessibility/LICENSES/prism/mpl-2.0.txt'
            'metaphor.accessibility/Metaphor.Hooks.dll'
            'metaphor.accessibility/ModConfig.json'
            'metaphor.accessibility/Zeta.dll'
            'metaphor.accessibility/a-lower.txt'
            'metaphor.accessibility/prism.dll'
        )
        ($entries | Where-Object Name -EQ 'metaphor.accessibility/prism.dll').Text | Should -Be 'pretend prism'
    }

    It 'stores files uncompressed, each stamped with the given time' {
        $zip = Join-Path $script:root 'out.zip'
        (Invoke-Zip $script:package $zip -Timestamp 1763251200).ExitCode | Should -Be 0
        foreach ($entry in Get-Entries $zip) {
            $entry.CompressedLength | Should -Be $entry.Length
            $entry.LastWriteTime.DateTime | Should -Be ([datetime]'2025-11-16T00:00:00')
        }
    }

    It 'gives the same bytes for the same files whatever their order and timestamps on disk' {
        $other = Join-Path $script:root 'other'
        New-Package $other -Reverse
        Get-ChildItem -LiteralPath $other -Recurse -File | ForEach-Object { $_.LastWriteTime = [datetime]'2001-02-03' }

        $first = Join-Path $script:root 'first.zip'
        $second = Join-Path $script:root 'second.zip'
        (Invoke-Zip $script:package $first).ExitCode | Should -Be 0
        (Invoke-Zip $other $second).ExitCode | Should -Be 0
        Get-Sha256 $second | Should -Be (Get-Sha256 $first)
    }

    It 'gives different bytes when a file changes' {
        $first = Join-Path $script:root 'first.zip'
        $second = Join-Path $script:root 'second.zip'
        (Invoke-Zip $script:package $first).ExitCode | Should -Be 0
        Set-Content -LiteralPath (Join-Path $script:package 'prism.dll') -Value 'tampered' -NoNewline
        (Invoke-Zip $script:package $second).ExitCode | Should -Be 0
        Get-Sha256 $second | Should -Not -Be (Get-Sha256 $first)
    }

    It 'refuses a folder without ModConfig.json' {
        Remove-Item -LiteralPath (Join-Path $script:package 'ModConfig.json')
        $zip = Join-Path $script:root 'out.zip'
        $result = Invoke-Zip $script:package $zip
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'ModConfig\.json'
        Test-Path -LiteralPath $zip | Should -BeFalse
    }

    It 'refuses a mod ID that isn''t a plain folder name: <ModId>' -ForEach @(
        @{ ModId = '../escape' }, @{ ModId = 'a/b' }, @{ ModId = '..' }, @{ ModId = '' }
    ) {
        $bad = Join-Path $script:root 'bad'
        New-Package $bad -ModId $ModId
        $zip = Join-Path $script:root 'out.zip'
        $result = Invoke-Zip $bad $zip
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match 'ModId'
        Test-Path -LiteralPath $zip | Should -BeFalse
    }

    It 'refuses to write the zip inside the folder it zips' {
        $result = Invoke-Zip $script:package (Join-Path $script:package 'self.zip')
        $result.ExitCode | Should -Be 1
        Test-Path -LiteralPath (Join-Path $script:package 'self.zip') | Should -BeFalse
    }

    It 'refuses a time a zip can''t hold' {
        $zip = Join-Path $script:root 'out.zip'
        $result = Invoke-Zip $script:package $zip -Timestamp 0
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match '1980'
        Test-Path -LiteralPath $zip | Should -BeFalse
    }
}
