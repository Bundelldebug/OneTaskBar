[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ExpectedProductVersion,
    [string]$Report,
    [string]$DistRoot,
    [string]$ReleaseRoot
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
if(-not $DistRoot){$DistRoot=Join-Path $taskRoot 'dist'}
if(-not $ReleaseRoot){$ReleaseRoot=Join-Path $taskRoot 'release'}
$taskExtract=Join-Path $taskRoot ('.qa-data\payload-'+[Guid]::NewGuid().ToString('N'))
$taskSetup=Join-Path $ReleaseRoot 'OneTaskBar-Setup.exe'
$taskZipPath=Join-Path $ReleaseRoot 'OneTaskBar-Portable-x64.zip'
$taskHashes=@($taskSetup,$taskZipPath)|ForEach-Object {Get-FileHash -LiteralPath $_ -Algorithm SHA256}
$taskExpected=@('OneTaskBar.exe','OneTaskBar.exe.config','OneTaskBar.Guard.exe','Read-Apps.ps1','Install.ps1','Uninstall.ps1','Uninstall.cmd','README.md','README-ko.md','VALIDATION.md','docs/NOTIFICATIONS.md','LICENSE','THIRD_PARTY_NOTICES.txt','assets/OneTaskBar.ico','assets/OneTaskBar.png','assets/OneTaskBar.svg','assets/StoreLogo.png','assets/Square150x150Logo.png','assets/Square44x44Logo.png')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskArchive=[IO.Compression.ZipFile]::OpenRead($taskZipPath)
try{
    if($taskArchive.Entries.Count -ne $taskExpected.Count){throw 'Unexpected portable payload count'}
    foreach($taskName in $taskExpected){if(@($taskArchive.Entries|Where-Object {$_.FullName -ceq $taskName}).Count -ne 1){throw ('Missing or duplicate entry: '+$taskName)}}
    $taskVerify=Start-Process -FilePath $taskSetup -ArgumentList @('--verify-payload',('"'+$taskExtract+'"')) -PassThru -WindowStyle Hidden
    if(-not $taskVerify.WaitForExit(45000)){Stop-Process -Id $taskVerify.Id;throw 'Setup verification timed out'}
    if($taskVerify.ExitCode -ne 0){throw 'Setup verification failed'}
    foreach($taskName in $taskExpected){
        $taskEntry=@($taskArchive.Entries|Where-Object {$_.FullName -ceq $taskName})[0]
        $taskStream=$taskEntry.Open();$taskSha=[Security.Cryptography.SHA256]::Create()
        try{$taskZipHash=[BitConverter]::ToString($taskSha.ComputeHash($taskStream)).Replace('-','')}finally{$taskStream.Dispose();$taskSha.Dispose()}
        if($taskZipHash -cne (Get-FileHash -LiteralPath (Join-Path $DistRoot $taskName)).Hash -or $taskZipHash -cne (Get-FileHash -LiteralPath (Join-Path $taskExtract $taskName)).Hash){throw ('Payload mismatch: '+$taskName)}
    }
}finally{$taskArchive.Dispose()}
if(@(Get-ChildItem -LiteralPath $taskExtract -Recurse -File).Count -ne $taskExpected.Count){throw 'Unexpected extracted files'}
$taskExecutables=@((Join-Path $DistRoot 'OneTaskBar.exe'),(Join-Path $DistRoot 'OneTaskBar.Guard.exe'),$taskSetup)|ForEach-Object {
    $taskVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($_)
    if($taskVersion.ProductVersion -cne $ExpectedProductVersion -or ([Version]$taskVersion.FileVersion).ToString(3) -cne $ExpectedProductVersion.Split('-')[0]){throw ('Version mismatch: '+[IO.Path]::GetFileName($_))}
    $taskSignature=(Get-AuthenticodeSignature -LiteralPath $_).Status.ToString()
    if($taskSignature -notin @('NotSigned','Valid')){throw ('Unexpected signature status: '+$taskSignature)}
    [pscustomobject]@{file=[IO.Path]::GetFileName($_);version=$taskVersion.FileVersion;productVersion=$taskVersion.ProductVersion;sha256=(Get-FileHash -LiteralPath $_).Hash;signature=$taskSignature}
}
$taskSums=Get-Content -LiteralPath (Join-Path $ReleaseRoot 'SHA256SUMS.txt')
foreach($taskHash in $taskHashes){if((Get-FileHash -LiteralPath $taskHash.Path).Hash -cne $taskHash.Hash){throw 'Candidate changed during verification'};$taskLine=$taskHash.Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($taskHash.Path);if(@($taskSums|Where-Object {$_ -ceq $taskLine}).Count -ne 1){throw 'Stale release checksums'}}
$taskResult=[ordered]@{passed=$true;productVersion=$ExpectedProductVersion;payloadEntries=$taskExpected.Count;portableAndSetupMatchDist=$true;normalSetupVerification=$true;executables=$taskExecutables;setupSha256=$taskHashes[0].Hash;zipSha256=$taskHashes[1].Hash;securitySettingsChanged=$false}
if($Report){[IO.File]::WriteAllText([IO.Path]::GetFullPath($Report),($taskResult|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))}
$taskResult|ConvertTo-Json -Depth 5
