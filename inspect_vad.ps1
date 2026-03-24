Add-Type -Path 'D:\voicetext_csharp1\src\VoiceText.App\bin\Debug\net8.0-windows\Microsoft.ML.OnnxRuntime.dll'
$session = [Microsoft.ML.OnnxRuntime.InferenceSession]::new('D:\voicetext_csharp1\src\VoiceText.App\Assets\silero_vad.onnx')
'INPUTS'
$session.InputMetadata.GetEnumerator() | ForEach-Object { "{0} => {1}" -f $_.Key, ($_.Value.ElementType.ToString() + ' ' + ($_.Value.Dimensions -join ',')) }
'OUTPUTS'
$session.OutputMetadata.GetEnumerator() | ForEach-Object { "{0} => {1}" -f $_.Key, ($_.Value.ElementType.ToString() + ' ' + ($_.Value.Dimensions -join ',')) }
