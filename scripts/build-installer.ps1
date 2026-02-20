param(
    [ValidateSet('win-x64', 'win-x86', 'win-arm64')]
    [string]$Runtime = 'win-x64',
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\GymManager.App\GymManager.App.csproj'
$iss = Join-Path $root 'installer\inno\GymManager.iss'

if (-not (Test-Path $project)) { throw "Project not found: $project" }
if (-not (Test-Path $iss)) { throw "Installer script not found: $iss" }

Write-Host "1) Publish single-file (self-contained)..." -ForegroundColor Cyan
dotnet publish $project -c $Configuration -r $Runtime --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    /p:DebugType=None `
    /p:DebugSymbols=false

Write-Host ""
Write-Host "2) Find Inno Setup (ISCC.exe)..." -ForegroundColor Cyan

$iscc = (Get-Command iscc.exe -ErrorAction SilentlyContinue)?.Source
if (-not $iscc) {
    $candidates = @(
        Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe',
        Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    $iscc = $candidates
}

if (-not $iscc) {
    Write-Host ""
    Write-Host "未找到 Inno Setup 编译器（ISCC.exe）。" -ForegroundColor Yellow
    Write-Host "请先安装 Inno Setup 6，然后重新运行本脚本：" -ForegroundColor Yellow
    Write-Host "  https://jrsoftware.org/isinfo.php" -ForegroundColor Yellow
    throw "ISCC.exe not found"
}

Write-Host "  ISCC: $iscc"

Write-Host ""
Write-Host "3) Build installer..." -ForegroundColor Cyan
& $iscc $iss /DMyRuntime=$Runtime /DMyConfiguration=$Configuration | Out-Host

$dist = Join-Path $root 'dist'
Write-Host ""
Write-Host "Installer output folder:" -ForegroundColor Green
Write-Host "  $dist"

