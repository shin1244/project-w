#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$GodotPath = $env:GODOT_BIN,
    [ValidateSet(2,3,4)][int]$Players = 2,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$serverRoot = Join-Path (Split-Path $projectRoot -Parent) 'project-w-server'
$logRoot = Join-Path $projectRoot ".godot/check-logs/match-$Players"
if (!$GodotPath -or !(Test-Path -LiteralPath $GodotPath)) { throw 'Set GODOT_BIN or pass -GodotPath (Godot .NET).' }
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

function Start-Hidden([string]$File, [string[]]$Arguments, [string]$Directory, [string]$Log) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $File
    $info.WorkingDirectory = $Directory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    [void]$process.Start()
    return @{ Process=$process; Out=$process.StandardOutput.ReadToEndAsync(); Err=$process.StandardError.ReadToEndAsync(); Log=$Log }
}
function Save-Log($Job) {
    $output = $Job.Out.GetAwaiter().GetResult() + "`n" + $Job.Err.GetAwaiter().GetResult()
    [IO.File]::WriteAllText($Job.Log, $output, [Text.UTF8Encoding]::new($false))
    return $output
}
function Get-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    return $port
}

$jobs = @()
$oldGoCache = $env:GOCACHE
try {
    $env:GOCACHE = Join-Path $serverRoot '.godot/go-cache'
    if (!$SkipBuild) {
        Push-Location $projectRoot
        try { dotnet build --no-restore --nologo --verbosity quiet; if ($LASTEXITCODE) { throw 'C# build failed' } }
        finally { Pop-Location }
    }
    Push-Location $serverRoot
    try {
        go build -buildvcs=false -o "$logRoot/game.exe" .
        if ($LASTEXITCODE) { throw 'Game build failed' }
        go build -buildvcs=false -o "$logRoot/lobby.exe" ./cmd/lobby
        if ($LASTEXITCODE) { throw 'Lobby build failed' }
    } finally { Pop-Location }
    $lobbyPort = Get-FreePort
    $gamePort = Get-FreePort
    while ($gamePort -eq $lobbyPort) { $gamePort = Get-FreePort }
    $url = "http://127.0.0.1:$lobbyPort"
    $lobby = Start-Hidden "$logRoot/lobby.exe" @('-listen', "127.0.0.1:$lobbyPort", '-game-host', '127.0.0.1', '-game-port', "$gamePort", '-game-bin', "$logRoot/game.exe", '-wait', '4s') $serverRoot "$logRoot/lobby.log"
    $jobs += $lobby
    $healthy = $false
    for ($attempt=0; $attempt -lt 30; $attempt++) {
        try { $null = Invoke-RestMethod "$url/health" -TimeoutSec 1; $healthy=$true; break }
        catch { Start-Sleep -Milliseconds 100 }
    }
    if (!$healthy) { throw 'Lobby did not start' }
    $clients = @()
    for ($i=1; $i -le $Players; $i++) {
        $arguments = @('--headless', '--path', $projectRoot, '--log-file', "$logRoot/engine-$i.log", '--resolution', '1152x648', 'res://tests/MatchFlowChecks.tscn', '--', "--lobby-url=$url")
        if ($Players -eq 2) { $arguments += '--check-cancellation' }
        $client = Start-Hidden $GodotPath $arguments $projectRoot "$logRoot/client-$i.log"
        $clients += $client
        $jobs += $client
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(80)
    $results = @()
    foreach ($client in $clients) {
        $remaining = [Math]::Max(1, [int]($deadline-[DateTime]::UtcNow).TotalMilliseconds)
        if (!$client.Process.WaitForExit($remaining)) { throw "Client timeout: $($client.Log)" }
        $output = Save-Log $client
        if ($client.Process.ExitCode -ne 0 -or $output -match '(?m)^(ERROR|SCRIPT ERROR):' -or ([regex]::Matches($output,'PASS: cycle')).Count -ne 2) {
            throw "Match flow failed: $($client.Log)`n$(($output -split '\r?\n' | Select-Object -Last 16) -join "`n")"
        }
        $summaries = @($output -split '\r?\n' | Where-Object { $_ -like 'PASS: cycle*' })
        $results += $summaries
        $summaries | Write-Host
    }
    for ($cycle=1; $cycle -le 2; $cycle++) {
        $expected = @('team 1, COMMANDER', 'team 1, HERO', 'team 2, COMMANDER', 'team 2, HERO') | Select-Object -First $Players
        foreach ($seat in $expected) {
            if (@($results | Where-Object { $_ -like "PASS: cycle $cycle, $Players players, $seat,*" }).Count -ne 1) { throw "Missing/duplicate seat: cycle $cycle, $seat" }
        }
    }
    Write-Host "PASS: $Players real Godot clients, two fresh matches and return to lobby. Logs: $logRoot"
} finally {
    foreach ($job in $jobs) {
        if (!$job.Process.HasExited) { $job.Process.Kill($true); $job.Process.WaitForExit() }
        $null = Save-Log $job
        $job.Process.Dispose()
    }
    $env:GOCACHE = $oldGoCache
}
