param(
    [ValidateSet('All','Quick','Media','Launcher','Dock','Notifications','Settings','Startup','Resources','Glass')]
    [string]$Area='All',
    [string[]]$Probe,
    [string]$Executable,
    [string]$OutputDirectory,
    [ValidateRange(10,180)][int]$TimeoutSeconds=90,
    [switch]$List
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
# Only isolated fixtures belong here. Live diagnostic signals and hardware-changing probes do not.
$taskAreas=[ordered]@{
    Quick=@('self-test','quick9-probe','quick-size-probe')
    Media=@('self-test','media9-probe','nowbar9-probe','nowbar-probe')
    Launcher=@('self-test','launcher-keyboard-probe','launcher-motion-probe','start-size-probe','deferred-icon-probe')
    Dock=@('self-test','desktop-behavior-probe','window-switcher-probe','window-activation-probe','window-preview-probe')
    Notifications=@('self-test','notification-ui-probe','notification-stack-probe','popup-probe')
    Settings=@('self-test','panel-theme-probe','settings-visibility-probe','beta-feedback-probe','update950-probe')
    Startup=@('self-test','startup-probe')
    Resources=@('self-test','memory-lifecycle-probe')
    Glass=@('self-test','glass95-probe')
}
$taskKnown=@($taskAreas.Values | ForEach-Object { $_ } | Select-Object -Unique)
if($List){$taskAreas.GetEnumerator() | ForEach-Object { $_.Key+': '+($_.Value -join ', ') };return}
$taskProbes=if($Probe){@($Probe|Select-Object -Unique)}elseif($Area -eq 'All'){$taskKnown}else{@($taskAreas[$Area])}
foreach($taskProbe in $taskProbes){if($taskKnown -notcontains $taskProbe){throw ('Unknown isolated fixture: '+$taskProbe)}}
if(-not $Executable){$Executable=Join-Path $taskRoot 'dist\OneTaskBar.exe'}
$Executable=(Resolve-Path -LiteralPath $Executable).Path
$taskVersion=[Version][Diagnostics.FileVersionInfo]::GetVersionInfo($Executable).FileVersion
if($taskVersion -lt [Version]'9.5.0.0'){$taskProbes=@($taskProbes|Where-Object {$_ -ne 'glass95-probe'})}
if(-not $OutputDirectory){$OutputDirectory=Join-Path $taskRoot ('.qa-data\ui-'+[Guid]::NewGuid().ToString('N'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){throw ('Use a new report directory: '+$OutputDirectory)}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$taskPreviousData=$env:ONE_TASKBAR_DATA
$taskResults=[Collections.Generic.List[object]]::new()
$taskFailure=$null
try {
    foreach($taskProbe in $taskProbes){
        $taskData=Join-Path $OutputDirectory $taskProbe
        New-Item -ItemType Directory -Path $taskData | Out-Null
        $env:ONE_TASKBAR_DATA=$taskData
        $taskTimer=[Diagnostics.Stopwatch]::StartNew()
        $taskProcess=Start-Process -FilePath $Executable -ArgumentList ('--'+$taskProbe) -WindowStyle Hidden -PassThru
        $taskCompleted=$taskProcess.WaitForExit($TimeoutSeconds*1000)
        if(-not $taskCompleted){Stop-Process -Id $taskProcess.Id -ErrorAction SilentlyContinue}
        $taskReport=Join-Path $taskData ($taskProbe+'.json')
        $taskResult=$null
        if(Test-Path -LiteralPath $taskReport){try{$taskResult=Get-Content -LiteralPath $taskReport -Raw|ConvertFrom-Json}catch{}}
        $taskPassed=$taskCompleted -and $taskProcess.ExitCode -eq 0 -and $taskResult -and $taskResult.passed -eq $true
        $taskResults.Add([pscustomobject]@{probe=$taskProbe;passed=[bool]$taskPassed;seconds=[Math]::Round($taskTimer.Elapsed.TotalSeconds,2);report=$taskReport})
        if(-not $taskPassed){$taskFailure='Fixture failed: '+$taskProbe+'; details: '+$taskReport;break}
        Write-Output ($taskProbe+': passed')
    }
} catch {
    $taskFailure=$_.Exception.Message
    throw
} finally {
    $env:ONE_TASKBAR_DATA=$taskPreviousData
    $taskSummary=[ordered]@{passed= -not $taskFailure;version=$taskVersion.ToString();executable=$Executable;sha256=(Get-FileHash -LiteralPath $Executable).Hash;results=$taskResults.ToArray()}
    $taskSummary|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding utf8
}
if($taskFailure){throw $taskFailure}
Write-Output ('Report: '+(Join-Path $OutputDirectory 'summary.json'))
