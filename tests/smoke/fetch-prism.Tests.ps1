# Tests for scripts/fetch-prism.ps1, with a fake pin and a zip built here: no network, no real Prism.

BeforeAll {
    $script:fetch = (Resolve-Path "$PSScriptRoot/../../scripts/fetch-prism.ps1").Path
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    function Get-Sha256([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

    # A release-shaped zip: the pinned DLL path, licences, and files that must not ship.
    function New-ReleaseZip([string] $Path, [string] $DllText = 'pretend dll') {
        $source = Join-Path (Split-Path $Path) "zip-source-$([guid]::NewGuid())"
        foreach ($file in @{
                'dynamic/release/bin/prism.dll' = $DllText
                'dynamic/debug/bin/prism.dll'   = 'debug build, must not ship'
                'LICENSES/prism/mpl-2.0.txt'    = 'MPL'
                'LICENSES/fmt/LICENSE'          = 'fmt'
                'include/prism.h'               = 'header'
            }.GetEnumerator()) {
            $target = Join-Path $source $file.Key
            New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
            Set-Content -LiteralPath $target -Value $file.Value -NoNewline
        }
        [IO.Compression.ZipFile]::CreateFromDirectory($source, $Path)
        Remove-Item -LiteralPath $source -Recurse -Force
    }

    function Invoke-Fetch([string] $Pin, [string] $Out, [string] $Cache) {
        $output = & pwsh -NoProfile -File $script:fetch -Pin $Pin -OutDir $Out -Cache $Cache 2>&1
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
    }
}

Describe 'fetch-prism.ps1' {
    BeforeEach {
        $root = (New-Item -ItemType Directory (Join-Path $TestDrive (New-Guid))).FullName
        $script:cache = Join-Path $root 'cache'
        $script:out = Join-Path $root 'out'
        New-Item -ItemType Directory $script:cache | Out-Null

        $zip = Join-Path $root 'release.zip'
        New-ReleaseZip $zip
        $script:zipSha = Get-Sha256 $zip
        $dllFile = Join-Path $root 'dll'
        Set-Content -LiteralPath $dllFile -Value 'pretend dll' -NoNewline
        $script:dllSha = Get-Sha256 $dllFile
        # Pre-seed the cache, as a previous run would have, so nothing is downloaded.
        Copy-Item $zip (Join-Path $script:cache "prism-$script:zipSha.zip")

        $script:pin = Join-Path $root 'prism.json'
        $script:zipUrl = ([Uri]$zip).AbsoluteUri
        function Write-Pin([string] $DllSha = $script:dllSha, [string] $ZipSha = $script:zipSha,
            [string] $Url = 'https://example.invalid/never-fetched.zip') {
            @{ version = 'test'; sourceUrl = 'https://example.invalid/source'; url = $Url; zipSha256 = $ZipSha
               dllPath = 'dynamic/release/bin/prism.dll'; dllSha256 = $DllSha } |
                ConvertTo-Json | Set-Content -LiteralPath $script:pin
        }
        Write-Pin
    }

    It 'extracts the pinned DLL and the licences, and nothing else' {
        $result = Invoke-Fetch $script:pin $script:out $script:cache

        $result.ExitCode | Should -Be 0 -Because $result.Output
        Get-Sha256 (Join-Path $script:out 'prism.dll') | Should -Be $script:dllSha
        Join-Path $script:out 'LICENSES/prism/mpl-2.0.txt' | Should -Exist
        Join-Path $script:out 'LICENSES/fmt/LICENSE' | Should -Exist
        @(Get-ChildItem -LiteralPath $script:out -Recurse -File).Count | Should -Be 4
    }

    It 'says where the source is, next to the licences' {
        Invoke-Fetch $script:pin $script:out $script:cache | Out-Null

        Get-Content -Raw (Join-Path $script:out 'LICENSES/SOURCE.md') | Should -Match ([regex]::Escape('https://example.invalid/source'))
    }

    It 'fetches again when the licences are missing, even if the DLL is right' {
        Invoke-Fetch $script:pin $script:out $script:cache | Out-Null
        Remove-Item -LiteralPath (Join-Path $script:out 'LICENSES') -Recurse -Force

        $result = Invoke-Fetch $script:pin $script:out $script:cache

        $result.ExitCode | Should -Be 0
        Join-Path $script:out 'LICENSES/prism/mpl-2.0.txt' | Should -Exist
    }

    It 'refuses a DLL that does not match the pin, and leaves no output' {
        Write-Pin -DllSha ('0' * 64)

        $result = Invoke-Fetch $script:pin $script:out $script:cache

        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match "prism.dll in the zip doesn't match the pin"
        $script:out | Should -Not -Exist
    }

    It 'refuses a download whose hash does not match the pin, and keeps nothing of it' {
        # No cached zip under this hash, so it downloads the (local) zip, whose real hash differs from the pin.
        Write-Pin -ZipSha ('1' * 64) -Url $script:zipUrl

        $result = Invoke-Fetch $script:pin $script:out $script:cache

        $result.ExitCode | Should -Be 1 -Because $result.Output
        $result.Output | Should -Match "the download's SHA-256 doesn't match the pin"
        $script:out | Should -Not -Exist
        Join-Path $script:cache ('prism-' + ('1' * 64) + '.zip.part') | Should -Not -Exist
    }

    It 'fails when the download itself fails, and extracts nothing' {
        Write-Pin -ZipSha ('1' * 64)

        $result = Invoke-Fetch $script:pin $script:out $script:cache

        $result.ExitCode | Should -Not -Be 0
        $script:out | Should -Not -Exist
    }

    It 'does nothing when the pinned DLL is already in place' {
        Invoke-Fetch $script:pin $script:out $script:cache | Out-Null
        Remove-Item -LiteralPath $script:cache -Recurse -Force

        $result = Invoke-Fetch $script:pin $script:out $script:cache

        $result.ExitCode | Should -Be 0
        $script:cache | Should -Not -Exist
    }
}
