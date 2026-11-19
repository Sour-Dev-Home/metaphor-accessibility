# Helpers the smoke and deploy script tests share for the stand-in game (tests/Metaphor.StandInGame).

# Stops every stand-in launcher or game running from under FOLDER, then empties FOLDER, for a Describe's AfterAll on
# TestDrive (#348). Pester's own TestDrive cleanup fails on a file Windows still holds, and the tests left two kinds:
# - a stand-in a test's own stop missed: a game still starting has no Path in Get-Process yet (CIM's ExecutablePath
#   has it), and a launcher being stopped may just have started a game;
# - an exited stand-in's exe and DLLs, which stay locked for a second or two after it exits (2.5 s at most in five
#   tries on 2026-11-19, longer on a loaded machine).
# So it stops and waits for them until none is left, then deletes FOLDER's contents, retrying for up to a minute.
# Processes elsewhere, as another clone's tests, are left alone.
function Stop-StandIn([Parameter(Mandatory)] [string] $Folder) {
    $prefix = $Folder.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    for ($round = 0; $round -lt 5; $round++) {
        $left = @(Get-CimInstance Win32_Process -Filter "Name = 'METAPHOR.exe' OR Name = 'Metaphor.StandInGame.exe'" |
                Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) })
        if ($left.Count -eq 0) { break }
        foreach ($process in $left) {
            Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
            Wait-Process -Id $process.ProcessId -Timeout 10 -ErrorAction SilentlyContinue
        }
    }
    $deadline = (Get-Date).AddSeconds(60)
    while ($true) {
        try {
            Get-ChildItem -LiteralPath $Folder -Force | Remove-Item -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            if ((Get-Date) -gt $deadline) { throw }
            Start-Sleep -Milliseconds 200
        }
    }
}

Export-ModuleMember -Function Stop-StandIn
