// src/VoiceText.Audio/VadEngine.cs
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace VoiceText.Audio;

public class VadEngine : IDisposable
{
    private readonly InferenceSession _session;
    private float[] _h = new float[2 * 1 * 64];
    private float[] _c = new float[2 * 1 * 64];
    private const float Threshold = 0.5f;

    public VadEngine(string modelPath)
    {
        _session = new InferenceSession(modelPath);
    }

    public bool IsSpeech(float[] samples, int sampleRate)
    {
        // Silero VAD expects chunks of exactly 512 (16kHz) or 256 (8kHz) samples
        var inputTensor = new DenseTensor<float>(samples, new int[] { 1, samples.Length });
        var srTensor = new DenseTensor<long>(new long[] { sampleRate }, new int[] { 1 });
        var hTensor = new DenseTensor<float>(_h, new int[] { 2, 1, 64 });
        var cTensor = new DenseTensor<float>(_c, new int[] { 2, 1, 64 });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor),
            NamedOnnxValue.CreateFromTensor("sr", srTensor),
            NamedOnnxValue.CreateFromTensor("h", hTensor),
            NamedOnnxValue.CreateFromTensor("c", cTensor),
        };

        using var outputs = _session.Run(inputs);
        var prob = outputs.First(o => o.Name == "output").AsEnumerable<float>().First();
        _h = outputs.First(o => o.Name == "hn").AsEnumerable<float>().ToArray();
        _c = outputs.First(o => o.Name == "cn").AsEnumerable<float>().ToArray();

        return prob >= Threshold;
    }

    public void Reset()
    {
        _h = new float[2 * 1 * 64];
        _c = new float[2 * 1 * 64];
    }

    public void Dispose() => _session.Dispose();
}
