$ErrorActionPreference = 'Stop'
$testRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outputPath = Join-Path ([System.IO.Path]::GetTempPath()) ('pulse-gesture-' + [guid]::NewGuid().ToString('N') + '.exe')
& $compiler /nologo "/out:$outputPath" (Join-Path $testRoot 'Assets\Scripts\Calibration\HandGestureState.cs') (Join-Path $PSScriptRoot 'HandGestureStateTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Gesture test compilation failed.' }
try {
    & $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'Gesture regression tests failed.' }
}
finally { Remove-Item -LiteralPath $outputPath -ErrorAction SilentlyContinue }
