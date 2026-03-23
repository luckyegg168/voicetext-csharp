// src/VoiceText.Config/MicrophoneEnumerator.cs
using NAudio.Wave;

namespace VoiceText.Config;

public class MicrophoneEnumerator
{
    public IReadOnlyList<(string Id, string Name)> GetDevices()
    {
        var devices = new List<(string, string)>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            devices.Add((i.ToString(), caps.ProductName));
        }
        return devices;
    }
}
