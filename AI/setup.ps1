param([string]$Python = 'python')
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & $Python -c "import sys; assert (3, 10) <= sys.version_info[:2] <= (3, 12), 'Use Python 3.10, 3.11 or 3.12'"
    if ($LASTEXITCODE -ne 0) { throw 'Python 3.10-3.12 is required.' }
    & $Python -m venv .venv
    if ($LASTEXITCODE -ne 0) { throw 'Could not create AI/.venv.' }
    $aiPython = Join-Path $PSScriptRoot '.venv\Scripts\python.exe'
    & $aiPython -m pip install -r requirements.txt
    if ($LASTEXITCODE -ne 0) { throw 'AI dependency installation failed.' }
    & $aiPython -m pip check
    if ($LASTEXITCODE -ne 0) { throw 'AI dependencies are inconsistent.' }
    & $aiPython main.py --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Model or MediaPipe validation failed.' }
    Write-Host 'AI setup completed. Open SampleScene in Unity and select Start Journey.'
}
finally { Pop-Location }
