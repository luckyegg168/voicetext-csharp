// src/VoiceText.Audio/VadPipeline.cs
namespace VoiceText.Audio;

public class VadPipeline : IDisposable
{
    private readonly VadEngine _vad;
    private readonly List<float> _speechBuffer = new();
    private readonly double _silenceTimeoutMs;
    private DateTime _lastSpeechTime = DateTime.MinValue;
    private bool _wasSpeech = false;
    private const int VadChunkSamples = 512; // 32ms at 16kHz

    public event EventHandler<float[]>? SpeechSegmentReady;

    public VadPipeline(VadEngine vad, double silenceTimeoutMs = 1500)
    {
        _vad = vad;
        _silenceTimeoutMs = silenceTimeoutMs;
    }

    public void Feed(AudioChunk chunk)
    {
        var samples = chunk.Samples;
        int offset = 0;

        while (offset + VadChunkSamples <= samples.Length)
        {
            var window = samples[offset..(offset + VadChunkSamples)];
            offset += VadChunkSamples;

            bool isSpeech = _vad.IsSpeech(window, chunk.SampleRate);

            if (isSpeech)
            {
                _speechBuffer.AddRange(window);
                _lastSpeechTime = DateTime.UtcNow;
                _wasSpeech = true;
            }
            else if (_wasSpeech)
            {
                double silenceMs = (DateTime.UtcNow - _lastSpeechTime).TotalMilliseconds;
                if (silenceMs >= _silenceTimeoutMs)
                {
                    var segment = _speechBuffer.ToArray();
                    _speechBuffer.Clear();
                    _vad.Reset();
                    _wasSpeech = false;
                    SpeechSegmentReady?.Invoke(this, segment);
                }
            }
        }
    }

    public float[]? FlushIfAny()
    {
        if (_speechBuffer.Count == 0) return null;
        var segment = _speechBuffer.ToArray();
        _speechBuffer.Clear();
        _vad.Reset();
        _wasSpeech = false;
        return segment;
    }

    public void Dispose() => _vad.Dispose();
}
