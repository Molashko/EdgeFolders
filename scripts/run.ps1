$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "src\EdgeFolders\EdgeFolders.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "dotnet was not found. Install .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    exit 1
}

dotnet run --project $project --configuration Release
