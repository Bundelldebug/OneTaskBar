param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$rows=@(Get-StartApps | Sort-Object Name | Select-Object Name,AppID)
$json=ConvertTo-Json -InputObject $rows -Depth 3
[IO.File]::WriteAllText($OutputPath,$json,(New-Object Text.UTF8Encoding($false)))
