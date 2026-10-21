[CmdletBinding()]
param([switch]$DeveloperRegistration,[switch]$NoLaunch)
$ErrorActionPreference='Stop'
if(-not $DeveloperRegistration){throw 'Development-only unsigned identity. Read docs/NOTIFICATIONS.md and explicitly pass -DeveloperRegistration.'}
$installed=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'OneTaskBar\App'
$packageFile=Join-Path $PSScriptRoot 'dist\OneTaskBar.identity.msix'
if(-not(Test-Path -LiteralPath (Join-Path $installed 'OneTaskBar.exe'))){throw 'Install One TaskBar first.'}
if(-not(Test-Path -LiteralPath $packageFile)){throw 'Run python tools-package.py in the source directory first.'}
$existing=Get-AppxPackage -Name 'OneTaskBar.Desktop' -ErrorAction SilentlyContinue
if(-not $existing){Add-AppxPackage -Path $packageFile -ExternalLocation $installed -AllowUnsigned -ErrorAction Stop}
$package=Get-AppxPackage -Name 'OneTaskBar.Desktop' -ErrorAction Stop
if(-not $package){throw 'Identity registration was not confirmed.'}
$aumid=$package.PackageFamilyName+'!OneTaskBar'
$stateDir=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'OneTaskBar\Data'
New-Item -ItemType Directory -Path $stateDir -Force|Out-Null
[IO.File]::WriteAllText((Join-Path $stateDir 'notification-registration.json'),(@{Registered=$true;AppID=$aumid;Package=$package.PackageFullName;DevelopmentOnly=$true}|ConvertTo-Json),[Text.Encoding]::UTF8)
Write-Output ('Launch registered identity: explorer.exe shell:AppsFolder\'+$aumid)
if(-not $NoLaunch){Start-Process -FilePath 'explorer.exe' -ArgumentList ('"shell:AppsFolder\'+$aumid+'"') -WindowStyle Hidden}
