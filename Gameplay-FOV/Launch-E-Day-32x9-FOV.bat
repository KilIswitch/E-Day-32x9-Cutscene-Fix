@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "EOS_USE_ANTICHEATCLIENTNULL=1"
set "SteamAppId=3010850"
set "GEARS_EDAY_LAUNCHER_PATH=%~f0"
powershell.exe -NoLogo -NoProfile -Command "$source=[IO.File]::ReadAllText($env:GEARS_EDAY_LAUNCHER_PATH); $marker='# BEGIN GEARS EDAY POWERSHELL'; $offset=$source.LastIndexOf($marker); if($offset -lt 0){exit 1}; & ([scriptblock]::Create($source.Substring($offset+$marker.Length)))"
set "GEARS_EDAY_EXIT_CODE=%ERRORLEVEL%"
if not "%GEARS_EDAY_EXIT_CODE%"=="0" pause
exit /b %GEARS_EDAY_EXIT_CODE%

# BEGIN GEARS EDAY POWERSHELL
$ErrorActionPreference = 'Stop'
$directory = [IO.Path]::GetDirectoryName($env:GEARS_EDAY_LAUNCHER_PATH)
$executable = Join-Path $directory 'GoWEDay-Steam.exe'
$idPath = Join-Path $directory 'steam_appid.txt'
$expectedId = '3010850'
$createdId = $false
$idHandle = $null
$exitCode = 1
$game = $null
$helper = Join-Path $directory 'E-Day-32x9-FOV.exe'

try {
    if (-not (Get-Process -Name steam -ErrorAction SilentlyContinue)) {
        throw 'Steam must be running. Open Steam, then double-click this launcher again.'
    }
    if (-not [IO.File]::Exists($executable)) {
        throw 'Place this launcher beside the full-version GoWEDay-Steam.exe.'
    }

    if (-not [IO.File]::Exists($helper)) {
        throw 'E-Day-32x9-FOV.exe is missing. Keep it beside this launcher.'
    }
    if (Get-Process -Name GoWEDay-Steam -ErrorAction SilentlyContinue) {
        throw 'The game is already running. Use Apply-E-Day-32x9-FOV.bat for the current session.'
    }

    # CreateNew never overwrites an existing ID file.
    try {
        $idHandle = [IO.File]::Open($idPath, [IO.FileMode]::CreateNew,
            [IO.FileAccess]::ReadWrite, [IO.FileShare]::Read)
        $createdId = $true
        $bytes = [Text.Encoding]::ASCII.GetBytes($expectedId)
        $idHandle.Write($bytes, 0, $bytes.Length)
        $idHandle.Flush()
        $idHandle.Dispose()
        $idHandle = $null
    }

    catch [IO.IOException] {
        if ($createdId -or -not [IO.File]::Exists($idPath)) { throw }
        if ([IO.File]::ReadAllText($idPath).Trim() -cne $expectedId) {
            throw 'Existing steam_appid.txt does not contain 3010850. It was preserved; launch stopped.'
        }
    }

    # A read-only handle allows normal Steam reads while preventing changes
    # or replacement of the ID file during this launch.
    $idHandle = [IO.File]::Open($idPath, [IO.FileMode]::Open,
        [IO.FileAccess]::Read, [IO.FileShare]::Read)
    if ([IO.File]::ReadAllText($idPath).Trim() -cne $expectedId) {
        throw 'steam_appid.txt changed before launch. Launch stopped.'
    }

    Write-Host 'Starting Gears of War: E-Day. Keep this window open until the game exits.'
    $game = Start-Process -FilePath $executable -WorkingDirectory $directory -PassThru
    $null = $game.Handle
    & $helper --pid $game.Id --wait forever
    $helperExitCode = $LASTEXITCODE
    if ($helperExitCode -eq 2) {
        Write-Host '32:9 fix skipped at your request. The game can continue without the mod.'
    }
    elseif ($helperExitCode -ne 0) {
        [Console]::Error.WriteLine('The cinematic fix failed. The game can continue with its original cinematic limit.')
    }
    $game.WaitForExit()
    $exitCode = $game.ExitCode
    if ($helperExitCode -ne 0 -and $helperExitCode -ne 2 -and $exitCode -eq 0) { $exitCode = 1 }
}
catch {
    [Console]::Error.WriteLine('Launcher error: ' + $_.Exception.Message)
    $exitCode = 1
}
finally {
    # Keep the Steam ID available even if applying the fix failed.
    if ($null -ne $game) {
        try { $game.WaitForExit() }
        catch { [Console]::Error.WriteLine('Could not wait for the game: ' + $_.Exception.Message); $exitCode = 1 }
    }
    if ($null -ne $idHandle) { $idHandle.Dispose() }
    if ($createdId) {
        try {
            Remove-Item -LiteralPath $idPath -Force -ErrorAction Stop
        }
        catch {
            [Console]::Error.WriteLine('Could not remove the ID file created by this launch: ' + $_.Exception.Message)
            if ($exitCode -eq 0) { $exitCode = 1 }
        }
    }
}
exit $exitCode
