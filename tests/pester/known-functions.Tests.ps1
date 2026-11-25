# Tests for scripts/dev/known-functions.ps1 (#716), on made-up findings, signatures and builds in a scratch repository:
# no real finding or game identifier.

Describe 'known-functions' {
    BeforeAll {
        $script:script = (Resolve-Path (Join-Path $PSScriptRoot '../../scripts/dev/known-functions.ps1')).Path
        $script:newBuild = 'b' * 64
        $script:oldBuild = 'a' * 64

        # New-Repo: a scratch repository with out/ git-ignored, two known builds and the given signatures.
        function New-Repo([object[]] $Signatures = @()) {
            $repo = Join-Path $TestDrive "repo é $([guid]::NewGuid().ToString('N').Substring(0, 6))"
            New-Item -ItemType Directory -Path (Join-Path $repo 'docs/findings'), (Join-Path $repo 'src/Metaphor.Core') | Out-Null
            & git -C $repo init -q
            Set-Content -LiteralPath (Join-Path $repo '.gitignore') -Value 'out/' -Encoding utf8
            Set-Content -LiteralPath (Join-Path $repo 'docs/findings/README.md') -Value "# Findings`n- **Functions:**`n  - x" -Encoding utf8
            ConvertTo-Json -InputObject @(@{ sha256 = $oldBuild }, @{ sha256 = $newBuild }) |
                Set-Content -LiteralPath (Join-Path $repo 'src/Metaphor.Core/known-game-builds.json') -Encoding utf8
            ConvertTo-Json -InputObject @($Signatures) -Depth 4 |
                Set-Content -LiteralPath (Join-Path $repo 'src/Metaphor.Core/hook-signatures.json') -Encoding utf8
            $repo
        }

        # Add-Finding REPO NAME BUILD LINES: a finding whose header holds LINES after its build.
        function Add-Finding([string] $Repo, [string] $Name, [string] $Build, [string[]] $Lines) {
            $text = @("# A made-up finding", '', "- **Game build:** ``$Build`` (Steam build 1)", '- **Method:** none') +
                $Lines + @('', 'The claim.', '', '## Evidence', '', '- **Functions:**', '  - Later | 0x1 | .text | Menus | read | Not in the header.')
            [IO.File]::WriteAllLines((Join-Path $Repo "docs/findings/$Name.md"), $text, [Text.UTF8Encoding]::new($false))
        }

        function Invoke-Generator([string] $Repo, [string] $OutFile = '') {
            $arguments = @('-NoProfile', '-File', $script, '-Root', $Repo)
            if ($OutFile) { $arguments += '-OutFile', $OutFile }
            $output = & pwsh @arguments 2>&1
            [pscustomobject]@{ Code = $LASTEXITCODE; Output = ($output -join "`n") }
        }

        function Read-Page([string] $Repo) {
            Get-Content -LiteralPath (Join-Path $Repo 'out/known-functions.md') -Raw -Encoding utf8
        }
    }

    It 'writes rows grouped by area, with build, finding link and signature' {
        $repo = New-Repo @(@{ name = 'MadeUpHook'; section = '.ecode'; gameBuilds = @($newBuild); evidence = 'made up' })
        Add-Finding $repo 'menus-thing' $newBuild @(
            '- **Functions:**',
            '  - MadeUpHook | 0x140001000 | .ecode | Menus | hooked | Opens a made-up panel. | finding',
            '  - madeUpGlobal | 0x140002000 | .data | Field | read | A made-up global. | finding')
        $run = Invoke-Generator $repo
        $run.Code | Should -Be 0 -Because $run.Output
        $run.Output | Should -Match '^ok: wrote 2 functions from 1 findings'
        $page = Read-Page $repo
        $page | Should -Match '(?s)## Menus.*## Field' -Because 'areas follow the declared order'
        $page | Should -Match '\| MadeUpHook \| 0x140001000 \| bbbbbbbb \| \.ecode \| hooked \| Opens a made-up panel\. \| finding \| \[menus-thing\.md\]\(\.\./docs/findings/menus-thing\.md\) \| yes \|'
        $page | Should -Match '\| madeUpGlobal \| .* \| no \|'
        $page | Should -Not -Match 'Later' -Because 'only the header block counts'
        $page | Should -Not -Match 'Signatures no finding lists'
    }

    It 'marks a row from an older build instead of dropping it' {
        $repo = New-Repo
        Add-Finding $repo 'old' $oldBuild @('- **Functions:**', '  - OldOne | 0x140001000 | .text | Battle | known only | Old. | finding')
        (Invoke-Generator $repo).Code | Should -Be 0
        Read-Page $repo | Should -Match '\| OldOne \| 0x140001000 \| aaaaaaaa \(old build: re-verify\) \|'
    }

    It 'lists signatures no finding names, and findings without a block' {
        $repo = New-Repo @(@{ name = 'Lonely'; section = '.text'; gameBuilds = @($oldBuild); evidence = 'a | b' })
        Add-Finding $repo 'no-block' $newBuild @()
        Add-Finding $repo 'none-block' $newBuild @('- **Functions:** none')
        $run = Invoke-Generator $repo
        $run.Code | Should -Be 0 -Because $run.Output
        $run.Output | Should -Match '1 findings have no block$'
        $page = Read-Page $repo
        $page | Should -Match '\| Lonely \| \.text \| aaaaaaaa \(old build: re-verify\) \| a \\\| b \|'
        $page | Should -Match '(?s)## Findings without a functions block.*no-block\.md'
        $page | Should -Not -Match 'none-block'
    }

    It 'fails on each malformed entry, naming its file and line, and writes nothing' {
        $repo = New-Repo
        Add-Finding $repo 'bad' $newBuild @(
            '- **Functions:**',
            '  - TooFew | 0x1 | .text',
            '  - Bad | 140001000 | text | Shops | patched |  | finding')
        $run = Invoke-Generator $repo
        $run.Code | Should -Be 1
        $run.Output | Should -Match 'docs/findings/bad\.md:6: needs seven fields'
        $run.Output | Should -Match "bad\.md:7: the address '140001000'"
        $run.Output | Should -Match "bad\.md:7: the section 'text'"
        $run.Output | Should -Match "bad\.md:7: the area 'Shops'"
        $run.Output | Should -Match "bad\.md:7: the use 'patched'"
        $run.Output | Should -Match 'bad\.md:7: the summary is empty'
        $run.Output | Should -Match '\nfailed: 6 problem[^\n]*$' -Because 'the last line is the verdict'
        Join-Path $repo 'out/known-functions.md' | Should -Not -Exist
    }

    It 'fails on a block in a finding with no game build' {
        $repo = New-Repo
        Add-Finding $repo 'nobuild' 'not a build' @('- **Functions:**', '  - X | 0x1 | .text | Menus | read | x. | finding')
        $run = Invoke-Generator $repo
        $run.Code | Should -Be 1
        $run.Output | Should -Match 'names no game build'
    }

    It 'refuses to write where git would track the page' {
        $repo = New-Repo
        Add-Finding $repo 'fine' $newBuild @('- **Functions:** none')
        $run = Invoke-Generator $repo (Join-Path $repo 'docs/known-functions.md')
        $run.Code | Should -Be 2
        $run.Output | Should -Match '^failed: .*is not git-ignored'
        Join-Path $repo 'docs/known-functions.md' | Should -Not -Exist
    }

    It 'refuses to write a page git already tracks, even under out/' {
        $repo = New-Repo
        Add-Finding $repo 'fine' $newBuild @('- **Functions:** none')
        New-Item -ItemType Directory -Path (Join-Path $repo 'out') | Out-Null
        Set-Content -LiteralPath (Join-Path $repo 'out/known-functions.md') -Value 'tracked' -Encoding utf8
        & git -C $repo add -f out/known-functions.md
        $run = Invoke-Generator $repo
        $run.Code | Should -Be 2
        $run.Output | Should -Match 'git already tracks it'
        (Get-Content -LiteralPath (Join-Path $repo 'out/known-functions.md') -Raw).Trim() | Should -Be 'tracked'
    }

    It 'replaces an earlier page whole' {
        $repo = New-Repo
        Add-Finding $repo 'fine' $newBuild @('- **Functions:**', '  - First | 0x1 | .text | Speech | read | One. | finding')
        (Invoke-Generator $repo).Code | Should -Be 0
        Add-Finding $repo 'fine' $newBuild @('- **Functions:**', '  - Second | 0x2 | .text | Speech | read | Two. | finding')
        (Invoke-Generator $repo).Code | Should -Be 0
        $page = Read-Page $repo
        $page | Should -Match '\| Second \|'
        $page | Should -Not -Match '\| First \|'
        Join-Path $repo 'out/known-functions.md.tmp' | Should -Not -Exist
    }

    Context 'sources (#997)' {
        BeforeAll {
            function Invoke-Check([string] $Repo) {
                $output = & pwsh -NoProfile -File $script -Root $Repo -Check 2>&1
                [pscustomobject]@{ Code = $LASTEXITCODE; Output = ($output -join "`n") }
            }
        }

        It 'fails an entry that cites no source, naming its line' {
            $repo = New-Repo
            Add-Finding $repo 'bare' $newBuild @('- **Functions:**', '  - Guessed | 0x1 | .ecode | Menus | known only | A guess.')
            $run = Invoke-Check $repo
            $run.Code | Should -Be 1
            $run.Output | Should -Match 'docs/findings/bare\.md:6: cites no source'
        }

        It 'fails an empty source, an unknown form and a cited file that does not exist' {
            $repo = New-Repo
            Add-Finding $repo 'bad-sources' $newBuild @(
                '- **Functions:**',
                '  - A | 0x1 | .text | Menus | read | a. | ',
                '  - B | 0x2 | .text | Menus | read | b. | memory',
                '  - C | 0x3 | .text | Menus | read | c. | docs/raw-sources/missing.md',
                '  - D | 0x4 | .text | Menus | read | d. | finding; ../outside.md')
            $run = Invoke-Check $repo
            $run.Code | Should -Be 1
            $run.Output | Should -Match 'bad-sources\.md:6: cites no source'
            $run.Output | Should -Match "bad-sources\.md:7: the source 'memory' isn't"
            $run.Output | Should -Match "bad-sources\.md:8: the source 'docs/raw-sources/missing\.md' isn't a file"
            $run.Output | Should -Match "bad-sources\.md:9: the source '\.\./outside\.md' isn't"
        }

        It 'passes finding, a committed file, and [NEEDS VERIFICATION], and writes no page' {
            $repo = New-Repo
            New-Item -ItemType Directory -Path (Join-Path $repo 'docs/raw-sources') | Out-Null
            Set-Content -LiteralPath (Join-Path $repo 'docs/raw-sources/a-source.md') -Value '# A source' -Encoding utf8
            Add-Finding $repo 'sourced' $newBuild @(
                '- **Functions:**',
                '  - A | 0x1 | .text | Menus | read | a. | finding',
                '  - B | 0x2 | .ecode | Menus | known only | b. | finding; docs/raw-sources/a-source.md',
                '  - unnamed, at 0x3 | 0x3 | .ecode | Menus | known only | c. | [NEEDS VERIFICATION] section')
            $run = Invoke-Check $repo
            $run.Code | Should -Be 0 -Because $run.Output
            $run.Output | Should -Match '^ok: 3 functions entries, each with a source, in 1 findings$'
            Join-Path $repo 'out/known-functions.md' | Should -Not -Exist
        }
    }
}
