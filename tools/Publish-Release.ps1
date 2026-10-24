[CmdletBinding()]
param(
    [string]$Tag,
    [string]$SignerThumbprint,
    [string]$NotesFile,
    [string]$GhPath='gh.exe',
    [string]$GitPath='git.exe',
    [switch]$ValidateOnly
)
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$main=Join-Path $project 'dist\OneTaskBar.exe'
$version=[Version]([Diagnostics.FileVersionInfo]::GetVersionInfo($main).FileVersion)
$expected='v'+$version.ToString(3)
if($Tag -ne $expected){throw ('Release tag must match the tested application version: '+$expected)}
# Verify before executing any candidate or contacting GitHub. No unsigned fallback.
& (Join-Path $PSScriptRoot 'Test-ReleaseSignatures.ps1') -SignerThumbprint $SignerThumbprint
$originPath=Join-Path $project 'release\candidate-origin.json'
if(-not(Test-Path -LiteralPath $originPath)){throw 'The signed CI candidate origin record is required.'}
$origin=Get-Content -LiteralPath $originPath -Raw|ConvertFrom-Json
if($origin.commit -notmatch '\A[0-9a-f]{40}\z' -or $origin.version -ne $version.ToString() -or $origin.run -notmatch '\Ahttps://github.com/Bundelldebug/OneTaskBar/actions/runs/[0-9]+\z'){throw 'Candidate origin does not match this project/version.'}
$head=@(& $GitPath -C $project rev-parse HEAD)
if($LASTEXITCODE -ne 0 -or $head.Count -ne 1 -or $head[0] -ne $origin.commit){throw 'Check out the exact source commit from the signed CI candidate before validation.'}
$dirty=@(& $GitPath -C $project status --porcelain --untracked-files=normal)
if($LASTEXITCODE -ne 0 -or $dirty.Count -ne 0){throw 'Release validation requires a clean source checkout.'}
if(-not $ValidateOnly){
    $tagCommit=@(& $GitPath -C $project rev-parse --verify ($Tag+'^{commit}'))
    if($LASTEXITCODE -ne 0 -or $tagCommit.Count -ne 1 -or $tagCommit[0] -ne $origin.commit){throw 'The release tag must point at the signed candidate source commit.'}
}
$candidateHashes=@('dist\OneTaskBar.exe','dist\OneTaskBar.Guard.exe','dist\Read-Apps.ps1','dist\Install.ps1','dist\Uninstall.ps1','release\OneTaskBar-Setup.exe','release\OneTaskBar-Portable-x64.zip'|ForEach-Object {Get-FileHash -LiteralPath (Join-Path $project $_) -Algorithm SHA256})
& (Join-Path $PSScriptRoot 'Test-ReleaseDefender.ps1')
$data=Join-Path ([IO.Path]::GetTempPath()) ('OneTaskBar-release-check-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $data -Force|Out-Null
$previous=[Environment]::GetEnvironmentVariable('ONE_TASKBAR_DATA')
try{
    $env:ONE_TASKBAR_DATA=$data
    $test=Start-Process -FilePath $main -ArgumentList '--self-test' -PassThru -Wait -WindowStyle Hidden
    if($test.ExitCode -ne 0){throw 'Signed main self-test failed; release stopped.'}
}finally{[Environment]::SetEnvironmentVariable('ONE_TASKBAR_DATA',$previous)}
$setup=Join-Path $project 'release\OneTaskBar-Setup.exe'
$extracted=Join-Path $data 'payload'
$verify=Start-Process -FilePath $setup -ArgumentList @('--verify-payload',('"'+$extracted+'"')) -PassThru -Wait -WindowStyle Hidden
if($verify.ExitCode -ne 0){throw 'Signed Setup verification failed; release stopped.'}
$expectedFiles=@('OneTaskBar.exe','OneTaskBar.exe.config','OneTaskBar.Guard.exe','Read-Apps.ps1','Install.ps1','Uninstall.ps1','Uninstall.cmd','README.md','README-ko.md','VALIDATION.md','docs/NOTIFICATIONS.md','LICENSE','THIRD_PARTY_NOTICES.txt','assets/OneTaskBar.ico','assets/OneTaskBar.png','assets/OneTaskBar.svg','assets/StoreLogo.png','assets/Square150x150Logo.png','assets/Square44x44Logo.png')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$portable=Join-Path $project 'release\OneTaskBar-Portable-x64.zip'
$archive=[IO.Compression.ZipFile]::OpenRead($portable)
try{
    if($archive.Entries.Count -ne $expectedFiles.Count){throw 'Unexpected portable payload count.'}
    foreach($name in $expectedFiles){
        $zipMatches=@($archive.Entries|Where-Object {$_.FullName -ceq $name})
        if($zipMatches.Count -ne 1){throw ('Missing or duplicate payload entry: '+$name)}
        $stream=$zipMatches[0].Open();$sha=[Security.Cryptography.SHA256]::Create()
        try{$zipHash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
        $distHash=(Get-FileHash -LiteralPath (Join-Path (Join-Path $project 'dist') $name)).Hash
        $setupHash=(Get-FileHash -LiteralPath (Join-Path $extracted $name)).Hash
        if($zipHash -ne $distHash -or $setupHash -ne $distHash){throw ('Payload mismatch: '+$name)}
    }
}finally{$archive.Dispose()}
$sums=Get-Content -LiteralPath (Join-Path $project 'release\SHA256SUMS.txt')
foreach($file in @($setup,$portable)){
    $line=((Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant())+'  '+[IO.Path]::GetFileName($file)
    if(@($sums|Where-Object {$_ -ceq $line}).Count -ne 1){throw 'Release checksums are stale.'}
}
# Recheck signatures after execution and packaging; signing is the final code mutation.
& (Join-Path $PSScriptRoot 'Test-ReleaseSignatures.ps1') -SignerThumbprint $SignerThumbprint
foreach($candidate in $candidateHashes){if((Get-FileHash -LiteralPath $candidate.Path -Algorithm SHA256).Hash -ne $candidate.Hash){throw 'A candidate changed during validation; release stopped.'}}
if($ValidateOnly){Write-Output 'Signed release validation passed. Nothing published.';return}
if(-not $NotesFile -or -not(Test-Path -LiteralPath $NotesFile)){throw 'A reviewed release notes file is required.'}
Push-Location $project
try{
    & $GhPath release create $Tag $setup $portable (Join-Path $project 'release\SHA256SUMS.txt') --repo Bundelldebug/OneTaskBar --verify-tag --latest --title ($version.ToString(3)) --notes-file $NotesFile
    if($LASTEXITCODE -ne 0){throw 'GitHub release creation failed.'}
}finally{Pop-Location}
