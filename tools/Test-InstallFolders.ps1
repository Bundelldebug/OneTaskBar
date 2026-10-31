$ErrorActionPreference='Stop'
$installer=Join-Path (Split-Path -Parent $PSScriptRoot) 'Install.ps1'
$tokens=$null;$errors=$null
$syntax=[Management.Automation.Language.Parser]::ParseFile($installer,[ref]$tokens,[ref]$errors)
if($errors.Count){throw ($errors | Out-String)}
$function=$syntax.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Resolve-UserFolder'},$true)
if(-not $function){throw 'Installer folder resolver is missing'}
Invoke-Expression $function.Extent.Text
function Assert([bool]$ok,[string]$message){if(-not $ok){throw $message}}
Assert ((Resolve-UserFolder '' @('','relative','C:\Users\Fixture\AppData\Local')) -eq 'C:\Users\Fixture\AppData\Local') 'An empty known folder must use an absolute fallback'
Assert ((Resolve-UserFolder 'C:\Redirected\Local' @('C:\Fallback')) -eq 'C:\Redirected\Local') 'A redirected known folder must take precedence'
Assert ((Resolve-UserFolder '\\server\profile\Documents' -Optional) -eq '\\server\profile\Documents') 'UNC known folders must remain usable'
Assert ($null -eq (Resolve-UserFolder '' -Optional)) 'An unavailable optional folder must skip optional migration or shortcut work'
$failed=$false
try{Resolve-UserFolder '' @('relative','C:relative') | Out-Null}catch{$failed=$true}
Assert $failed 'An unavailable essential folder must fail before changing the installation'
Write-Output 'Installer folder fallback cases passed; no installation or registry changes performed.'
