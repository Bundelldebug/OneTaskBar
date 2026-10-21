[CmdletBinding()]
param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $SkipBuild){& (Join-Path $root 'Build.ps1')}
$dist=Join-Path $root 'dist'
$build=Join-Path $root '.build'
$release=Join-Path $root 'release'
New-Item -ItemType Directory -Path $build,$release -Force|Out-Null
$zip=Join-Path $build 'OneTaskBar.Payload.zip'
if(Test-Path -LiteralPath $zip){Remove-Item -LiteralPath $zip -Force}
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{
    $files=@('OneTaskBar.exe','OneTaskBar.exe.config','OneTaskBar.Guard.exe','Read-Apps.ps1','Install.ps1','Uninstall.ps1','Uninstall.cmd','README.md','README-ko.md','VALIDATION.md','docs/NOTIFICATIONS.md','LICENSE','THIRD_PARTY_NOTICES.txt')
    $files+=@('OneTaskBar.ico','OneTaskBar.png','OneTaskBar.svg','StoreLogo.png','Square150x150Logo.png','Square44x44Logo.png'|ForEach-Object {'assets/'+$_})
    foreach($name in $files){$source=Join-Path $dist $name;if(-not(Test-Path -LiteralPath $source)){throw ('Missing release file: '+$name)};[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$source,$name,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}
}finally{$archive.Dispose()}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$setup=Join-Path $release 'OneTaskBar-Setup.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/win32manifest:$root\OneTaskBar.manifest" "/win32icon:$root\assets\OneTaskBar.ico" "/resource:$zip,OneTaskBar.Payload.zip" "/out:$setup" (Join-Path $PSScriptRoot 'Setup.cs') (Join-Path $root 'src\AssemblyInfo.cs')
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
Copy-Item -LiteralPath $zip -Destination (Join-Path $release 'OneTaskBar-Portable-x64.zip') -Force
$lines=Get-ChildItem -LiteralPath $release -File|Where-Object {$_.Name -in @('OneTaskBar-Setup.exe','OneTaskBar-Portable-x64.zip')}|ForEach-Object {((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())+'  '+$_.Name}
[IO.File]::WriteAllLines((Join-Path $release 'SHA256SUMS.txt'),[string[]]$lines,[Text.Encoding]::ASCII)
Write-Output $setup
