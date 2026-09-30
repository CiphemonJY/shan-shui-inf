# Builds dist\ShanShui-screensaver-win-x64.zip: self-contained ShanShui.scr (no .NET install needed)
# plus install/uninstall scripts, README and licences.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
python build.py
if ($LASTEXITCODE) { throw 'build.py failed' }
dotnet publish ShanShui.csproj -c Release -o bin\publish -p:SelfContained=true -p:EnableCompressionInSingleFile=true -p:DebugType=none
if ($LASTEXITCODE) { throw 'dotnet publish failed' }

$stage = 'bin\package'
Remove-Item $stage -Recurse -ErrorAction SilentlyContinue
New-Item $stage -ItemType Directory | Out-Null
Copy-Item bin\publish\ShanShui.exe "$stage\ShanShui.scr"
Copy-Item install.cmd, uninstall.cmd, README.md, LICENSE $stage
Copy-Item ..\LICENSE "$stage\LICENSE-shan-shui-inf"

New-Item dist -ItemType Directory -Force | Out-Null
$zip = 'dist\ShanShui-screensaver-win-x64.zip'
Compress-Archive "$stage\*" $zip -Force
Get-Item $zip | Select-Object Name, Length
