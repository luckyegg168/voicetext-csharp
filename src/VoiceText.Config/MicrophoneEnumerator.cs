// src/VoiceText.Config/MicrophoneEnumerator.cs
using NAudio.Wave;

namespace VoiceText.Config;

public class MicrophoneEnumerator
{
    public IReadOnlyList<MicrophoneDevice> GetDevices()
    {
        var devices = new List<MicrophoneDevice>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            devices.Add(new MicrophoneDevice(i.ToString(), caps.ProductName));
        }
        return devices;
    }
}
