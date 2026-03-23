// src/VoiceText.Config/MicrophoneDevice.cs
namespace VoiceText.Config;

/// <summary>Real class so WPF bindings (DisplayMemberPath, SelectedValuePath) work via reflection.</summary>
public sealed class MicrophoneDevice
{
    public string Id { get; }
    public string Name { get; }
    public MicrophoneDevice(string id, string name) { Id = id; Name = name; }
    public override string ToString() => Name;
}
