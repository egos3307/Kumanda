[CmdletBinding()] param([string]$Runtime='win-x64')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root 'dist/windows'
dotnet publish (Join-Path $root 'windows-receiver/CloudPad.Receiver/CloudPad.Receiver.csproj') -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $out
Write-Host "Published to $out"
