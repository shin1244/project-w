param([string]$Run = 'league5', [string]$Profile = 'mixed',
    [string]$ServerRepo = $env:PROJECT_W_SERVER_ROOT,
    [string]$AIRepo = $env:PROJECT_W_AI_ROOT,
    [string]$RunsRoot = $env:PROJECT_W_RUNS_ROOT, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$snapshot = & (Join-Path $PSScriptRoot 'Use-Trained-AI.ps1') -Run $Run -Profile $Profile `
    -ServerRepo $ServerRepo -AIRepo $AIRepo -RunsRoot $RunsRoot -OutputDirectory $OutputDirectory -Latest -PassThru
& (Join-Path $PSScriptRoot 'Start-Local.ps1') -Spectate -LearnedAI `
    -PolicyDirectory $snapshot.policy_directory -RunDirectory $snapshot.run_directory
