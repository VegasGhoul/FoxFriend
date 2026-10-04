$ErrorActionPreference = 'Stop'
$foxRoot = $PSScriptRoot
$foxFramework = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319'
$foxReferences = @('System.dll','System.Core.dll','System.Xaml.dll','System.Net.Http.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $foxFramework $_) }
& (Join-Path $foxFramework 'csc.exe') /nologo /target:exe /utf8output "/out:$foxRoot\tools\IconBuilder.exe" @foxReferences "$foxRoot\tools\IconBuilder.cs"
if ($LASTEXITCODE -ne 0) { throw 'Icon builder failed' }
& "$foxRoot\tools\IconBuilder.exe" "$foxRoot\assets\fox-icon.png" "$foxRoot\assets\fox.ico"
$foxSources = Get-ChildItem -LiteralPath "$foxRoot\src" -Filter '*.cs' | ForEach-Object { $_.FullName }
& (Join-Path $foxFramework 'csc.exe') /nologo /target:winexe /utf8output /optimize+ "/out:$foxRoot\Lisichka.exe" "/win32icon:$foxRoot\assets\fox.ico" "/win32manifest:$foxRoot\src\app.manifest" "/resource:$foxRoot\src\FoxMind.corpus.tsv,FoxMind.corpus.tsv" "/resource:$foxRoot\src\Theme.xaml,FoxFriend.Theme.xaml" @foxReferences @foxSources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Output "Built: $foxRoot/Lisichka.exe"
