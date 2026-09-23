$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$appProject = Join-Path $root "src\PCDoctor.App\PCDoctor.App.csproj"
$setupProject = Join-Path $root "src\PCDoctor.Setup\PCDoctor.Setup.csproj"
$payloadDir = Join-Path $root "src\PCDoctor.Setup\payload"
$distDir = Join-Path $root "dist"

New-Item -ItemType Directory -Force -Path $payloadDir | Out-Null
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

Write-Host "Publishing Hytera Pc Doctor (single-file)..."
dotnet publish $appProject -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=embedded `
    -o $payloadDir
if ($LASTEXITCODE -ne 0) { throw "App publish failed." }

Write-Host "Publishing setup..."
dotnet publish $setupProject -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=embedded `
    -o $distDir
if ($LASTEXITCODE -ne 0) { throw "Setup publish failed." }

$setup = Join-Path $distDir "HyteraPcDoctor-Setup.exe"
if (-not (Test-Path $setup)) { throw "Setup exe was not created." }
Write-Host "Setup ready: $setup"
