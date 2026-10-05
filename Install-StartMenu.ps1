param([switch]$NoPause)
$ErrorActionPreference='Stop'
try {
    $launcher=Join-Path $PSScriptRoot 'Release-All-Project-Tools.cmd'
    foreach($name in @('Release-All-Project-Tools.cmd','Release-ProjectTools.ps1','ProcessGuard.cs')){
        if(-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $name) -PathType Leaf)){throw "Required file missing: $name"}
    }
    $programs=[Environment]::GetFolderPath('Programs')
    if(-not (Test-Path -LiteralPath $programs -PathType Container)){throw 'The current user Start Menu directory is unavailable.'}
    $linkPath=Join-Path $programs 'Codex Project Tool Release.lnk'
    $shell=New-Object -ComObject WScript.Shell
    if(Test-Path -LiteralPath $linkPath){
        $old=$shell.CreateShortcut($linkPath)
        if($old.TargetPath -ne $launcher){throw "A shortcut with this name points elsewhere. Remove or rename it yourself first: $linkPath"}
    }
    $link=$shell.CreateShortcut($linkPath)
    $link.TargetPath=$launcher
    $link.WorkingDirectory=$PSScriptRoot
    $link.Description='Release Codex project helper processes. Use only while project tasks are idle.'
    $link.IconLocation=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $link.Save()
    $readback=$shell.CreateShortcut($linkPath)
    if($readback.TargetPath -ne $launcher -or $readback.WorkingDirectory -ne $PSScriptRoot){throw 'Shortcut verification failed.'}
    Write-Host ('Start Menu shortcut installed: '+$linkPath) -ForegroundColor Green
    Write-Host 'Search Windows Start for: Codex Project Tool Release'
    Write-Host 'Keep this folder in its current location. The tool was not executed.'
    if(-not $NoPause){[void](Read-Host 'Press Enter to close')}
} catch {
    Write-Host ('ERROR: '+$_.Exception.Message) -ForegroundColor Red
    if(-not $NoPause){[void](Read-Host 'Press Enter to close')}
    exit 1
}
