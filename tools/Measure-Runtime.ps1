param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [ValidateRange(5,300)][int]$Seconds=30,
    [string]$DataDirectory,
    [string]$Report,
    [switch]$IsolatedFixture
)
$ErrorActionPreference='Stop'
$Executable=(Resolve-Path -LiteralPath $Executable).Path
if(-not $DataDirectory){$DataDirectory=Join-Path (Split-Path -Parent (Split-Path -Parent $Executable)) 'Data'}
if(-not $Report){$Report=Join-Path (Split-Path -Parent $PSScriptRoot) ('.qa-data\runtime-'+[Guid]::NewGuid().ToString('N')+'.json')}
$taskMainInfo=Get-CimInstance Win32_Process -Filter "Name='OneTaskBar.exe'" | Where-Object {$_.ExecutablePath -eq $Executable} | Select-Object -First 1
if(-not $taskMainInfo){throw 'The specified app is not running'}
$taskMain=Get-Process -Id $taskMainInfo.ProcessId
$taskProcesses=@($taskMain)
if($IsolatedFixture){
    if($taskMainInfo.CommandLine -notmatch '(?:^|\s)--topbar-runtime-probe(?:\s|$)'){
        throw 'Isolated measurements require the idle topbar/dock fixture'
    }
}else{
    $taskGuardInfo=Get-CimInstance Win32_Process -Filter "Name='OneTaskBar.Guard.exe'" | Where-Object {$_.ParentProcessId -eq $taskMainInfo.ProcessId} | Select-Object -First 1
    if(-not $taskGuardInfo){throw 'Recovery helper is not running'}
    $taskProcesses+=Get-Process -Id $taskGuardInfo.ProcessId
}
$taskStarts=@($taskProcesses|ForEach-Object {$_.StartTime})
$taskFilter=($taskProcesses|ForEach-Object {'IDProcess='+$_.Id}) -join ' OR '
function Read-RuntimeState {
    $taskStateFile=Join-Path $DataDirectory 'live-state.json'
    if(-not(Test-Path -LiteralPath $taskStateFile)){return $null}
    $taskState=Get-Content -LiteralPath $taskStateFile -Raw -Encoding UTF8 | ConvertFrom-Json
    [pscustomobject]@{fixture=$taskState.fixture;desktop=$taskState.desktop;mediaActive=$taskState.mediaActive;notifications=$taskState.notificationsAllowed;displays=$taskState.displays;windowCount=$taskState.windowCount;pulseMilliseconds=$taskState.pulseMilliseconds}
}
$null=Get-CimInstance Win32_PerfFormattedData_PerfProc_Process -Filter $taskFilter
$taskBefore=Read-RuntimeState
if($IsolatedFixture -and $taskBefore.fixture -ne 'idle-topbar-dock'){throw 'The isolated fixture is not ready'}
$taskCpu=($taskProcesses|ForEach-Object {$_.TotalProcessorTime.TotalSeconds}|Measure-Object -Sum).Sum
$taskSamples=[Collections.Generic.List[object]]::new()
$taskTimer=[Diagnostics.Stopwatch]::StartNew()
while($taskTimer.Elapsed.TotalSeconds -lt $Seconds){
    for($taskIndex=0;$taskIndex -lt $taskProcesses.Count;$taskIndex++){
        $taskProcess=$taskProcesses[$taskIndex]
        $taskProcess.Refresh()
        if($taskProcess.HasExited -or $taskProcess.StartTime -ne $taskStarts[$taskIndex]){
            throw 'A measured process changed; restart the sample'
        }
    }
    $taskCounters=@(Get-CimInstance Win32_PerfFormattedData_PerfProc_Process -Filter $taskFilter)
    if($taskCounters.Count -ne $taskProcesses.Count){throw 'Process counters are unavailable'}
    $taskPrivate=($taskCounters|Measure-Object WorkingSetPrivate -Sum).Sum
    $taskWorking=($taskProcesses|Measure-Object WorkingSet64 -Sum).Sum
    $taskBytes=($taskProcesses|Measure-Object PrivateMemorySize64 -Sum).Sum
    $taskSamples.Add([pscustomobject]@{seconds=[Math]::Round($taskTimer.Elapsed.TotalSeconds,2);totalWorkingSetMiB=[Math]::Round($taskWorking/1MB,2);privateWorkingSetMiB=[Math]::Round($taskPrivate/1MB,2);privateBytesMiB=[Math]::Round($taskBytes/1MB,2)})
    $taskRemaining=$Seconds-$taskTimer.Elapsed.TotalSeconds
    if($taskRemaining -gt 0){Start-Sleep -Milliseconds ([int](1000*[Math]::Min(5,$taskRemaining)))}
}
$taskElapsed=$taskTimer.Elapsed.TotalSeconds
$taskProcesses|ForEach-Object {$_.Refresh()}
$taskCpuAfter=($taskProcesses|ForEach-Object {$_.TotalProcessorTime.TotalSeconds}|Measure-Object -Sum).Sum
$taskResult=[ordered]@{version=$taskMain.MainModule.FileVersionInfo.ProductVersion;mode=$(if($IsolatedFixture){'isolated-topbar-dock'}else{'installed-app-and-guard'});mainSha256=(Get-FileHash -LiteralPath $Executable).Hash;seconds=[Math]::Round($taskElapsed,2);cpuPercent=[Math]::Round(($taskCpuAfter-$taskCpu)/$taskElapsed/[Environment]::ProcessorCount*100,4);before=$taskBefore;after=(Read-RuntimeState);samples=$taskSamples.ToArray();forcedGc=$false;workingSetTrim=$false}
New-Item -ItemType Directory -Path (Split-Path -Parent ([IO.Path]::GetFullPath($Report))) -Force | Out-Null
$taskResult|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $Report -Encoding utf8
[pscustomobject]@{version=$taskResult.version;cpuPercent=$taskResult.cpuPercent;averageWorkingSetMiB=[Math]::Round(($taskSamples|Measure-Object totalWorkingSetMiB -Average).Average,2);averagePrivateWorkingSetMiB=[Math]::Round(($taskSamples|Measure-Object privateWorkingSetMiB -Average).Average,2);report=[IO.Path]::GetFullPath($Report)}
