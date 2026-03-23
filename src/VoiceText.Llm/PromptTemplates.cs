// src/VoiceText.Llm/PromptTemplates.cs
namespace VoiceText.Llm;

public static class PromptTemplates
{
    public const string PolishSystemNatural =
        "你是一位專業編輯。請在保留原意的前提下，修正語音轉錄文字的語法、標點和用詞，使其流暢自然。只輸出修正後的文字，不加說明。";

    public const string PolishSystemFormal =
        "你是一位專業文書助理。請將以下語音轉錄文字改寫為正式書面語，修正語法並補充標點符號。只輸出修正後的文字。";

    public const string PolishSystemTechnical =
        "你是一位技術文件編輯。請將語音轉錄的技術內容整理為清晰的技術文件格式，保留術語準確性。只輸出修正後的文字。";

    public const string PolishSystemMeeting =
        "你是一位會議記錄助理。請將語音轉錄整理為結構化的會議摘要，標示重點決議與行動事項。只輸出整理後的文字。";

    public const string TranslationSystemToEnglish =
        "你是一位專業翻譯。請將以下文字翻譯成自然流暢的英文。只輸出翻譯結果，不加說明。";

    public const string TranslationSystemToChinese =
        "You are a professional translator. Translate the following text into natural, fluent Traditional Chinese. Output only the translation.";

    public static string GetPolishSystem(string style) => style switch
    {
        "formal" => PolishSystemFormal,
        "technical" => PolishSystemTechnical,
        "meeting" => PolishSystemMeeting,
        _ => PolishSystemNatural,
    };
}
