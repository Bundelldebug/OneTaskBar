[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$baseDir=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'OneTaskBar'
$installDir=[IO.Path]::GetFullPath($PSScriptRoot)
$expected=[IO.Path]::GetFullPath((Join-Path $baseDir 'App'))
$hostExpected=[IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Packages\OpenAI.Codex_2p2nqsd0c76g0\LocalCache\Local\OneTaskBar\App'))
if(-not [string]::Equals($installDir,$expected,[StringComparison]::OrdinalIgnoreCase) -and -not [string]::Equals($installDir,$hostExpected,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected uninstall location.'}
$exe=Join-Path $installDir 'OneTaskBar.exe'
if(Test-Path -LiteralPath $exe){$stop=Start-Process -FilePath $exe -ArgumentList '--stop' -PassThru -WindowStyle Hidden;$stop.WaitForExit(4000)|Out-Null}
$deadline=(Get-Date).AddSeconds(12)
do{$running=@(Get-Process -Name OneTaskBar,OneTaskBar.Guard -ErrorAction SilentlyContinue|Where-Object {try{$_.Path -eq $exe -or $_.Path -eq (Join-Path $installDir 'OneTaskBar.Guard.exe')}catch{$false}});if(-not $running.Count){break};Start-Sleep -Milliseconds 150}while((Get-Date)-lt $deadline)
if($running.Count){throw 'Close One TaskBar before uninstalling. Your settings have been preserved.'}
$taskService=$null
$taskFolder=$null
$taskEntry=$null
try{
    $taskService=New-Object -ComObject Schedule.Service
    $taskService.Connect()
    $taskFolder=$taskService.GetFolder('\')
    try{$taskEntry=$taskFolder.GetTask('OneTaskBar.Startup')}catch{$taskError=$_.Exception;while($taskError.InnerException){$taskError=$taskError.InnerException};if($taskError.HResult -ne -2147024894 -and $taskError.HResult -ne -2147216625){throw}}
    if($taskEntry){
        $taskSettings=[Xml.XmlReaderSettings]::new();$taskSettings.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$taskSettings.XmlResolver=$null
        $taskReader=[Xml.XmlReader]::Create([IO.StringReader]::new($taskEntry.Xml),$taskSettings)
        try{$taskXml=[Xml.XmlDocument]::new();$taskXml.XmlResolver=$null;$taskXml.Load($taskReader)}finally{$taskReader.Dispose()}
        $taskNs=[Xml.XmlNamespaceManager]::new($taskXml.NameTable);$taskNs.AddNamespace('t','http://schemas.microsoft.com/windows/2004/02/mit/task')
        $taskSource=$taskXml.SelectSingleNode('/t:Task/t:RegistrationInfo/t:Source',$taskNs)
        $taskCommand=$taskXml.SelectSingleNode('/t:Task/t:Actions/t:Exec/t:Command',$taskNs)
        $taskArguments=$taskXml.SelectSingleNode('/t:Task/t:Actions/t:Exec/t:Arguments',$taskNs)
        $taskUser=$taskXml.SelectSingleNode('/t:Task/t:Principals/t:Principal/t:UserId',$taskNs)
        $taskLogon=$taskXml.SelectSingleNode('/t:Task/t:Principals/t:Principal/t:LogonType',$taskNs)
        $taskLevel=$taskXml.SelectSingleNode('/t:Task/t:Principals/t:Principal/t:RunLevel',$taskNs)
        $taskSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        if($taskSource -and $taskSource.InnerText -ceq ('OneTaskBar.Logon.v1|'+$taskSid) -and $taskCommand -and [string]::Equals($taskCommand.InnerText,$exe,[StringComparison]::OrdinalIgnoreCase) -and $taskArguments -and $taskArguments.InnerText -ceq '--desktop --logon' -and $taskUser -and $taskUser.InnerText -ceq $taskSid -and $taskLogon -and $taskLogon.InnerText -ceq 'InteractiveToken' -and (-not $taskLevel -or $taskLevel.InnerText -ceq 'LeastPrivilege') -and $taskXml.SelectNodes('/t:Task/t:Actions/*',$taskNs).Count -eq 1){$taskFolder.DeleteTask('OneTaskBar.Startup',0)}
    }
}finally{foreach($taskCom in @($taskEntry,$taskFolder,$taskService)){if($taskCom -and [Runtime.InteropServices.Marshal]::IsComObject($taskCom)){[Runtime.InteropServices.Marshal]::FinalReleaseComObject($taskCom)|Out-Null}}}
$run=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run',$true)
try{if($run -and (([string]$run.GetValue('OneTaskBar','')).Contains($exe) -or ([string]$run.GetValue('OneTaskBar','')).Contains('OneTaskBar.Desktop_t4xw24pnxmkbp!OneTaskBar'))){$run.DeleteValue('OneTaskBar',$false)}}finally{if($run){$run.Dispose()}}
$software=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Uninstall',$true)
try{if($software){$software.DeleteSubKeyTree('OneTaskBar',$false)}}finally{if($software){$software.Dispose()}}
$shortcutShell=New-Object -ComObject WScript.Shell
try{
    foreach($path in @((Join-Path ([Environment]::GetFolderPath('Desktop')) 'One TaskBar.lnk'),(Join-Path ([Environment]::GetFolderPath('Programs')) 'One TaskBar\One TaskBar.lnk'))){if(Test-Path -LiteralPath $path){$link=$shortcutShell.CreateShortcut($path);if($link.TargetPath -eq $exe){Remove-Item -LiteralPath $path -Force}}}
}finally{[Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcutShell)|Out-Null}
# Only remove the exact per-user App directory. Data is a sibling and is retained.
$expected=[IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'OneTaskBar\App'))
if(-not [string]::Equals($installDir,$expected,[StringComparison]::OrdinalIgnoreCase) -and -not [string]::Equals($installDir,$hostExpected,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected uninstall location.'}
if(Test-Path -LiteralPath $installDir){Remove-Item -LiteralPath $installDir -Recurse -Force}
Write-Output 'One TaskBar removed. Windows taskbar restored; settings retained in LocalAppData\OneTaskBar\Data.'
