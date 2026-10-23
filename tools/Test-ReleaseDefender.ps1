[CmdletBinding()]
param([string]$Report)
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
if(-not $Report){$Report=Join-Path $project '.qa-data\release-defender.json'}
$status=Get-MpComputerStatus
if(-not $status.AntivirusEnabled -or -not $status.RealTimeProtectionEnabled){throw 'Defender protection is unavailable; a passing scan cannot be reported.'}
$platform=Join-Path $env:ProgramData 'Microsoft\Windows Defender\Platform'
$scanner=Get-ChildItem -LiteralPath $platform -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | ForEach-Object {Join-Path $_.FullName 'MpCmdRun.exe'} | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
if(-not $scanner){$scanner=Join-Path $env:ProgramFiles 'Windows Defender\MpCmdRun.exe'}
if(-not(Test-Path -LiteralPath $scanner)){throw 'Official Defender scanner not found.'}
$started=Get-Date
$results=@()
foreach($relative in @('dist\OneTaskBar.exe','dist\OneTaskBar.Guard.exe','release\OneTaskBar-Setup.exe','release\OneTaskBar-Portable-x64.zip')){
    $file=Join-Path $project $relative
    $before=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    $output=@(& $scanner -Scan -ScanType 3 -File $file 2>&1)
    $code=$LASTEXITCODE
    $intact=(Test-Path -LiteralPath $file) -and (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -eq $before
    $results+=[pscustomobject]@{File=$relative;Sha256=$before;ExitCode=$code;Unchanged=$intact;Output=($output -join [Environment]::NewLine)}
    Write-Output ($relative+': scan exit '+$code+', unchanged='+$intact)
}
# Exit 0 can also mean a threat was remediated. Require no new relevant detections
# and intact release hashes, instead of treating the exit code alone as proof.
$new=@(Get-MpThreatDetection | Where-Object {$_.InitialDetectionTime -ge $started -and ($_.Resources -join ' ') -match 'OneTaskBar|One TaskBar'})
$passed=(@($results | Where-Object {$_.ExitCode -ne 0 -or -not $_.Unchanged}).Count -eq 0) -and $new.Count -eq 0
$final=Get-MpComputerStatus
$passed=$passed -and $final.AntivirusEnabled -and $final.RealTimeProtectionEnabled
$record=[pscustomobject]@{Passed=$passed;Started=$started.ToString('o');Finished=(Get-Date).ToString('o');Signature=$final.AntivirusSignatureVersion;NewRelevantDetections=$new.Count;ProtectionEnabled=$final.AntivirusEnabled -and $final.RealTimeProtectionEnabled;ExclusionsChanged=$false;Results=$results}
$parent=Split-Path -Parent ([IO.Path]::GetFullPath($Report));New-Item -ItemType Directory -Path $parent -Force|Out-Null
[IO.File]::WriteAllText($Report,($record | ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
if(-not $passed){throw 'Release Defender validation did not pass. Inspect the report before publishing.'}
Write-Output ('Passed. Report: '+$Report)
