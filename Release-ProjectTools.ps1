param([switch]$Apply, [switch]$NoPause)
$ErrorActionPreference='Stop'
try {
    if(-not [Environment]::Is64BitProcess){throw 'Please run 64-bit Windows PowerShell.'}
    Add-Type -Path (Join-Path $PSScriptRoot 'ProcessGuard.cs')
    $mirrorRoot=Join-Path $env:USERPROFILE '.codex\.chatgpt-projects'
    if(-not (Test-Path -LiteralPath $mirrorRoot -PathType Container)){throw "Project mirror directory not found: $mirrorRoot"}
    Write-Host 'Codex project tool release - all ChatGPT project mirrors'
    Write-Host 'Run only while project tasks are idle. Temporary tool memory will be reset.'
    Write-Host ('Mode: '+$(if($Apply){'RELEASE'}else{'CHECK ONLY'}))
    $targets=@([ProjectToolRelease.Guard]::Scan($mirrorRoot))
    Write-Host ('Matched tool processes: '+$targets.Count)
    # Stop younger descendants first, while their ancestor chain can still be verified.
    foreach($t in ($targets | Sort-Object Created -Descending)){
        $project=($t.Cwd.Substring($mirrorRoot.Length).TrimStart('\') -split '\\')[0]
        if($Apply){$result=[ProjectToolRelease.Guard]::Stop($t,$mirrorRoot)}else{$result='check-only'}
        Write-Host ('PID {0} | {1} | {2} | {3}' -f $t.Pid,[IO.Path]::GetFileName($t.Image),$project,$result)
    }
    if($Apply){Start-Sleep -Milliseconds 600}
    $locked=0
    foreach($dir in Get-ChildItem -LiteralPath $mirrorRoot -Directory | Where-Object {$_.Name.StartsWith('g-p-', [StringComparison]::OrdinalIgnoreCase)}){
        $code=[ProjectToolRelease.Guard]::Probe($dir.FullName)
        if($code -eq 0){Write-Host ($dir.Name+' : available')}
        else {$locked++;Write-Host ($dir.Name+' : BLOCKED / Win32 '+$code) -ForegroundColor Yellow}
    }
    $remaining=@([ProjectToolRelease.Guard]::Scan($mirrorRoot)).Count
    Write-Host ('Remaining matching processes: '+$remaining)
    if($locked -eq 0){Write-Host 'Directory access checks passed. Retry creation in the app.' -ForegroundColor Green}
    else{Write-Host 'Some directories remain blocked. No unrelated processes were terminated.' -ForegroundColor Yellow}
    Write-Host 'This is a one-shot release, not a background watchdog or a permanent product fix.'
    if(-not $NoPause){[void](Read-Host 'Press Enter to close')}
    if($locked -gt 0){exit 2}
} catch {
    Write-Host ('ERROR: '+$_.Exception.Message) -ForegroundColor Red
    if(-not $NoPause){[void](Read-Host 'Press Enter to close')}
    exit 1
}
