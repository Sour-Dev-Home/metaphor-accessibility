<#
.SYNOPSIS
Asks the live inspector in a running Debug build of the mod read-only questions about the game's memory (#291).

.DESCRIPTION
Connects to \\.\pipe\metaphor-accessibility-inspector, which a Debug build of the mod opens only with
opt-in-inspector.txt in its folder, sends each -Request as one line, and prints each response line: "ok ..." or
"err ...". The commands (read, follow, rtti, watch, changes, ...) are in
docs/contributors/how-to/inspect-the-running-game.md, and "help" lists them.

The client checks that the pipe belongs to the current user (PipeOptions.CurrentUserOnly), so it never talks to another
user's server. Watches stay in the mod between runs, so "watch" in one run and "changes" in a later one report what
changed in between. Only reads the game (ADR 0002); the inspector has no command that writes.

Exit codes: 0 every request answered "ok"; 1 a request answered "err"; 2 no inspector to connect to (not running, not
opted in, a Release build, or another client connected), or it closed the connection.

.EXAMPLE
pwsh scripts/dev/inspect.ps1 'base' 'read base+0x10 16' 'follow base+0x40 0x8 0x10' 'rtti 0x1a2b3c4d0'
#>
[CmdletBinding()]
param(
    # The requests, one per argument, sent in order on one connection.
    [Parameter(Mandatory, Position = 0, ValueFromRemainingArguments)] [string[]] $Request,
    # The pipe's name; LiveInspector.PipeName in the mod.
    [string] $PipeName = 'metaphor-accessibility-inspector',
    # How long to wait for the pipe, in milliseconds.
    [ValidateRange(1, 60000)] [int] $TimeoutMilliseconds = 2000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Identification: the server may learn who connected but can't act as this user (#393).
# https://learn.microsoft.com/dotnet/api/system.security.principal.tokenimpersonationlevel
$pipe = [System.IO.Pipes.NamedPipeClientStream]::new(
    '.', $PipeName, [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::CurrentUserOnly,
    [System.Security.Principal.TokenImpersonationLevel]::Identification)
try {
    try {
        $pipe.Connect($TimeoutMilliseconds)
    }
    catch {
        Write-Output "err no live inspector on \\.\pipe\$PipeName ($($_.Exception.Message.Trim())); is a Debug build running with opt-in-inspector.txt?"
        exit 2
    }
    $ascii = [System.Text.Encoding]::ASCII
    $writer = [System.IO.StreamWriter]::new($pipe, $ascii, 1024, $true)
    $writer.NewLine = "`n"
    $writer.AutoFlush = $true
    $reader = [System.IO.StreamReader]::new($pipe, $ascii, $false, 1024, $true)
    $failed = $false
    foreach ($line in $Request) {
        $writer.WriteLine($line)
        $response = $reader.ReadLine()
        if ($null -eq $response) {
            Write-Output 'err the inspector closed the connection'
            exit 2
        }
        Write-Output $response
        if (-not $response.StartsWith('ok', [System.StringComparison]::Ordinal)) { $failed = $true }
    }
    exit ($failed ? 1 : 0)
}
finally {
    $pipe.Dispose()
}
