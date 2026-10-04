$ErrorActionPreference = 'Stop'
$foxRoot = Split-Path $PSScriptRoot -Parent
$foxCompiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $foxCompiler /nologo /utf8output /target:exe "/out:$PSScriptRoot\LogicTests.exe" "/resource:$foxRoot\src\FoxMind.corpus.tsv,FoxMind.corpus.tsv" "$foxRoot\src\Companion.cs" "$foxRoot\src\FoxMind.cs" "$foxRoot\src\Preferences.cs" "$foxRoot\src\ConversationAnalyzer.cs" "$foxRoot\src\DialogueRules.cs" "$PSScriptRoot\LogicTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& "$PSScriptRoot/LogicTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Behavior tests failed' }
$foxReferences=@('System.dll','System.Core.dll','System.Xaml.dll','System.Net.Http.dll','System.Web.Extensions.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll')|ForEach-Object{'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+$_}
& $foxCompiler /nologo /utf8output /target:exe "/out:$PSScriptRoot\UpdateTests.exe" @foxReferences "$foxRoot\src\Updates.cs" "$foxRoot\src\AssemblyInfo.cs" "$PSScriptRoot\UpdateTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Update test compilation failed' }
& "$PSScriptRoot/UpdateTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Update tests failed' }
$foxRender = Start-Process -FilePath "$foxRoot/Lisichka.exe" -ArgumentList '--render-check' -WindowStyle Hidden -Wait -PassThru
if ($foxRender.ExitCode -ne 0) { throw 'Render checks failed' }
Get-Content -LiteralPath "$PSScriptRoot/renders/layout-result.txt"
Get-Content -LiteralPath "$PSScriptRoot/renders/blink-result.txt"
Get-Content -LiteralPath "$PSScriptRoot/renders/tail-result.txt"
$foxSmoke = Start-Process -FilePath "$foxRoot/Lisichka.exe" -ArgumentList '--smoke' -WindowStyle Hidden -Wait -PassThru
Get-Content -LiteralPath "$PSScriptRoot/renders/smoke-result.txt"
if ($foxSmoke.ExitCode -ne 0) { throw 'Live smoke checks failed' }
