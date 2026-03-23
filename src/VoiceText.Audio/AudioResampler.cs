// src/VoiceText.Audio/AudioResampler.cs
namespace VoiceText.Audio;

public static class AudioResampler
{
    public static float[] Resample(float[] samples, int sourceSampleRate, int targetSampleRate)
    {
        if (sourceSampleRate == targetSampleRate)
            return samples;

        double ratio = (double)targetSampleRate / sourceSampleRate;
        int newLength = (int)(samples.Length * ratio);
        var result = new float[newLength];
        for (int i = 0; i < newLength; i++)
        {
            double srcPos = i / ratio;
            int lo = (int)srcPos;
            int hi = Math.Min(lo + 1, samples.Length - 1);
            double t = srcPos - lo;
            result[i] = (float)(samples[lo] * (1 - t) + samples[hi] * t);
        }
        return result;
    }

    public static float[] StereoToMono(float[] stereo)
    {
        var mono = new float[stereo.Length / 2];
        for (int i = 0; i < mono.Length; i++)
            mono[i] = (stereo[i * 2] + stereo[i * 2 + 1]) * 0.5f;
        return mono;
    }
}
