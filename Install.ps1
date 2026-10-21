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
    $deadline=(Get-Date).AddSeconds(12)
    do{
        $owned=@(Get-Process -Name 'OneTaskBar','OneTaskBar.Guard','One UI 홈' -ErrorAction SilentlyContinue|Where-Object {try{$_.Path -eq $installedExe -or $_.Path -eq (Join-Path $installDir 'OneTaskBar.Guard.exe') -or $_.Path -eq $legacyExe}catch{$false}})
        if($owned.Count -eq 0){break};Start-Sleep -Milliseconds 150
    }while((Get-Date)-lt $deadline)
    if($owned.Count){throw 'Close One TaskBar before updating. Your running app has not been terminated.'}
    New-Item -ItemType Directory -Path $stateDir -Force|Out-Null
    $legacyData=Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Codex\OneUIHomeData'
    foreach($name in @('settings.json','layout.json','dock-pins.json','search-history.json','explorer-pins.json')){
        $from=Join-Path $legacyData $name;$to=Join-Path $stateDir $name
        if((Test-Path -LiteralPath $from)-and -not(Test-Path -LiteralPath $to)){Copy-Item -LiteralPath $from -Destination $to}
    }
    # Unpackaged installs use Windows notifications until a signed identity is provided.
    $settings=Join-Path $stateDir 'settings.json'
    if(-not(Test-Path -LiteralPath $settings)){[IO.File]::WriteAllText($settings,'{"NativeNotifications":true}',[Text.Encoding]::UTF8)}
}
New-Item -ItemType Directory -Path $installDir -Force|Out-Null
$files=@('OneTaskBar.exe','OneTaskBar.exe.config','OneTaskBar.Guard.exe','Read-Apps.ps1','Install.ps1','Uninstall.ps1','Uninstall.cmd','README.md','README-ko.md','VALIDATION.md','docs/NOTIFICATIONS.md','LICENSE','THIRD_PARTY_NOTICES.txt')
$assets=@('OneTaskBar.ico','OneTaskBar.png','OneTaskBar.svg','StoreLogo.png','Square150x150Logo.png','Square44x44Logo.png')
if(-not[string]::Equals([IO.Path]::GetFullPath($sourceDir),[IO.Path]::GetFullPath($installDir),[StringComparison]::OrdinalIgnoreCase)){
    foreach($name in $files){$from=Join-Path $sourceDir $name;if(-not(Test-Path -LiteralPath $from)){throw ('Incomplete payload: '+$name)};$to=Join-Path $installDir $name;New-Item -ItemType Directory -Path (Split-Path -Parent $to) -Force|Out-Null;Copy-Item -LiteralPath $from -Destination $to -Force}
    New-Item -ItemType Directory -Path (Join-Path $installDir 'assets') -Force|Out-Null
    foreach($name in $assets){Copy-Item -LiteralPath (Join-Path $sourceDir ('assets\'+$name)) -Destination (Join-Path $installDir ('assets\'+$name)) -Force}
}
if($StagingOnly){Write-Output ('Payload extracted and verified: '+$installDir);return}
$run=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
try{
    if($startupEnabled){$run.SetValue('OneTaskBar',('"'+$installedExe+'" --desktop --logon'),[Microsoft.Win32.RegistryValueKind]::String)}else{$run.DeleteValue('OneTaskBar',$false)}
    foreach($name in @('OneUIHome','One UI 홈')){$value=[string]$run.GetValue($name,'');if($value -match 'OneUIHome|One UI 홈'){$run.DeleteValue($name,$false)}}
}finally{$run.Dispose()}
$shortcutShell=New-Object -ComObject WScript.Shell
try{
    $programs=Join-Path ([Environment]::GetFolderPath('Programs')) 'One TaskBar'
    New-Item -ItemType Directory -Path $programs -Force|Out-Null
    $links=@((Join-Path $programs 'One TaskBar.lnk'))
    if(-not $NoShortcut){$links+=Join-Path ([Environment]::GetFolderPath('Desktop')) 'One TaskBar.lnk'}
    foreach($path in $links){$link=$shortcutShell.CreateShortcut($path);$link.TargetPath=$installedExe;$link.Arguments='--desktop';$link.WorkingDirectory=$installDir;$link.IconLocation=$installedExe+',0';$link.Save()}
}finally{[Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcutShell)|Out-Null}
$uninstall=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Uninstall\OneTaskBar')
try{
    $command='powershell.exe -NoProfile -ExecutionPolicy Bypass -File "'+(Join-Path $installDir 'Uninstall.ps1')+'"'
    foreach($entry in @{DisplayName='One TaskBar';DisplayVersion='0.3.0';Publisher='OneTaskBar contributors';InstallLocation=$installDir;DisplayIcon=$installedExe;UninstallString=$command;QuietUninstallString=$command}.GetEnumerator()){$uninstall.SetValue($entry.Key,$entry.Value)}
    $uninstall.SetValue('NoModify',1,[Microsoft.Win32.RegistryValueKind]::DWord);$uninstall.SetValue('NoRepair',1,[Microsoft.Win32.RegistryValueKind]::DWord)
}finally{$uninstall.Dispose()}
[IO.File]::WriteAllText((Join-Path $stateDir 'installation.json'),(@{Version='0.3.0';Executable=$installedExe;Installed=(Get-Date).ToString('o');StartupEnabled=$startupEnabled;AdministratorRequired=$false}|ConvertTo-Json),[Text.Encoding]::UTF8)
if(-not $NoLaunch){Start-Process -FilePath $installedExe -ArgumentList '--desktop' -WindowStyle Hidden}
Write-Output ('Installed: '+$installedExe)
