// src/VoiceText.App/Converters/RecordingStateConverter.cs
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using VoiceText.App.ViewModels;

namespace VoiceText.App.Converters;

public class StateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is RecordingState state ? state switch
        {
            RecordingState.Recording => new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x30)),   // Red
            RecordingState.Transcribing => new SolidColorBrush(Color.FromRgb(0xFF, 0x95, 0x00)), // Orange
            RecordingState.Polishing => new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xFF)),    // Blue
            RecordingState.Done => new SolidColorBrush(Color.FromRgb(0x34, 0xC7, 0x59)),          // Green
            RecordingState.Error => new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x30)),         // Red
            _ => new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)),                            // Gray
        } : new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StateToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is RecordingState state ? state switch
        {
            RecordingState.Recording => "⏹",
            RecordingState.Transcribing => "⏳",
            RecordingState.Polishing => "✨",
            RecordingState.Done => "✓",
            RecordingState.Error => "⚠",
            _ => "🎙",
        } : "🎙";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StateToShadowColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is RecordingState state ? state switch
        {
            RecordingState.Recording => Color.FromRgb(0xFF, 0x3B, 0x30),
            RecordingState.Done => Color.FromRgb(0x34, 0xC7, 0x59),
            _ => Color.FromRgb(0x00, 0x7A, 0xFF),
        } : Color.FromRgb(0x00, 0x7A, 0xFF);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
