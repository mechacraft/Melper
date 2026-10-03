# Builds every mod in this folder and copies the DLLs into the game's Mods folder.
#   .\deploy.ps1                 build and deploy all mods
#   .\deploy.ps1 -Run            then start the game through Steam
#   .\deploy.ps1 -GameRoot X:\…  game installed somewhere else
# A running game keeps its loaded copies; the new ones load on the next start.
param(
    [string]$GameRoot = 'D:\SteamLibrary\steamapps\common\Mechabellum',
    [switch]$Run
)

$ErrorActionPreference = 'Stop'
$failed = @()

foreach ($project in Get-ChildItem $PSScriptRoot -Filter *.csproj -Recurse -Depth 1) {
    Write-Host "`n=== $($project.Directory.Name) ===" -ForegroundColor Cyan
    dotnet build $project.FullName -c Release -p:Deploy=true -p:GameRoot=$GameRoot -nologo -v:minimal
    if ($LASTEXITCODE -ne 0) { $failed += $project.Directory.Name }
}

if ($failed) {
    Write-Host "`nFailed: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}

Write-Host "`nAll mods deployed to $GameRoot\Mods" -ForegroundColor Green
if (Get-Process Mechabellum -ErrorAction SilentlyContinue) {
    Write-Host 'The game is running: restart it to load the new versions.' -ForegroundColor Yellow
}
elseif ($Run) {
    Start-Process 'steam://rungameid/669330'
}
