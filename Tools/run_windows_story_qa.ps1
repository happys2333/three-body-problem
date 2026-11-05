param([string]$RequestFile = 'Documentation/WindowsStoryQA/request.json')
$ErrorActionPreference = 'Stop'
$request = Get-Content -LiteralPath $RequestFile -Raw | ConvertFrom-Json
$work = Join-Path $env:RUNNER_TEMP 'WindowsStoryQA'
$results = Join-Path $work 'Results'
$game = Join-Path $work 'Game'
New-Item -ItemType Directory -Path $results, $game -Force | Out-Null
$configuration = $env:TRAVELLER_QA_CONFIG | ConvertFrom-Json
if (-not $configuration.apiKey) { throw 'The temporary real-model test configuration is missing.' }
Write-Output ('::add-mask::' + $configuration.apiKey)
$privateFile = Join-Path $work 'connection.private.json'
$configuration | ConvertTo-Json -Compress | Set-Content -LiteralPath $privateFile -Encoding utf8NoBOM
$checkpointToken = $env:GH_TOKEN
function Publish-Checkpoint($state) {
    if (-not $request.releaseId) { return }
    try {
        $headers = @{Authorization = ('Bearer ' + $checkpointToken); Accept = 'application/vnd.github+json'}
        $api = 'https://api.github.com/repos/' + $env:GITHUB_REPOSITORY
        $assets = Invoke-RestMethod -Headers $headers -Uri ($api + '/releases/' + $request.releaseId + '/assets') -TimeoutSec 10
        foreach ($asset in $assets) {
            if ($asset.name -eq 'story-progress.json') { Invoke-RestMethod -Method Delete -Headers $headers -Uri ($api + '/releases/assets/' + $asset.id) -TimeoutSec 10 | Out-Null }
        }
        $checkpoint = @{runId = $env:GITHUB_RUN_ID; phase = $state.finalStep; passed = $state.passed;
            authenticated = $state.authenticated; checks = $state.checks; realModelReplies = $state.realModelReplies;
            transientRetries = $state.transientRetries; visited = $state.visited; errors = $state.errors;
            updatedUtc = [DateTime]::UtcNow.ToString('o')}
        $data = [Text.Encoding]::UTF8.GetBytes(($checkpoint | ConvertTo-Json -Depth 5 -Compress))
        $upload = 'https://uploads.github.com/repos/' + $env:GITHUB_REPOSITORY + '/releases/' + $request.releaseId + '/assets?name=story-progress.json'
        Invoke-RestMethod -Method Post -Headers $headers -Uri $upload -ContentType 'application/json' -Body $data -TimeoutSec 10 | Out-Null
    } catch { Write-Output 'Live checkpoint unavailable; the complete report remains in the result directory.' }
}
$process = $null
try {
    & gh release download $request.tag --repo $env:GITHUB_REPOSITORY --pattern $request.asset --dir $work
    if ($LASTEXITCODE) { throw 'Could not download the private QA build.' }
    $archive = Join-Path $work $request.asset
    if ((Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $request.sha256) { throw 'QA ZIP integrity mismatch.' }
    Expand-Archive -LiteralPath $archive -DestinationPath $game
    if ($request.patch) {
        & gh release download $request.tag --repo $env:GITHUB_REPOSITORY --pattern $request.patch.asset --dir $work
        if ($LASTEXITCODE) { throw 'Could not download the QA patch.' }
        $patch = Join-Path $work $request.patch.asset
        if ((Get-FileHash $patch -Algorithm SHA256).Hash.ToLowerInvariant() -ne $request.patch.sha256) { throw 'QA patch integrity mismatch.' }
        Expand-Archive -LiteralPath $patch -DestinationPath $game -Force
    }
    $root = Join-Path $game 'ThreeBodyTraveller-Windows-x64'
    $manifest = Get-Content (Join-Path $root 'playtest-manifest.json') -Raw | ConvertFrom-Json
    if (-not $manifest.storyQa) { throw 'The workflow requires the separately instrumented story QA player.' }
    if ($manifest.sourceCommit -ne $request.sourceCommit) { throw 'QA source version mismatch.' }
    foreach ($entry in $manifest.files.PSObject.Properties) {
        $file = Join-Path $root $entry.Name
        if (-not (Test-Path -LiteralPath $file)) { throw ('Missing QA file: ' + $entry.Name) }
        if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value.sha256) { throw ('Corrupt QA file: ' + $entry.Name) }
    }
    $env:TRAVELLER_QA_CONFIG = $null
    $env:GH_TOKEN = $null
    $report = Join-Path $results 'story-report.json'
    $log = Join-Path $results 'player.log'
    $arguments = @('-silent-crashes', '-force-d3d11', '-force-driver-type-warp',
        '-screen-fullscreen', '0', '-screen-width', '960', '-screen-height', '540',
        '-logFile', ('"' + $log + '"'), '-windows-story-qa', ('"' + $report + '"'),
        '-story-qa-config', ('"' + $privateFile + '"'))
    $process = Start-Process -FilePath (Join-Path $root 'ThreeBodyTraveller.exe') -ArgumentList $arguments -WorkingDirectory $root -PassThru
    $null = $process.Handle
    $deadline = [DateTime]::UtcNow.AddMinutes(32)
    $phase = ''
    $replies = -1
    while (-not $process.HasExited -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Seconds 10
        if (Test-Path -LiteralPath $report) {
            try {
                $state = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
                if ($state.finalStep -ne $phase -or $state.realModelReplies -ne $replies) {
                    $phase = $state.finalStep
                    $replies = $state.realModelReplies
                    Write-Output ('Story phase: ' + $phase + '; real model replies: ' + $state.realModelReplies)
                    Publish-Checkpoint $state
                }
            } catch { }
        }
        $process.Refresh()
    }
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force; throw 'Windows story test timed out.' }
    $process.WaitForExit()
    if (-not (Test-Path -LiteralPath $report)) { throw 'The game exited without a story report. Inspect player.log.' }
    $state = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    Publish-Checkpoint $state
    $state | ConvertTo-Json -Depth 10 | Write-Output
    if ($process.ExitCode -ne 0 -or -not $state.passed -or $state.finalStep -ne 'Summary') { throw 'The native Windows full-story test failed.' }
} finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    foreach ($file in Get-ChildItem -LiteralPath $results -File) {
        if ($file.Extension -in '.log', '.json', '.txt') {
            $text = [IO.File]::ReadAllText($file.FullName)
            [IO.File]::WriteAllText($file.FullName, $text.Replace($configuration.apiKey, '[REDACTED]'))
        }
    }
    Remove-Item -LiteralPath $privateFile -Force -ErrorAction SilentlyContinue
}
