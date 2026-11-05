param(
    [ValidateSet('speak', 'recognize')][string]$Operation,
    [string]$InputFile,
    [string]$OutputFile
)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
Add-Type -AssemblyName System.Speech
if ($Operation -eq 'speak') {
    $request = Get-Content -LiteralPath $InputFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $speaker = [System.Speech.Synthesis.SpeechSynthesizer]::new()
    try {
        $voice = $speaker.GetInstalledVoices() | Where-Object { $_.Enabled -and $_.VoiceInfo.Culture.Name -like 'zh-*' } | Select-Object -First 1
        if ($voice) { $speaker.SelectVoice($voice.VoiceInfo.Name) }
        $format = [System.Speech.AudioFormat.SpeechAudioFormatInfo]::new(16000, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
        $speaker.SetOutputToWaveFile($OutputFile, $format)
        $speaker.Speak([string]$request.text)
    } finally { $speaker.Dispose() }
} else {
    $info = [System.Speech.Recognition.SpeechRecognitionEngine]::InstalledRecognizers() | Where-Object { $_.Culture.Name -like 'zh-*' } | Select-Object -First 1
    if (-not $info) { throw 'Chinese speech recognition is unavailable. Use Enter for text dialogue.' }
    $recognizer = [System.Speech.Recognition.SpeechRecognitionEngine]::new($info)
    try {
        $recognizer.LoadGrammar([System.Speech.Recognition.DictationGrammar]::new())
        $recognizer.SetInputToWaveFile($InputFile)
        $result = $recognizer.Recognize([TimeSpan]::FromSeconds(25))
        @{text = $(if ($result) { $result.Text } else { '' })} | ConvertTo-Json -Compress
    } finally { $recognizer.Dispose() }
}
