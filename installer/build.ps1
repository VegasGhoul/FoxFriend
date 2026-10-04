$ErrorActionPreference='Stop'
$foxRoot=Split-Path $PSScriptRoot -Parent
$foxPackage=Join-Path $PSScriptRoot 'payload'
New-Item -ItemType Directory -Path $foxPackage,(Join-Path $foxPackage 'assets') -Force | Out-Null
Copy-Item -LiteralPath "$foxRoot/Lisichka.exe" -Destination "$foxPackage/FoxFriend.exe" -Force
foreach($foxAsset in @('fox-atlas.png','fox-icon.png','fox.ico')){Copy-Item -LiteralPath "$foxRoot/assets/$foxAsset" -Destination "$foxPackage/assets/$foxAsset" -Force}
Copy-Item -LiteralPath "$foxRoot/ПРОЧИТАЙ МЕНЯ.md" -Destination "$foxPackage/README.md" -Force
$foxFiles=@('FoxFriend.exe','assets/fox-atlas.png','assets/fox-icon.png','assets/fox.ico','README.md')
$foxHashes=foreach($foxFile in $foxFiles){(Get-FileHash -LiteralPath (Join-Path $foxPackage $foxFile) -Algorithm SHA256).Hash+"`t"+$foxFile}
[IO.File]::WriteAllLines((Join-Path $foxPackage 'SHA256SUMS.txt'),$foxHashes,[Text.Encoding]::UTF8)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$foxZip=Join-Path $PSScriptRoot 'payload.zip'
if(Test-Path -LiteralPath $foxZip){Remove-Item -LiteralPath $foxZip}
[IO.Compression.ZipFile]::CreateFromDirectory($foxPackage,$foxZip)
$foxFramework='C:/Windows/Microsoft.NET/Framework64/v4.0.30319'
$foxReferences=@('System.dll','System.Core.dll','System.Xaml.dll','System.Drawing.dll','System.Windows.Forms.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','Microsoft.CSharp.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll')|ForEach-Object{'/reference:'+(Join-Path $foxFramework $_)}
& (Join-Path $foxFramework 'csc.exe') /nologo /target:winexe /utf8output /optimize+ "/out:$foxRoot\installer\FoxFriend.exe" "/win32icon:$foxRoot\assets\fox.ico" "/win32manifest:$foxRoot\src\app.manifest" "/resource:$foxZip,FoxFriend.payload.zip" "/resource:$foxRoot\src\Theme.xaml,FoxFriend.Theme.xaml" "/resource:$foxRoot\assets\fox-icon.png,FoxFriend.icon.png" @foxReferences "$PSScriptRoot\Setup.cs"
if($LASTEXITCODE -ne 0){throw 'Installer build failed'}
Write-Output "Installer: $PSScriptRoot/FoxFriend.exe"
$foxInstallerHash=(Get-FileHash -LiteralPath "$PSScriptRoot/FoxFriend.exe" -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$PSScriptRoot/FoxFriend.exe.sha256",$foxInstallerHash+'  FoxFriend.exe'+[Environment]::NewLine,[Text.Encoding]::ASCII)
