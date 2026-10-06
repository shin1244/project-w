#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$GodotPath = $env:GODOT_BIN,
    [switch]$SkipBuild # Only when the same C# sources were already built in Debug.
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '../build'))
$logRoot = Join-Path $projectRoot '.godot/check-logs/export-windows'

if (!$GodotPath) {
    $metadata = Join-Path $projectRoot '.godot/editor/project_metadata.cfg'
    if (Test-Path -LiteralPath $metadata) {
        $match = [regex]::Match([IO.File]::ReadAllText($metadata), '(?m)^executable_path="([^"]+)"')
        if ($match.Success) { $GodotPath = $match.Groups[1].Value }
    }
}
if (!$GodotPath -or !(Test-Path -LiteralPath $GodotPath -PathType Leaf)) {
    throw 'Pass -GodotPath with the Godot .NET editor executable, or set GODOT_BIN.'
}
$GodotPath = (Resolve-Path -LiteralPath $GodotPath).Path
$dotnet = (Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
if (!$dotnet) { $dotnet = Join-Path $env:ProgramFiles 'dotnet/dotnet.exe' }
if (!(Test-Path -LiteralPath $dotnet -PathType Leaf)) { throw 'Install the .NET SDK before exporting.' }
New-Item -ItemType Directory -Path $outputRoot, $logRoot -Force | Out-Null

function Invoke-ExportStep([string]$Name, [string]$File, [string[]]$Arguments) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $File
    $info.WorkingDirectory = $projectRoot
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.Environment['PATH'] = "$(Split-Path $dotnet -Parent);$env:PATH"
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    try {
        [void]$process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(180000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "$Name timed out."
        }
        $output = $stdout.GetAwaiter().GetResult() + "`n" + $stderr.GetAwaiter().GetResult()
        $log = Join-Path $logRoot "$Name.log"
        [IO.File]::WriteAllText($log, $output, [Text.UTF8Encoding]::new($false))
        if ($process.ExitCode -ne 0 -or $output -match '(?m)^(SCRIPT ERROR|ERROR):') {
            throw "$Name failed. See $log"
        }
        Write-Host "PASS: $Name"
    }
    finally { $process.Dispose() }
}

# The editor serializes exported C# properties using the Debug assembly.
# Build it before starting a new editor process for the release export.
if (!$SkipBuild) {
    Invoke-ExportStep 'build-debug' $dotnet @('build', 'Project W.csproj', '-c', 'Debug', '--nologo', '--verbosity', 'quiet')
} elseif (!(Test-Path -LiteralPath (Join-Path $projectRoot '.godot/mono/temp/bin/Debug/Project W.dll'))) {
    throw 'No Debug assembly found. Run again without -SkipBuild.'
}

# Cached binary scenes can omit newly exported fields even when the DLL is current.
# Keep a recoverable copy and force the editor to convert every scene again.
$cache = [IO.Path]::GetFullPath((Join-Path $projectRoot '.godot/exported'))
$backup = [IO.Path]::GetFullPath((Join-Path $projectRoot ".godot/export-backups/$([DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff'))/exported"))
$projectPrefix = $projectRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if (!$cache.StartsWith($projectPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    !$backup.StartsWith($projectPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Export cache paths must stay inside the project.'
}
if (Test-Path -LiteralPath $cache) {
    New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
    Move-Item -LiteralPath $cache -Destination $backup
}
$output = Join-Path $outputRoot 'Project W.exe'
Invoke-ExportStep 'export-release' $GodotPath @('--headless', '--path', $projectRoot,
    '--export-release', 'Windows Desktop', $output, '--log-file', (Join-Path $logRoot 'godot.log'))
Write-Host "Windows build: $outputRoot"
Write-Host 'Distribute the entire build folder, including the PCK and data directory.'
