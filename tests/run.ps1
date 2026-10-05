#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Smoke', 'Units', 'Buildings', 'World', 'UI', 'All')]
    [string]$Suite = 'Smoke',
    [string[]]$Check,
    [string]$GodotPath = $env:GODOT_BIN,
    [switch]$SkipBuild,
    [ValidateRange(1, 60)]
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$logRoot = Join-Path $projectRoot '.godot/check-logs'
$suites = [ordered]@{
    Smoke = @('MapSyncChecks', 'UnitSceneChecks', 'CommandPanelChecks', 'StockChecks')
    Units = @('UnitSceneChecks', 'InterpolationChecks', 'MinionSyncChecks')
    Buildings = @('BuildingPlacementChecks', 'ConstructionSyncChecks', 'CommandPanelChecks', 'RallyChecks', 'TowerSyncChecks')
    World = @('MapSyncChecks', 'MapZonesChecks', 'FogSyncChecks', 'check_arena')
    UI = @('RtsControlChecks', 'SelectionDetailsChecks', 'CommandPanelChecks', 'MatchResultChecks')
}
$all = @($suites.Values | ForEach-Object { $_ } | Select-Object -Unique)
$selected = if ($Check) { @($Check | Select-Object -Unique) } elseif ($Suite -eq 'All') { $all } else { $suites[$Suite] }
foreach ($name in $selected) {
    if ($name -notin $all) { throw "Unknown check: $name. Available: $($all -join ', ')" }
}
if (!$GodotPath) {
    foreach ($name in @('Godot', 'godot', 'godot4')) {
        $command = Get-Command $name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($command) { $GodotPath = $command.Source; break }
    }
}
if (!$GodotPath -or !(Test-Path -LiteralPath $GodotPath -PathType Leaf)) {
    throw 'Set GODOT_BIN or pass -GodotPath with the Godot .NET executable path.'
}
$GodotPath = (Resolve-Path -LiteralPath $GodotPath).Path
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

function Invoke-Logged([string]$Name, [string]$File, [string[]]$Arguments, [int]$Timeout, [bool]$RequirePass) {
    $log = Join-Path $logRoot "$Name.log"
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $File
    $info.WorkingDirectory = $projectRoot
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = [Text.Encoding]::UTF8
    $info.StandardErrorEncoding = [Text.Encoding]::UTF8
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    $timer = [Diagnostics.Stopwatch]::StartNew()
    try {
        [void]$process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = !$process.WaitForExit($Timeout * 1000)
        if ($timedOut) { $process.Kill($true); $process.WaitForExit() }
        $output = $stdout.GetAwaiter().GetResult() + "`n" + $stderr.GetAwaiter().GetResult()
        if ($timedOut) { $output += "`nTIMEOUT after $Timeout seconds" }
        [IO.File]::WriteAllText($log, $output, [Text.UTF8Encoding]::new($false))
        $failed = $timedOut -or $process.ExitCode -ne 0 -or
            ($RequirePass -and ($output -notmatch '(?m)^PASS:' -or $output -match '(?m)^(SCRIPT ERROR|ERROR):'))
        if ($failed) {
            $tail = ($output -split '\r?\n' | Select-Object -Last 25) -join "`n"
            throw "FAIL: $Name (exit $($process.ExitCode)). Log: $log`n$tail"
        }
        Write-Host ('PASS: {0} ({1:F1}s)' -f $Name, $timer.Elapsed.TotalSeconds)
    }
    finally { $process.Dispose() }
}

if (!$SkipBuild) {
    $dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
    Invoke-Logged 'build' $dotnet @('build', '--no-restore', '--nologo', '--verbosity', 'quiet') 60 $false
}
foreach ($name in $selected) {
    $arguments = @('--headless', '--path', $projectRoot, '--resolution', '1152x648')
    if ($name -eq 'check_arena') { $arguments += @('--script', 'res://tests/check_arena.gd') }
    else { $arguments += "res://tests/$name.tscn" }
    Invoke-Logged $name $GodotPath $arguments $TimeoutSeconds $true
}
Write-Host "Passed $($selected.Count) checks. Logs: $logRoot"
