using System.Text.Json.Serialization;

namespace PersianTypoFixer.Core;

public sealed class CustomWord
{
    [JsonPropertyName("on")]
    public bool On { get; set; } = true;

    [JsonPropertyName("from")]
    public string From { get; set; } = "";

    [JsonPropertyName("to")]
    public string To { get; set; } = "";
}

public class FixOptions
{
    public bool Enabled { get; set; } = true;
    public bool AutoFix { get; set; } = true;
    public bool FixArabicYK { get; set; } = true;
    public bool FixArabicAlef { get; set; } = true;
    public bool FixTashkeel { get; set; } = true;
    public bool FixKashida { get; set; } = true;
    public bool FixZWNJ { get; set; } = true;
    public bool FixAttachedHa { get; set; } = true;
    public bool FixCompounds { get; set; } = true;
    public bool FixCommonTypos { get; set; } = true;
    public bool FixPunctuation { get; set; } = true;
    public bool FixPersianPunct { get; set; } = true;
    public bool FixDoubleSpace { get; set; } = true;
    public bool FixArabicDigits { get; set; } = true;
    public bool FixLatinDigits { get; set; } = false;
    public bool FixSeparators { get; set; } = false;
    public bool FixRepeat { get; set; } = false;
    public bool FixQuotes { get; set; } = false;
    public bool Notices { get; set; } = true;
    public List<string> DisabledSites { get; set; } = new();
    public List<CustomWord> CustomWords { get; set; } = new();

    public FixOptions Clone()
    {
        var copy = (FixOptions)MemberwiseClone();
        copy.DisabledSites = new List<string>(DisabledSites);
        copy.CustomWords = CustomWords
            .Select(w => new CustomWord { On = w.On, From = w.From, To = w.To })
            .ToList();
        return copy;
    }
}

public sealed class TypingResult
{
    public string Text { get; set; } = "";
    public int Cursor { get; set; }
}
