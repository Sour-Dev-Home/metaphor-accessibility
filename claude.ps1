# Starts Claude Code with this project's own config directory, so nothing from the owner's main Claude setup
# (global CLAUDE.md, agents, plugins, MCP servers, memory) loads here. Arguments pass through, for example:
#   .\claude.ps1
#   .\claude.ps1 plugin marketplace add mattpocock/skills
#   .\claude.ps1 plugin install mattpocock-skills@mattpocock
$previous = $env:CLAUDE_CONFIG_DIR
try {
    $env:CLAUDE_CONFIG_DIR = Join-Path $HOME ".claude-metaphor"
    Push-Location $PSScriptRoot
    claude @args
}
finally {
    Pop-Location
    $env:CLAUDE_CONFIG_DIR = $previous
}
