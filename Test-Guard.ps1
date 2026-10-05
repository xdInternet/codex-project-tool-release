$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'ProcessGuard.cs')
$root=Join-Path $env:USERPROFILE '.codex\.chatgpt-projects'
$runtime=Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\runtimes\cua_node\example-version\bin'
function Assert-Equal($actual,$expected,$name){if($actual -ne $expected){throw "Failed: $name"}; Write-Output "PASS: $name"}
Assert-Equal ([ProjectToolRelease.Guard]::InMirror(($root+'\g-p-test\'),$root)) $true 'project root accepted'
Assert-Equal ([ProjectToolRelease.Guard]::InMirror(($root+'\g-p-test\sources'),$root)) $true 'project child accepted'
Assert-Equal ([ProjectToolRelease.Guard]::InMirror(($root+'-other\g-p-test'),$root)) $false 'sibling prefix rejected'
Assert-Equal ([ProjectToolRelease.Guard]::InMirror(($root+'\g-p-test\..\..\other'),$root)) $false 'path escape rejected'
Assert-Equal ([ProjectToolRelease.Guard]::InMirror(($root+'\.metadata'),$root)) $false 'metadata rejected'
Assert-Equal ([ProjectToolRelease.Guard]::InMirror(($root+'\.g-p-test-staging-x'),$root)) $false 'staging rejected'
Assert-Equal ([ProjectToolRelease.Guard]::ToolImage(($runtime+'\node.exe'))) $true 'bundled node accepted'
Assert-Equal ([ProjectToolRelease.Guard]::ToolImage(($runtime+'\node_repl.exe'))) $true 'bundled repl accepted'
Assert-Equal ([ProjectToolRelease.Guard]::ToolImage('C:\Program Files\nodejs\node.exe')) $false 'unrelated node rejected'
Assert-Equal ([ProjectToolRelease.Guard]::ToolImage(($runtime+'\codex.exe'))) $false 'app-server rejected'
Assert-Equal ([ProjectToolRelease.Guard]::ToolImage(($runtime+'\extra\node.exe'))) $false 'unexpected layout rejected'
$self=New-Object ProjectToolRelease.Target
$self.Pid=$PID; $self.Created=1; $self.Image=$runtime+'\node.exe'
Assert-Equal ([ProjectToolRelease.Guard]::Stop($self,$root)) 'identity-changed-skipped' 'wrong process identity cannot terminate caller'

