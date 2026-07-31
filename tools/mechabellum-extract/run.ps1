# One-shot extraction: creates the venv if missing, then runs the whole chain.
#   .\run.ps1            extract + build tables
#   .\run.ps1 -Compare   also diff against Melper.Core/Data/units.json
param([switch]$Compare)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$py = ".\.venv\Scripts\python.exe"

if (-not (Test-Path $py)) {
    Write-Host "venv not found, creating..." -ForegroundColor Yellow
    uv venv --python 3.11 .venv
    uv pip install --python $py -r requirements.txt
}

& $py scripts\dump_all.py
& $py scripts\make_units.py
if ($Compare) { & $py scripts\compare_with_csharp.py }

Write-Host "`ndone -> data\units.csv" -ForegroundColor Green
