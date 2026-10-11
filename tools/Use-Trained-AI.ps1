# Freeze both roles into an immutable spectator snapshot; never changes training files.
param(
    [string]$Run = 'league5',
    [string]$Profile = 'mixed',
    [string]$ServerRepo = $env:PROJECT_W_SERVER_ROOT,
    [string]$AIRepo = $env:PROJECT_W_AI_ROOT,
    [string]$RunsRoot = $env:PROJECT_W_RUNS_ROOT,
    [string]$OutputDirectory,
    [switch]$Latest,
    [switch]$PassThru
)
$ErrorActionPreference = 'Stop'
$runRoot = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) }
    elseif (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Project W.exe')) { $PSScriptRoot }
    else { [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../build-league5-watch')) }
$workspaceRoot = if (Test-Path -LiteralPath (Join-Path $PSScriptRoot '../project.godot')) {
    [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
} else { Split-Path $PSScriptRoot -Parent }
if (!$AIRepo) { $AIRepo = Join-Path $workspaceRoot 'project-w-ai' }
if (!(Test-Path -LiteralPath (Join-Path $AIRepo 'rl/model.py'))) { throw "AI source not found: $AIRepo (pass -AIRepo)." }
$AIRepo = (Resolve-Path -LiteralPath $AIRepo).Path
if (!$ServerRepo) { $ServerRepo = Join-Path (Split-Path $AIRepo -Parent) 'project-w-server' }
$ServerRepo = [IO.Path]::GetFullPath($ServerRepo)
if (!$RunsRoot) { $RunsRoot = Join-Path $AIRepo 'rl/runs' }
$RunsRoot = [IO.Path]::GetFullPath($RunsRoot)
$helper = Join-Path $PSScriptRoot 'export-watch-snapshot.py'
if (!(Test-Path -LiteralPath $helper)) { throw "Missing snapshot helper: $helper" }
$mode = if ($Latest) { 'latest' } else { 'completed' }
$arguments = @('-X', 'utf8', '-B', $helper, '--server-repo', $ServerRepo, '--ai-repo', $AIRepo, '--runs-root', $RunsRoot,
    '--run', $Run, '--profile', $Profile, '--mode', $mode, '--run-directory', $runRoot)
$json = & python @arguments
if ($LASTEXITCODE) { throw 'Snapshot export failed. Existing spectator policies and training checkpoints were preserved.' }
$snapshot = ($json -join "`n") | ConvertFrom-Json
foreach ($role in 'hero', 'commander') {
    $record = $snapshot.roles.$role
    Write-Host ("{0}: {1} games, {2} decisions" -f $role, $record.games, $record.global_step)
}
Write-Host "Snapshot ready: $($snapshot.policy_directory)"
if ($PassThru) { $snapshot }
else { Write-Host 'Start Watch-Latest-AI.cmd to freeze the latest models and spectate.' }
