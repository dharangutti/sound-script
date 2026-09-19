# Local, opt-in verification. Does not change the production solution or CI.
$ErrorActionPreference = 'Stop'
$labsRoot = $PSScriptRoot
$repoRoot = [IO.Path]::GetFullPath((Join-Path $labsRoot '../..'))
$runRoot = Join-Path $repoRoot ('artifacts/labs-verify-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runRoot | Out-Null
Push-Location $repoRoot
try {
    dotnet test (Join-Path $labsRoot 'SoundScript.Labs.slnx') -c Debug --logger 'trx;LogFileName=labs.trx' --results-directory $runRoot
    if ($LASTEXITCODE -ne 0) { throw 'Labs tests failed.' }
    $cli = Join-Path $labsRoot 'SoundScript.Labs.Cli'
    $source = Join-Path $labsRoot 'examples/tone.sslabs'
    foreach ($name in @('run-a', 'run-b')) {
        dotnet run --project $cli --no-build -- $source --out (Join-Path $runRoot $name)
        if ($LASTEXITCODE -ne 0) { throw "CLI $name failed." }
    }
    foreach ($file in @('result.json', 'signal.wav', 'signal.pcm', 'signal.f32')) {
        $a = (Get-FileHash -LiteralPath (Join-Path $runRoot "run-a/$file") -Algorithm SHA256).Hash
        $b = (Get-FileHash -LiteralPath (Join-Path $runRoot "run-b/$file") -Algorithm SHA256).Hash
        if ($a -ne $b) { throw "Repeatability failed: $file" }
    }
    Write-Output "Labs tests and separate-process byte comparisons passed. Evidence: $runRoot"
}
finally { Pop-Location }
