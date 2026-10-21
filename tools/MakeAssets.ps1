$ErrorActionPreference='Stop'
$sourceRoot=Split-Path -Parent $PSScriptRoot
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$work=Join-Path $sourceRoot '.build'
New-Item -ItemType Directory -Path $work -Force|Out-Null
$references=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll')|ForEach-Object {'/reference:'+(Join-Path "$framework\WPF" $_)}
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:x64 /reference:System.Xaml.dll "/out:$work\AssetBuilder.exe" @references (Join-Path $PSScriptRoot 'AssetBuilder.cs')
if($LASTEXITCODE -ne 0){throw 'Icon builder failed'}
$process=Start-Process -FilePath (Join-Path $work 'AssetBuilder.exe') -ArgumentList ('"'+(Join-Path $sourceRoot 'assets')+'"') -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){throw 'Icon rendering failed'}
