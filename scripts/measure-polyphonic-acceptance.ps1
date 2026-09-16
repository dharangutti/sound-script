param(
    [Parameter(Mandatory=$true)][string]$RecordingsDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$cli = Join-Path $repo 'src/SoundScript.Cli/bin/Debug/net10.0/soundscript.dll'
if (!(Test-Path -LiteralPath $cli)) { throw 'Build the Debug CLI before measuring.' }
if ((Test-Path -LiteralPath $OutputDirectory) -and (Get-ChildItem -LiteralPath $OutputDirectory | Select-Object -First 1)) { throw 'Choose an empty output directory to avoid stale acceptance artifacts.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$out = (Resolve-Path -LiteralPath $OutputDirectory).Path
$ffmpeg = if ($env:SOUNDSCRIPT_FFMPEG) { $env:SOUNDSCRIPT_FFMPEG } else { 'ffmpeg' }
$recordings = @('alanajordan-piano-solo-245698','music_for_videos-emotional-solo-piano-166032')
$measurements = @()
foreach ($recording in $recordings) {
    $inputFile = Join-Path $RecordingsDirectory "$recording.mp3"
    $hash = (Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash
    foreach ($start in @(0,30,60,90,110)) {
        $id = "$recording-$start"
        $original = Join-Path $out "$id-original.wav"
        & $ffmpeg -nostdin -y -ss $start -i $inputFile -t 10 -vn -ac 1 -ar 16000 $original *> (Join-Path $out "$id-decode.log")
        if ($LASTEXITCODE -ne 0) { throw "Excerpt decode failed: $id" }
        foreach ($mode in @('monophonic','extract-melody','polyphonic')) {
            $stem = Join-Path $out "$id-$mode"
            & dotnet $cli transcribe $inputFile --start $start --duration 10 --mode $mode --instrument piano --out "$stem.ss" --preview "$stem.wav" --report "$stem.json" *> "$stem.log"
            $code = $LASTEXITCODE
            if (!(Test-Path -LiteralPath "$stem.json")) { throw "No report for $id / $mode (exit $code). Inspect $stem.log" }
            $report = Get-Content -LiteralPath "$stem.json" -Raw | ConvertFrom-Json
            $measurements += [PSCustomObject]@{
                Id=$id; Recording=$recording; SourceSha256=$hash; StartSeconds=$start; DurationSeconds=10;
                Mode=$mode; ExitCode=$code; Generated=$report.Generated; Status=$report.Transcription.Suitability.Status;
                NoteCount=($report.Transcription.Score.Tracks | ForEach-Object { $_.Notes.Count } | Measure-Object -Sum).Sum;
                VoiceCount=$report.Transcription.Score.Tracks.Count;
                StableCoverage=$report.Transcription.Suitability.StableActiveFraction;
                MaximumSimultaneousNotes=$report.Transcription.Polyphony.MaximumSimultaneousNotes;
                ChordCount=$report.Transcription.Polyphony.ChordCount;
                MeanActivePolyphony=$report.Transcription.Polyphony.MeanActivePolyphony;
                PolyphonicCoverage=$report.Transcription.Polyphony.PolyphonicCoverage;
                AmbiguousFrameFraction=$report.Transcription.Polyphony.AmbiguousFrameFraction;
                OctaveAmbiguousFraction=$report.Transcription.Polyphony.OctaveAmbiguousFraction;
                RoundTrip=$report.RoundTrip; ListeningReview='Pending human review'
            }
            Write-Host "$id / $mode : $($report.Transcription.Suitability.Status), notes=$($measurements[-1].NoteCount), exit=$code"
        }
    }
}
$measurements | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $out 'measurements.json') -Encoding utf8
& node (Join-Path $PSScriptRoot 'polyphonic-listening-report.cjs') $out
if ($LASTEXITCODE -ne 0) { throw 'Listening report generation failed.' }
Write-Host "Listening report: $(Join-Path $out 'index.html')"
