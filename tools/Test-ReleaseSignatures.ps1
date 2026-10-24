[CmdletBinding()]
param(
    [string]$SignerThumbprint,
    [switch]$PayloadOnly,
    [string]$Report
)
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$pin=($SignerThumbprint -replace '\s','').ToUpperInvariant()
if($pin -notmatch '\A[0-9A-F]{40}\z'){throw 'A publisher certificate thumbprint from the approved signing provider is required. No trust certificates or security settings will be changed.'}
if(-not $Report){$Report=Join-Path $project '.qa-data\release-signatures.json'}
$files=@('dist\OneTaskBar.exe','dist\OneTaskBar.Guard.exe','dist\Read-Apps.ps1','dist\Install.ps1','dist\Uninstall.ps1')
if(-not $PayloadOnly){$files+='release\OneTaskBar-Setup.exe'}
$version=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $project 'dist\OneTaskBar.exe')).FileVersion
$rows=@()
foreach($relative in $files){
    $path=Join-Path $project $relative
    if(-not(Test-Path -LiteralPath $path)){throw ('Missing release code: '+$relative)}
    $signature=Get-AuthenticodeSignature -LiteralPath $path
    $publisher=$signature.SignerCertificate
    $timestamp=$signature.TimeStamperCertificate
    $trusted=$signature.Status -eq [System.Management.Automation.SignatureStatus]::Valid -and $signature.SignatureType.ToString() -eq 'Authenticode'
    $matches=$publisher -and $publisher.Thumbprint -eq $pin -and $publisher.Subject -ne $publisher.Issuer
    $metadata=$true
    if([IO.Path]::GetExtension($path) -eq '.exe'){
        $info=[Diagnostics.FileVersionInfo]::GetVersionInfo($path)
        $metadata=$info.ProductName -eq 'One TaskBar' -and $info.FileVersion -eq $version
    }
    $rows+=[pscustomobject]@{File=$relative;Sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash;Status=$signature.Status.ToString();ExpectedPublisher=[bool]$matches;TimestampPresent=$null -ne $timestamp;MetadataMatches=[bool]$metadata;Passed=[bool]($trusted -and $matches -and $timestamp -and $metadata)}
}
$passed=@($rows|Where-Object {-not $_.Passed}).Count -eq 0
$record=[pscustomobject]@{Passed=$passed;Version=$version;ExpectedSigner=$pin;PayloadOnly=[bool]$PayloadOnly;SecuritySettingsChanged=$false;Files=$rows}
$parent=Split-Path -Parent ([IO.Path]::GetFullPath($Report));New-Item -ItemType Directory -Path $parent -Force|Out-Null
[IO.File]::WriteAllText($Report,($record|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
if(-not $passed){throw 'Release stopped: every EXE and shipped PowerShell script needs a valid, timestamped signature from the approved publisher. See the signature report.'}
Write-Output ('Release signatures verified: '+$files.Count)
