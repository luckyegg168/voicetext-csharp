// src/VoiceText.Audio/VadEngine.cs
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace VoiceText.Audio;

public class VadEngine : IDisposable
{
    private readonly InferenceSession? _session;
    private readonly bool _requiresSampleRateInput;
    private readonly bool _supportsHiddenStateInputs;
    private readonly bool _supportsSingleStateInput;
    private float[] _h = new float[2 * 1 * 64];
    private float[] _c = new float[2 * 1 * 64];
    private float[] _state = new float[2 * 1 * 128];
    private float[] _context = Array.Empty<float>();
    private float _threshold = 0.5f;
    public bool IsAvailable => _session != null;
    public float Threshold
    {
        get => _threshold;
        set => _threshold = Math.Clamp(value, 0.01f, 0.99f);
    }

    public VadEngine(string modelPath)
    {
        try
        {
            if (System.IO.File.Exists(modelPath))
            {
                _session = new InferenceSession(modelPath);
                _requiresSampleRateInput = _session.InputMetadata.ContainsKey("sr");
                _supportsHiddenStateInputs = _session.InputMetadata.ContainsKey("h") &&
                                             _session.InputMetadata.ContainsKey("c");
                _supportsSingleStateInput = _session.InputMetadata.ContainsKey("state");
            }
        }
        catch
        {
            _session = null;
        }
    }

    public float GetSpeechProbability(float[] samples, int sampleRate)
    {
        if (_session == null) return 0f;

        // Silero VAD expects chunks of exactly 512 (16kHz) or 256 (8kHz) samples
        var chunkSize = sampleRate == 8000 ? 256 : 512;
        var contextSize = sampleRate == 8000 ? 32 : 64;
        if (samples.Length != chunkSize)
            return 0f;

        var actualInput = samples;
        if (_supportsSingleStateInput)
        {
            if (_context.Length != contextSize)
                _context = new float[contextSize];

            actualInput = new float[contextSize + samples.Length];
            Array.Copy(_context, 0, actualInput, 0, contextSize);
            Array.Copy(samples, 0, actualInput, contextSize, samples.Length);
        }

        var inputTensor = new DenseTensor<float>(actualInput, new int[] { 1, actualInput.Length });
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", inputTensor) };
        if (_requiresSampleRateInput)
        {
            var srTensor = new DenseTensor<long>(new long[] { sampleRate }, new int[] { 1 });
            inputs.Add(NamedOnnxValue.CreateFromTensor("sr", srTensor));
        }

        if (_supportsHiddenStateInputs)
        {
            var hTensor = new DenseTensor<float>(_h, new int[] { 2, 1, 64 });
            var cTensor = new DenseTensor<float>(_c, new int[] { 2, 1, 64 });
            inputs.Add(NamedOnnxValue.CreateFromTensor("h", hTensor));
            inputs.Add(NamedOnnxValue.CreateFromTensor("c", cTensor));
        }
        else if (_supportsSingleStateInput)
        {
            var stateTensor = new DenseTensor<float>(_state, new int[] { 2, 1, 128 });
            inputs.Add(NamedOnnxValue.CreateFromTensor("state", stateTensor));
        }

        using var outputs = _session.Run(inputs);
        var prob = outputs.First(o => o.Name == "output").AsEnumerable<float>().First();
        if (_supportsHiddenStateInputs)
        {
            var hn = outputs.FirstOrDefault(o => o.Name == "hn");
            var cn = outputs.FirstOrDefault(o => o.Name == "cn");
            if (hn != null) _h = hn.AsEnumerable<float>().ToArray();
            if (cn != null) _c = cn.AsEnumerable<float>().ToArray();
        }
        else if (_supportsSingleStateInput)
        {
            var stateOut = outputs.FirstOrDefault(o => o.Name == "stateN") ??
                           outputs.FirstOrDefault(o => o.Name == "state");
            if (stateOut != null)
                _state = stateOut.AsEnumerable<float>().ToArray();
            _context = samples.ToArray()[^contextSize..];
        }

        return prob;
    }

    public bool IsSpeech(float[] samples, int sampleRate) =>
        GetSpeechProbability(samples, sampleRate) >= _threshold;

    public void Reset()
    {
        _h = new float[2 * 1 * 64];
        _c = new float[2 * 1 * 64];
        _state = new float[2 * 1 * 128];
        _context = Array.Empty<float>();
    }

    public void Dispose() => _session?.Dispose();
}
