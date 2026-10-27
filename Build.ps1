param([switch]$SkipTests)
$ErrorActionPreference='Stop'
$projectRoot=$PSScriptRoot
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$out=Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $out -Force | Out-Null
if(-not(Test-Path -LiteralPath (Join-Path $projectRoot 'assets\OneTaskBar.ico'))){& (Join-Path $projectRoot 'tools\MakeAssets.ps1')}
$refs=@('System.dll','System.Core.dll','System.Xml.dll','System.Numerics.dll',"$env:WINDIR\Microsoft.NET\assembly\GAC_MSIL\System.Numerics.Vectors\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Numerics.Vectors.dll",'System.Xaml.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Management.dll','System.Web.Extensions.dll','System.Runtime.dll','System.Runtime.WindowsRuntime.dll','System.Runtime.InteropServices.WindowsRuntime.dll','System.ObjectModel.dll') +
    @('System.Data.dll') + @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','UIAutomationClient.dll','UIAutomationProvider.dll','UIAutomationTypes.dll' | ForEach-Object {Join-Path "$framework\WPF" $_}) +
    @('Windows.Foundation.winmd','Windows.Graphics.winmd','Windows.Devices.winmd','Windows.Media.winmd','Windows.Storage.winmd','Windows.UI.winmd','Windows.ApplicationModel.winmd','Windows.Data.winmd','Windows.System.winmd','Windows.Management.winmd','Windows.Networking.winmd' | ForEach-Object {Join-Path "$env:WINDIR\System32\WinMetadata" $_})
$compilerArgs=@('/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001',"/win32icon:$(Join-Path $projectRoot 'assets\OneTaskBar.ico')", "/win32manifest:$(Join-Path $projectRoot 'OneTaskBar.manifest')", "/resource:$(Join-Path $projectRoot 'Package.appxmanifest'),OneTaskBar.IdentityManifest", "/out:$(Join-Path $out 'OneTaskBar.exe')") +
    @($refs | ForEach-Object {"/reference:$_"}) + @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object {$_.FullName})
& (Join-Path $framework 'csc.exe') @compilerArgs
if($LASTEXITCODE -ne 0) {throw 'Compilation failed'}
$guardExe=Join-Path $out 'OneTaskBar.Guard.exe'
$guardStateFile=Join-Path $projectRoot '.build\guard-build-state.json'
$guardFlags='/nologo /target:winexe /platform:x64 /optimize+'
$guardInputs=$guardFlags+'|'+((@('tools\Guard.cs','src\AssemblyInfo.cs','OneTaskBar.manifest')|ForEach-Object {(Get-FileHash -LiteralPath (Join-Path $projectRoot $_) -Algorithm SHA256).Hash}) -join '|')+'|'+(Get-FileHash -LiteralPath (Join-Path $framework 'csc.exe') -Algorithm SHA256).Hash
$guardState=$null
try{if((Test-Path -LiteralPath $guardStateFile) -and (Get-Item -LiteralPath $guardStateFile).Length -lt 8192){$guardState=Get-Content -LiteralPath $guardStateFile -Raw|ConvertFrom-Json}}catch{}
$reuseGuard=$guardState -and $guardState.Inputs -ceq $guardInputs -and (Test-Path -LiteralPath $guardExe) -and $guardState.Sha256 -ceq (Get-FileHash -LiteralPath $guardExe -Algorithm SHA256).Hash
if(-not $reuseGuard){
    & (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:x64 /optimize+ "/win32manifest:$projectRoot\OneTaskBar.manifest" "/out:$guardExe" (Join-Path $projectRoot 'tools\Guard.cs') (Join-Path $projectRoot 'src\AssemblyInfo.cs')
    if($LASTEXITCODE -ne 0){throw 'Recovery helper compilation failed'}
}
New-Item -ItemType Directory -Path (Split-Path -Parent $guardStateFile) -Force|Out-Null
[IO.File]::WriteAllText($guardStateFile,(@{Inputs=$guardInputs;Sha256=(Get-FileHash -LiteralPath $guardExe -Algorithm SHA256).Hash}|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $projectRoot 'assets') -Destination $out -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'OneTaskBar.exe.config') -Destination (Join-Path $out 'OneTaskBar.exe.config') -Force
foreach($name in @('Read-Apps.ps1','Install.ps1','Uninstall.ps1','Uninstall.cmd','README.md','README-ko.md','VALIDATION.md','docs/NOTIFICATIONS.md','LICENSE','THIRD_PARTY_NOTICES.txt')) {if(Test-Path -LiteralPath (Join-Path $projectRoot $name)) {$destination=Join-Path $out $name;New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force|Out-Null;Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $destination -Force}}
if(-not $SkipTests) {
    $test=Start-Process -FilePath (Join-Path $out 'OneTaskBar.exe') -ArgumentList '--self-test' -PassThru -Wait -WindowStyle Hidden
    if($test.ExitCode -ne 0) {throw 'Self-test failed; see ONE_TASKBAR_DATA or LocalAppData\OneTaskBar\Data\self-test.json'}
}
Write-Output (Join-Path $out 'OneTaskBar.exe')
