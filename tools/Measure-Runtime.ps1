param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [ValidateRange(5,300)][int]$Seconds=30,
    [string]$DataDirectory,
    [string]$Report
)
$ErrorActionPreference='Stop'
$Executable=(Resolve-Path -LiteralPath $Executable).Path
if(-not $DataDirectory){$DataDirectory=Join-Path (Split-Path -Parent (Split-Path -Parent $Executable)) 'Data'}
if(-not $Report){$Report=Join-Path (Split-Path -Parent $PSScriptRoot) ('.qa-data\runtime-'+[Guid]::NewGuid().ToString('N')+'.json')}
$taskMainInfo=Get-CimInstance Win32_Process -Filter "Name='OneTaskBar.exe'" | Where-Object {$_.ExecutablePath -eq $Executable} | Select-Object -First 1
if(-not $taskMainInfo){throw 'The specified installed app is not running'}
$taskGuardInfo=Get-CimInstance Win32_Process -Filter "Name='OneTaskBar.Guard.exe'" | Where-Object {$_.ParentProcessId -eq $taskMainInfo.ProcessId} | Select-Object -First 1
if(-not $taskGuardInfo){throw 'Recovery helper is not running'}
$taskMain=Get-Process -Id $taskMainInfo.ProcessId
$taskGuard=Get-Process -Id $taskGuardInfo.ProcessId
$taskMainStart=$taskMain.StartTime
$taskGuardStart=$taskGuard.StartTime
$taskFilter="IDProcess=$($taskMain.Id) OR IDProcess=$($taskGuard.Id)"
function Read-RuntimeState {
    $taskStateFile=Join-Path $DataDirectory 'live-state.json'
    if(-not(Test-Path -LiteralPath $taskStateFile)){return $null}
    $taskState=Get-Content -LiteralPath $taskStateFile -Raw | ConvertFrom-Json
    [pscustomobject]@{desktop=$taskState.desktop;mediaActive=$taskState.mediaActive;notifications=$taskState.notificationsAllowed;displays=$taskState.displays;windowCount=$taskState.windowCount;pulseMilliseconds=$taskState.pulseMilliseconds}
}
$null=Get-CimInstance Win32_PerfFormattedData_PerfProc_Process -Filter $taskFilter
$taskBefore=Read-RuntimeState
$taskMainCpu=$taskMain.TotalProcessorTime.TotalSeconds
$taskGuardCpu=$taskGuard.TotalProcessorTime.TotalSeconds
$taskSamples=[Collections.Generic.List[object]]::new()
$taskTimer=[Diagnostics.Stopwatch]::StartNew()
while($taskTimer.Elapsed.TotalSeconds -lt $Seconds){
    $taskMain.Refresh();$taskGuard.Refresh()
    if($taskMain.HasExited -or $taskGuard.HasExited -or $taskMain.StartTime -ne $taskMainStart -or $taskGuard.StartTime -ne $taskGuardStart){throw 'A measured process changed; restart the sample'}
    $taskCounters=@(Get-CimInstance Win32_PerfFormattedData_PerfProc_Process -Filter $taskFilter)
    if($taskCounters.Count -ne 2){throw 'Process counters are unavailable'}
    $taskPrivate=($taskCounters|Measure-Object WorkingSetPrivate -Sum).Sum
    $taskSamples.Add([pscustomobject]@{seconds=[Math]::Round($taskTimer.Elapsed.TotalSeconds,2);totalWorkingSetMiB=[Math]::Round(($taskMain.WorkingSet64+$taskGuard.WorkingSet64)/1MB,2);privateWorkingSetMiB=[Math]::Round($taskPrivate/1MB,2);privateBytesMiB=[Math]::Round(($taskMain.PrivateMemorySize64+$taskGuard.PrivateMemorySize64)/1MB,2)})
    $taskRemaining=$Seconds-$taskTimer.Elapsed.TotalSeconds
    if($taskRemaining -gt 0){Start-Sleep -Milliseconds ([int](1000*[Math]::Min(5,$taskRemaining)))}
}
$taskElapsed=$taskTimer.Elapsed.TotalSeconds
$taskMain.Refresh();$taskGuard.Refresh()
$taskResult=[ordered]@{version=$taskMain.MainModule.FileVersionInfo.ProductVersion;mainSha256=(Get-FileHash -LiteralPath $Executable).Hash;seconds=[Math]::Round($taskElapsed,2);cpuPercent=[Math]::Round((($taskMain.TotalProcessorTime.TotalSeconds-$taskMainCpu)+($taskGuard.TotalProcessorTime.TotalSeconds-$taskGuardCpu))/$taskElapsed/[Environment]::ProcessorCount*100,4);before=$taskBefore;after=(Read-RuntimeState);samples=$taskSamples.ToArray();forcedGc=$false;workingSetTrim=$false}
New-Item -ItemType Directory -Path (Split-Path -Parent ([IO.Path]::GetFullPath($Report))) -Force | Out-Null
$taskResult|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $Report -Encoding utf8
[pscustomobject]@{version=$taskResult.version;cpuPercent=$taskResult.cpuPercent;averageWorkingSetMiB=[Math]::Round(($taskSamples|Measure-Object totalWorkingSetMiB -Average).Average,2);averagePrivateWorkingSetMiB=[Math]::Round(($taskSamples|Measure-Object privateWorkingSetMiB -Average).Average,2);report=[IO.Path]::GetFullPath($Report)}
