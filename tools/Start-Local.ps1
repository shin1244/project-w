# Launch a local distribution and stop only the lobby process this script owns.
param([switch]$Spectate, [switch]$LearnedAI, [string]$PolicyDirectory,
    [Alias('OutputDirectory')][string]$RunDirectory)
$ErrorActionPreference = 'Stop'
$runRoot = if ($RunDirectory) { [IO.Path]::GetFullPath($RunDirectory) }
    elseif (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Project W.exe')) { $PSScriptRoot }
    else { [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../build-jungle')) }
$serverRoot = Join-Path $runRoot 'Server'
# Server binaries rebuilt while a match was running wait in Server/update; apply them before starting.
$update = Join-Path $serverRoot 'update'
foreach ($binary in @('game.exe', 'lobby.exe')) {
    $staged = Join-Path $update $binary
    if (Test-Path -LiteralPath $staged) { Move-Item -LiteralPath $staged -Destination (Join-Path $serverRoot $binary) -Force }
}
$sessionName = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmssfff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6)
$logRoot = Join-Path $runRoot "logs/$sessionName"
foreach ($required in @('Project W.exe', 'Server/lobby.exe', 'Server/game.exe', 'Server/maps/test.json')) {
    if (!(Test-Path -LiteralPath (Join-Path $runRoot $required))) { throw "Missing distribution file: $required" }
}
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
function ConvertTo-WindowsArguments([string[]]$Arguments) {
    $quoted = foreach ($argument in $Arguments) {
        $escaped = [regex]::Replace($argument, '(\\*)"', '${1}${1}\"')
        $escaped = [regex]::Replace($escaped, '(\\+)$', '${1}${1}')
        '"' + $escaped + '"'
    }
    return ($quoted -join ' ')
}
function Get-AvailablePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try { return $listener.LocalEndpoint.Port } finally { $listener.Stop() }
}
$lobbyPort = Get-AvailablePort
$gamePort = Get-AvailablePort
while ($gamePort -eq $lobbyPort) { $gamePort = Get-AvailablePort }
$url = "http://127.0.0.1:$lobbyPort"
$lobby = $null
$lobbyArgs = @('-listen', "127.0.0.1:$lobbyPort", '-game-host', '127.0.0.1', '-game-port', "$gamePort", '-game-bin', '.\game.exe')
if ($LearnedAI -or $PolicyDirectory) {
    $PolicyDirectory = if ($PolicyDirectory) { (Resolve-Path -LiteralPath $PolicyDirectory).Path }
        else { Join-Path $serverRoot 'policies' }
    foreach ($role in @('commander', 'hero')) {
        $policy = Join-Path $PolicyDirectory "$role.json"
        if (!(Test-Path -LiteralPath $policy -PathType Leaf)) {
            throw "Missing learned $role policy: $policy. Prepare both models with Watch-Latest-AI.cmd."
        }
        $flag = if ($role -eq 'hero') { '-rl-hero-policy' } else { '-rl-policy' }
        $lobbyArgs += @($flag, $policy)
    }
    Write-Host "Frozen learned hero and commander policies: $PolicyDirectory"
}
$session = @{ state='starting'; started_at_utc=[DateTime]::UtcNow.ToString('o'); lobby_url=$url;
    game_port=$gamePort; policies=$PolicyDirectory; spectate=[bool]$Spectate; logs=$logRoot }
function Save-Session { $session | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $logRoot 'session.json') -Encoding UTF8 }
try {
    $lobby = Start-Process -FilePath (Join-Path $serverRoot 'lobby.exe') -WorkingDirectory $serverRoot -WindowStyle Hidden -PassThru `
        -ArgumentList (ConvertTo-WindowsArguments $lobbyArgs) `
        -RedirectStandardOutput (Join-Path $logRoot 'lobby.log') -RedirectStandardError (Join-Path $logRoot 'lobby-error.log')
    $healthy = $false
    for ($attempt=0; $attempt -lt 40; $attempt++) {
        if ($lobby.HasExited) { throw 'Local lobby exited. See logs/lobby-error.log.' }
        try { $null=Invoke-RestMethod "$url/health" -TimeoutSec 1; $healthy=$true; break }
        catch { Start-Sleep -Milliseconds 150 }
    }
    if (!$healthy) { throw 'Local lobby did not become ready.' }
    Write-Host "Local game ready: $url"
    if ($Spectate) { Write-Host 'Spectator mode: both teams use this snapshot. Close the game to stop this local server.' }
    else { Write-Host 'Choose a hero and Solo practice. Close the game to stop this local server.' }
    $clientArgs = @('--log-file', (Join-Path $logRoot 'client.log'), '--', "--lobby-url=$url")
    if ($Spectate) { $clientArgs += '--spectate' }
    $client=Start-Process -FilePath (Join-Path $runRoot 'Project W.exe') -WorkingDirectory $runRoot -WindowStyle Normal -PassThru `
        -ArgumentList (ConvertTo-WindowsArguments $clientArgs)
    $session.state='watching'; $session.lobby_pid=$lobby.Id; $session.client_pid=$client.Id
    Save-Session
    $client.WaitForExit()
}
catch { $session.error=$_.Exception.Message; throw }
finally {
    if ($lobby -and !$lobby.HasExited) {
        # End the owned lobby and any match it spawned; never stop another server by name or port.
        & taskkill.exe /PID $lobby.Id /T /F | Out-Null
    }
    $session.state=if ($session.error) { 'failed' } else { 'closed' }
    $session.closed_at_utc=[DateTime]::UtcNow.ToString('o'); Save-Session
}
