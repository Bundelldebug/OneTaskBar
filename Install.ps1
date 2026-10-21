[CmdletBinding()]
param([switch]$NoLaunch,[switch]$NoShortcut,[switch]$NoStartup,[switch]$EnableStartup,[switch]$StagingOnly,[string]$Destination)
$ErrorActionPreference='Stop'
$sourceDir=$PSScriptRoot
if(-not(Test-Path -LiteralPath (Join-Path $sourceDir 'OneTaskBar.exe'))){$sourceDir=Join-Path $sourceDir 'dist'}
$sourceExe=Join-Path $sourceDir 'OneTaskBar.exe'
if(-not(Test-Path -LiteralPath $sourceExe)){throw 'Build the app first, or extract the complete release ZIP.'}
$baseDir=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'OneTaskBar'
$installDir=Join-Path $baseDir 'App'
$stateDir=Join-Path $baseDir 'Data'
if($StagingOnly){if(-not $Destination){throw 'StagingOnly requires Destination.'};$installDir=[IO.Path]::GetFullPath($Destination)}
elseif($Destination){throw 'Use StagingOnly for a custom extraction directory.'}
$installedExe=Join-Path $installDir 'OneTaskBar.exe'
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class OneTaskBarInstallPath {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
 public static extern uint GetFinalPathNameByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle file,StringBuilder path,uint length,uint flags);
}
"@

function Resolve-InstallFile([string]$path) {
    if(-not(Test-Path -LiteralPath $path)){return [IO.Path]::GetFullPath($path)}
    $stream=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try{$buffer=New-Object Text.StringBuilder 32768;$length=[OneTaskBarInstallPath]::GetFinalPathNameByHandle($stream.SafeFileHandle,$buffer,32768,0);if($length -eq 0 -or $length -ge 32768){throw 'Cannot resolve installed executable.'};$resolved=$buffer.ToString();if($resolved.StartsWith('\\?\UNC\')){return '\\'+$resolved.Substring(8)};if($resolved.StartsWith('\\?\')){return $resolved.Substring(4)};return $resolved}finally{$stream.Dispose()}
}

$installedPaths=@($installedExe,(Resolve-InstallFile $installedExe))
$guardExe=Join-Path $installDir 'OneTaskBar.Guard.exe'
$guardPaths=@($guardExe,(Resolve-InstallFile $guardExe))
$wasInstalled=Test-Path -LiteralPath (Join-Path $stateDir 'installation.json')
$startupEnabled=$false
if(-not $StagingOnly){
    # Normal per-user installation never elevates, trusts certificates, or registers a package.
    $run=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
    try{$startupEnabled=($null -ne $run.GetValue('OneTaskBar',$null)) -or (-not $wasInstalled -and -not $NoStartup)}finally{$run.Dispose()}
    if($NoStartup){$startupEnabled=$false}
    elseif($EnableStartup){$startupEnabled=$true}
    # Upgrade only this product and its known personal predecessor.
    $legacyExe=Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Codex\OneUIHome\One UI 홈.exe'
    foreach($exe in @($installedExe,$legacyExe)){
        if(Test-Path -LiteralPath $exe){$process=Start-Process -FilePath $exe -ArgumentList '--stop' -WindowStyle Hidden -PassThru;$process.WaitForExit(4000)|Out-Null}
    }
    $mainDeadline=(Get-Date).AddSeconds(8)
    do{
        $mainStopping=@(Get-Process -Name 'OneTaskBar' -ErrorAction SilentlyContinue|Where-Object {try{$installedPaths -contains $_.Path}catch{$false}})
        if($mainStopping.Count -eq 0){break};Start-Sleep -Milliseconds 100
    }while((Get-Date) -lt $mainDeadline)
    $liveMain=@(Get-Process -Name 'OneTaskBar' -ErrorAction SilentlyContinue|Where-Object {try{$installedPaths -contains $_.Path}catch{$false}})
    if($liveMain.Count -eq 0){
        $oldGuards=@(Get-Process -Name 'OneTaskBar.Guard' -ErrorAction SilentlyContinue|Where-Object {try{$guardPaths -contains $_.Path}catch{$false}})
        if($oldGuards.Count){
            foreach($helper in $oldGuards){$helper.WaitForExit(1000)|Out-Null}
            if(Test-Path -LiteralPath $installedExe){$recovery=Start-Process -FilePath $installedExe -ArgumentList '--recover' -WindowStyle Hidden -PassThru;$recovery.WaitForExit(4000)|Out-Null}
            foreach($helper in $oldGuards){if(-not $helper.HasExited -and $guardPaths -contains $helper.Path){Stop-Process -Id $helper.Id -Force}}
        }
    }
    $deadline=(Get-Date).AddSeconds(12)
    do{
        $owned=@(Get-Process -Name 'OneTaskBar','OneTaskBar.Guard','One UI 홈' -ErrorAction SilentlyContinue|Where-Object {try{$installedPaths -contains $_.Path -or $guardPaths -contains $_.Path -or $_.Path -eq $legacyExe}catch{$false}})
        if($owned.Count -eq 0){break};Start-Sleep -Milliseconds 150
    }while((Get-Date)-lt $deadline)
    if($owned.Count){throw 'Close One TaskBar before updating. Your running app has not been terminated.'}
    New-Item -ItemType Directory -Path $stateDir -Force|Out-Null
    $legacyData=Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Codex\OneUIHomeData'
    foreach($name in @('settings.json','layout.json','dock-pins.json','search-history.json','explorer-pins.json')){
        $from=Join-Path $legacyData $name;$to=Join-Path $stateDir $name
        if((Test-Path -LiteralPath $from)-and -not(Test-Path -LiteralPath $to)){Copy-Item -LiteralPath $from -Destination $to}
    }
    # Windows notifications stay native until the user enables the optional in-app connection.
    $settings=Join-Path $stateDir 'settings.json'
    if(-not(Test-Path -LiteralPath $settings)){[IO.File]::WriteAllText($settings,'{"NativeNotifications":true}',[Text.Encoding]::UTF8)}
}
New-Item -ItemType Directory -Path $installDir -Force|Out-Null
function Get-PayloadHash([string]$path) {
    $algorithm=[Security.Cryptography.SHA256]::Create();$stream=[IO.File]::OpenRead($path)
    try{return [Convert]::ToBase64String($algorithm.ComputeHash($stream))}finally{$stream.Dispose();$algorithm.Dispose()}
}
function Copy-PayloadFile([string]$from,[string]$to) {
    if((Test-Path -LiteralPath $to) -and (Get-PayloadHash $from) -eq (Get-PayloadHash $to)){return}
    try { Copy-Item -LiteralPath $from -Destination $to -Force -ErrorAction Stop }
    catch [IO.IOException] {
        if([IO.Path]::GetExtension($to) -ne '.exe' -or -not(Test-Path -LiteralPath $to)){throw}
        # Windows can keep an image mapped briefly after normal exit. Rename retains
        # the old image for that mapping; never terminate another process to update.
        $previous=$to+'.previous-'+[Guid]::NewGuid().ToString('N')
        Move-Item -LiteralPath $to -Destination $previous -ErrorAction Stop
        try { Copy-Item -LiteralPath $from -Destination $to -Force -ErrorAction Stop }
        catch { Move-Item -LiteralPath $previous -Destination $to -Force; throw }
        try { Remove-Item -LiteralPath $previous -Force -ErrorAction Stop } catch { }
    }
}
$files=@('OneTaskBar.exe','OneTaskBar.exe.config','OneTaskBar.Guard.exe','Read-Apps.ps1','Install.ps1','Uninstall.ps1','Uninstall.cmd','README.md','README-ko.md','VALIDATION.md','docs/NOTIFICATIONS.md','LICENSE','THIRD_PARTY_NOTICES.txt')
$assets=@('OneTaskBar.ico','OneTaskBar.png','OneTaskBar.svg','StoreLogo.png','Square150x150Logo.png','Square44x44Logo.png')
if(-not[string]::Equals([IO.Path]::GetFullPath($sourceDir),[IO.Path]::GetFullPath($installDir),[StringComparison]::OrdinalIgnoreCase)){
    foreach($name in $files){$from=Join-Path $sourceDir $name;if(-not(Test-Path -LiteralPath $from)){throw ('Incomplete payload: '+$name)};$to=Join-Path $installDir $name;New-Item -ItemType Directory -Path (Split-Path -Parent $to) -Force|Out-Null;Copy-PayloadFile $from $to}
    New-Item -ItemType Directory -Path (Join-Path $installDir 'assets') -Force|Out-Null
    foreach($name in $assets){Copy-PayloadFile (Join-Path $sourceDir ('assets\'+$name)) (Join-Path $installDir ('assets\'+$name))}
}
if($StagingOnly){Write-Output ('Payload extracted and verified: '+$installDir);return}
$launchExe=Resolve-InstallFile $installedExe
$launchDir=Split-Path -Parent $launchExe

$run=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
try{
    if($startupEnabled){$run.SetValue('OneTaskBar',('"'+$launchExe+'" --desktop --logon'),[Microsoft.Win32.RegistryValueKind]::String)}else{$run.DeleteValue('OneTaskBar',$false)}
    foreach($name in @('OneUIHome','One UI 홈')){$value=[string]$run.GetValue($name,'');if($value -match 'OneUIHome|One UI 홈'){$run.DeleteValue($name,$false)}}
}finally{$run.Dispose()}
$shortcutShell=New-Object -ComObject WScript.Shell
try{
    $programs=Join-Path ([Environment]::GetFolderPath('Programs')) 'One TaskBar'
    New-Item -ItemType Directory -Path $programs -Force|Out-Null
    $links=@((Join-Path $programs 'One TaskBar.lnk'))
    if(-not $NoShortcut){$links+=Join-Path ([Environment]::GetFolderPath('Desktop')) 'One TaskBar.lnk'}
    foreach($path in $links){$link=$shortcutShell.CreateShortcut($path);$link.TargetPath=$launchExe;$link.Arguments='--desktop';$link.WorkingDirectory=$launchDir;$link.IconLocation=$launchExe+',0';$link.Save()}
}finally{[Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcutShell)|Out-Null}
$uninstall=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Uninstall\OneTaskBar')
try{
    $command='powershell.exe -NoProfile -ExecutionPolicy Bypass -File "'+(Join-Path $launchDir 'Uninstall.ps1')+'"'
    foreach($entry in @{DisplayName='One TaskBar';DisplayVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($installedExe).FileVersion;Publisher='OneTaskBar contributors';InstallLocation=$launchDir;DisplayIcon=$launchExe;UninstallString=$command;QuietUninstallString=$command}.GetEnumerator()){$uninstall.SetValue($entry.Key,$entry.Value)}
    $uninstall.SetValue('NoModify',1,[Microsoft.Win32.RegistryValueKind]::DWord);$uninstall.SetValue('NoRepair',1,[Microsoft.Win32.RegistryValueKind]::DWord)
}finally{$uninstall.Dispose()}
[IO.File]::WriteAllText((Join-Path $stateDir 'installation.json'),(@{Version=[Diagnostics.FileVersionInfo]::GetVersionInfo($installedExe).FileVersion;Executable=$launchExe;Installed=(Get-Date).ToString('o');StartupEnabled=$startupEnabled;AdministratorRequired=$false}|ConvertTo-Json),[Text.Encoding]::UTF8)
if(-not $NoLaunch){Start-Process -FilePath $launchExe -ArgumentList '--desktop' -WindowStyle Hidden}
Write-Output ('Installed: '+$installedExe)
