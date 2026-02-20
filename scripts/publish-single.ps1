param(
    [ValidateSet('win-x64', 'win-x86', 'win-arm64')]
    [string]$Runtime = 'win-x64',
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [switch]$ReadyToRun
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\GymManager.App\GymManager.App.csproj'

if (-not (Test-Path $project)) {
    throw "Project not found: $project"
}

$props = @(
    '/p:PublishSingleFile=true',
    '/p:SelfContained=true',
    # SQLite 等依赖包含 native 组件：单文件需要允许自解压
    '/p:IncludeNativeLibrariesForSelfExtract=true',
    '/p:EnableCompressionInSingleFile=true',
    '/p:DebugType=None',
    '/p:DebugSymbols=false'
)

if ($ReadyToRun) {
    $props += '/p:PublishReadyToRun=true'
}

Write-Host "Publishing single EXE..."
Write-Host "  Project: $project"
Write-Host "  Config : $Configuration"
Write-Host "  Runtime: $Runtime"

dotnet publish $project -c $Configuration -r $Runtime @props

$publishDir = Join-Path $root ("src\GymManager.App\bin\{0}\net8.0-windows\{1}\publish" -f $Configuration, $Runtime)
$exePath = Join-Path $publishDir 'GymManager.App.exe'

Write-Host ""
Write-Host "Publish output:"
Write-Host "  $publishDir"
Write-Host "EXE:"
Write-Host "  $exePath"

