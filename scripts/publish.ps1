param(
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $true
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "src\EdgeFolders\EdgeFolders.csproj"
$output = Join-Path $root "publish\$Runtime"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "dotnet was not found. Install .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    exit 1
}

dotnet publish $project `
    --configuration Release `
    --runtime $Runtime `
    --self-contained $SelfContained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    --output $output

Write-Host "Done: $output\EdgeFolders.exe" -ForegroundColor Green
