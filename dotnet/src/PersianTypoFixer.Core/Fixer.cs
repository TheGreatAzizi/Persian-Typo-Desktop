using System.Text.RegularExpressions;

namespace PersianTypoFixer.Core;

public static class Fixer
{
    private const string FaL = "\u0621-\u064A\u067E\u0686\u0698\u06A9\u06AF\u06CC\u06BE\u06C0";
    private const string FaEndStrict = "(?![" + FaL + "\u200C])";
    private const string FaPre = @"(^|[\s\u200C\(\[""'«.,،:؛!؟…?;\)\]»])";
    private const string FaAfter = @"(?=[\s\u200C.,،:؛!؟?;…\)\]\[""'»]|$)";
    private const string HaAfter = @"([\s.,،:؛!؟?;…\)\]\[""'»\u200C]|$)";
    private const string Zwnj = "\u200C";
    private const string AllDigits = "0-9\u06F0-\u06F9\u0660-\u0669";
    private const string BaseChars = "[\u0621-\u0627\u0628\u067E\u062A\u062B\u062C\u0686\u062D\u062E\u062F\u0630\u0631\u0632\u0698\u0633\u0634\u0635\u0636\u0637\u0638\u0639\u063A\u0641\u0642\u0643\u06A9\u0644\u0645\u0646\u0648\u0649\u064A\u06CC\u0624\u0629]";

    private const string BareDomain =
        @"(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,}(?::[0-9]{1,5})?(?:/[^\s<>""']*)?";

    private static readonly Regex ArabicAnyRe = new("[\u0600-\u06FF]");
    private static readonly Regex UrlRe = new(@"https?://[^\s<>""']+|www\.[^\s<>""']+|[A-Za-z0-9_.+-]+@[A-Za-z0-9_-]+\.[A-Za-z0-9_.]+|" + BareDomain);
    private static readonly Regex PlaceholderRe = new("\uE000([A-Z]+)\uE001");
    private static readonly Regex StableSplitRe = new(@"^([\s\S]*[\s.,،:؛!؟?;…)\]""'»\n])([\s\S]*)$");
    private static readonly Regex FaOnlyRe = new("^[" + FaL + "\u200C]*$");
    private static readonly Regex TashkeelRe = new("[\u064B-\u065F\u0670]");
    private static readonly Regex ArabicDigitRe = new("[\u0660-\u0669]");
    private static readonly Regex ZwnjSpaceRe = new("[ \t\r]*\u200C[ \t\r]*");
    private static readonly Regex ZwnjRepeatRe = new("\u200C{2,}");
    private static readonly Regex EllipsisRe = new(@"\.{3,}");
    private static readonly Regex PunctSpace1Re = new(@"\s+([.,،:؛!؟?;…\)\]])");
    private static readonly Regex PunctSpace2Re = new(@"([\(\[])\s+");
    private static readonly Regex PunctSpace3Re = new("([.,،:؛!؟?;…])([^\\s" + AllDigits + ".,،:؛!؟?;…])");
    private static readonly Regex FaPunct1Re = new("([" + FaL + "\u200C])\\s+([,;?])");
    private static readonly Regex FaPunctCommaRe = new("(?<=[" + FaL + "\u200C]|،),+|,+(?=[" + FaL + "\u200C]|،)");
    private static readonly Regex FaPunctSemiRe = new("(?<=[" + FaL + "\u200C]|؛);+|;+(?=[" + FaL + "\u200C]|؛)");
    private static readonly Regex FaPunctQuestionRe = new("(?<=[" + FaL + "\u200C]|؟)\\?+|\\?+(?=[" + FaL + "\u200C]|؟)");
    private static readonly Regex PercentRe = new("(?<=[\u0600-\u06FF\u200C0-9\u06F0-\u06F9])%|%(?=[\u0600-\u06FF\u200C])");
    private static readonly Regex DoubleSpaceRe = new("[ \t]{2,}");
    private static readonly Regex TripleNewlineRe = new("\n{3,}");
    private static readonly Regex RepeatRe = new(@"([\u0600-\u06FF])\1{2,}");
    private static readonly Regex QuotesRe = new("\"([^\"\n]*[\u0600-\u06FF][^\"\n]*)\"");
    private static readonly Regex FaSpace2Re = new(" {2,}");

    private static readonly Regex ZwnjMiRe = new(FaPre + @"(می)\s+");
    private static readonly Regex ZwnjNemiRe = new(FaPre + @"(نمی)\s+");
    private static readonly Regex ZwnjBiRe = new(FaPre + @"(بی)\s+");
    private static readonly Regex ZwnjHaSuffixRe = new("([" + FaL + "]+[هۀ])\\s+(ام|ات|اش|مان|تان|شان|ای|ایم|اید|اند|یی|یم|یت|یش)" + FaEndStrict);
    private static readonly Regex ZwnjHaYeRe = new("([" + FaL + "]+[هۀ])\\s+ی(\\s|$|[.,،:؛!؟?;…\\)\\]\\s])");
    private static readonly Regex ZwnjBaseSuffixRe = new("(" + BaseChars + @")\s+(ام|ات|اش|مان|تان|شان)" + FaEndStrict);
    private static readonly Regex ZwnjTarinRe = new(FaPre + @"(به|کم|بیش)\s+(ترین|تری|تر)" + FaEndStrict);
    private static readonly Regex ZwnjSuffixRe = new("([" + FaL + "])\\s+(هایمان|هایتان|هایشان|هایم|هایت|هایش|هایی|های|ها|ترین|تری|تر)" + FaEndStrict);
    private static readonly Regex MiAttachedRe = new(FaPre + @"می([" + FaL + @"]{2,})" + FaEndStrict);
    private static readonly Regex NemiAttachedRe = new(FaPre + @"نمی([" + FaL + @"]{2,})" + FaEndStrict);
    private static readonly Regex Ha1Re = new("([" + FaL + "]{1,30}?)ها" + HaAfter);
    private static readonly Regex Ha2Re = new("([" + FaL + "]{2,30}?)های" + HaAfter);
    private static readonly Regex Ha3Re = new("([" + FaL + "]{2,30}?)هایی" + HaAfter);
    private static readonly Regex SeparatorCommaRe = new("([\u06F0-\u06F9]),([\u06F0-\u06F9])");
    private static readonly Regex SeparatorPointRe = new("([\u06F0-\u06F9])[./]([\u06F0-\u06F9])");

    private static readonly (Regex Pattern, string To)[] CompoundPatterns =
        Dictionaries.Compounds.Select(p => (new Regex(FaPre + EscapeWord(p.From) + FaAfter), p.To)).ToArray();

    private static readonly (Regex Pattern, string To)[] CommonTypoPatterns =
        Dictionaries.CommonTypos.Select(p => (new Regex(FaPre + EscapeWord(p.From) + FaAfter), p.To)).ToArray();

    private static readonly (Regex Pattern, string To)[] AttachedWordPatterns =
        Dictionaries.AttachedFixes.Select(p => (new Regex(FaPre + EscapeWord(p.From) + FaAfter), p.To)).ToArray();

    private static string EscapeWord(string s) => Regex.Escape(s).Replace("\\ ", " ");

    public static bool HitsSet(string word, HashSet<string> set)
    {
        foreach (var e in set)
            if (word == e || e.StartsWith(word, StringComparison.Ordinal))
                return true;
        return false;
    }

    public static string FixArabicYK(string t) =>
        t.Replace("\u064A", "\u06CC").Replace("\u0649", "\u06CC").Replace("\u0643", "\u06A9");

    public static string FixArabicAlef(string t)
    {
        t = t.Replace("\u0623", "\u0627");
        t = t.Replace("\u0625", "\u0627");
        t = t.Replace("\u0624", "\u0648");
        t = t.Replace("\u0629", "\u0647");
        return t;
    }

    public static string FixTashkeel(string t)
    {
        t = t.Replace("اً", "\uE011");
        t = TashkeelRe.Replace(t, "");
        t = t.Replace("\uE011", "اً");
        return t;
    }

    public static string FixKashida(string t) => t.Replace("\u0640", "");

    public static string FixArabicDigits(string t)
    {
        var map = new Dictionary<char, char>
        {
            ['\u0660'] = '\u06F0', ['\u0661'] = '\u06F1', ['\u0662'] = '\u06F2', ['\u0663'] = '\u06F3', ['\u0664'] = '\u06F4',
            ['\u0665'] = '\u06F5', ['\u0666'] = '\u06F6', ['\u0667'] = '\u06F7', ['\u0668'] = '\u06F8', ['\u0669'] = '\u06F9',
        };
        return ArabicDigitRe.Replace(t, m => map.TryGetValue(m.Value[0], out var c) ? c.ToString() : m.Value);
    }

    public static string NormalizeZWNJSpaces(string t)
    {
        t = ZwnjSpaceRe.Replace(t, Zwnj);
        t = ZwnjRepeatRe.Replace(t, Zwnj);
        return t;
    }

    public static string FixZWNJ(string t)
    {
        var output = t;
        output = ZwnjMiRe.Replace(output, m => m.Groups[1].Value + "می" + Zwnj);
        output = ZwnjNemiRe.Replace(output, m => m.Groups[1].Value + "نمی" + Zwnj);
        output = ZwnjBiRe.Replace(output, m => m.Groups[1].Value + "بی" + Zwnj);
        output = ZwnjHaSuffixRe.Replace(output, m => m.Groups[1].Value + Zwnj + m.Groups[2].Value);
        output = ZwnjHaYeRe.Replace(output, m => m.Groups[1].Value + Zwnj + "ی" + m.Groups[2].Value);
        output = ZwnjBaseSuffixRe.Replace(output, m =>
        {
            var @base = m.Groups[1].Value;
            return m.Groups[2].Value switch
            {
                "ام" => @base + "م",
                "ات" => @base + "ت",
                "اش" => @base + "ش",
                _ => @base + m.Groups[2].Value,
            };
        });
        output = ZwnjTarinRe.Replace(output, "$1$2$3");
        output = ZwnjSuffixRe.Replace(output, m => m.Groups[1].Value + Zwnj + m.Groups[2].Value);
        output = FixAttachedMi(output);
        return output;
    }

    public static string FixAttachedMi(string t)
    {
        var output = t;
        output = MiAttachedRe.Replace(output, m =>
        {
            var full = "می" + m.Groups[2].Value;
            return Dictionaries.MiAttachedExceptions.Contains(full)
                ? m.Value
                : m.Groups[1].Value + "می" + Zwnj + m.Groups[2].Value;
        });
        output = NemiAttachedRe.Replace(output, m =>
        {
            var full = "نمی" + m.Groups[2].Value;
            return Dictionaries.NemiAttachedExceptions.Contains(full)
                ? m.Value
                : m.Groups[1].Value + "نمی" + Zwnj + m.Groups[2].Value;
        });
        return output;
    }

    public static string FixAttachedHa(string t)
    {
        t = Ha1Re.Replace(t, m =>
        {
            var @base = m.Groups[1].Value;
            var full = @base + "ها";
            if (Dictionaries.AttachedHaExceptions.Contains(full) || full is "شما" or "همه" or "بها")
                return m.Value;
            if (@base.EndsWith("ها", StringComparison.Ordinal))
                return m.Value;
            if (@base.Length < 2 && full is not ("آنها" or "اینها"))
                return m.Value;
            return @base + Zwnj + "ها" + m.Groups[2].Value;
        });
        t = Ha2Re.Replace(t, m =>
        {
            var @base = m.Groups[1].Value;
            var full = @base + "های";
            if (Dictionaries.AttachedHaExceptions.Contains(full) || full is "تنهای" or "رهای" or "شهای")
                return m.Value;
            if (@base.EndsWith("های", StringComparison.Ordinal))
                return m.Value;
            return @base + Zwnj + "های" + m.Groups[2].Value;
        });
        t = Ha3Re.Replace(t, m =>
        {
            var @base = m.Groups[1].Value;
            var full = @base + "هایی";
            if (full is "تنهایی" or "رهایی")
                return m.Value;
            if (@base.EndsWith("هایی", StringComparison.Ordinal))
                return m.Value;
            return @base + Zwnj + "هایی" + m.Groups[2].Value;
        });
        return t;
    }

    public static string FixCompounds(string t)
    {
        var output = t;
        foreach (var (pattern, to) in CompoundPatterns)
            output = pattern.Replace(output, m => m.Groups[1].Value + to);
        return output;
    }

    public static string FixCommonTypos(string t)
    {
        var output = t;
        foreach (var (pattern, to) in CommonTypoPatterns)
            output = pattern.Replace(output, m => m.Groups[1].Value + to);
        return output;
    }

    public static string FixStems(string t)
    {
        var output = t;
        foreach (var (from, to) in Dictionaries.StemFixes)
            output = output.Replace(from, to, StringComparison.Ordinal);
        return output;
    }

    public static string FixSubs(string t)
    {
        var output = t;
        foreach (var (from, to) in Dictionaries.SubFixes)
            output = output.Replace(from, to, StringComparison.Ordinal);
        return output;
    }

    public static string FixAttachedWords(string t)
    {
        var output = t;
        foreach (var (pattern, to) in AttachedWordPatterns)
            output = pattern.Replace(output, m => m.Groups[1].Value + to);
        return output;
    }

    public static string NormCustomFrom(string s) => FixArabicAlef(FixArabicYK(s ?? ""));

    public static string FixCustomWords(string t, IEnumerable<CustomWord> customs)
    {
        if (customs == null)
            return t;
        var output = t;
        foreach (var item in customs.Take(200))
        {
            if (item == null || !item.On)
                continue;
            var from = (item.From ?? "").Trim();
            var to = (item.To ?? "").Trim();
            if (from.Length == 0 || to.Length == 0 || from.Length > 60 || to.Length > 60)
                continue;
            from = NormCustomFrom(from);
            if (from.Length == 0 || from == to)
                continue;
            output = Regex.Replace(output, FaPre + EscapeWord(from) + FaAfter, m => m.Groups[1].Value + to);
        }
        return output;
    }

    private static bool IsAsciiAlnum(char c) =>
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');

    private static bool IsDigitChar(char c) =>
        (c >= '0' && c <= '9') || (c >= '\u06F0' && c <= '\u06F9') || (c >= '\u0660' && c <= '\u0669');

    public static string FixPunctuationSpacing(string t)
    {
        var output = t;
        output = PunctSpace1Re.Replace(output, "$1");
        output = PunctSpace2Re.Replace(output, "$1");
        var input = output;
        output = PunctSpace3Re.Replace(input, m =>
        {
            var index = m.Index;
            var next = m.Groups[2].Value[0];
            var prevAlnum = index > 0 && IsAsciiAlnum(input[index - 1]);
            if (prevAlnum && IsAsciiAlnum(next))
                return m.Value;
            return m.Groups[1].Value + " " + m.Groups[2].Value;
        });
        return output;
    }

    public static string FixPersianPunct(string t)
    {
        var output = t;
        output = EllipsisRe.Replace(output, "…");
        output = FaPunct1Re.Replace(output, "$1$2");
        var input = output;
        output = FaPunctCommaRe.Replace(input, m =>
        {
            if (m.Value == ",")
            {
                var before = m.Index > 0 ? input[m.Index - 1] : '\0';
                var after = m.Index + 1 < input.Length ? input[m.Index + 1] : '\0';
                if (IsDigitChar(before) && IsDigitChar(after))
                    return m.Value;
            }
            return new string('،', m.Value.Length);
        });
        output = FaPunctSemiRe.Replace(output, m => new string('؛', m.Value.Length));
        output = FaPunctQuestionRe.Replace(output, m => new string('؟', m.Value.Length));
        output = PercentRe.Replace(output, "٪");
        return output;
    }

    public static string FixDoubleSpace(string t)
    {
        var output = DoubleSpaceRe.Replace(t, " ");
        output = TripleNewlineRe.Replace(output, "\n\n");
        return output;
    }

    private static readonly char[] PersianDigits = { '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' };

    public static string FixLatinDigits(string t) =>
        Regex.Replace(t, "[0-9]", m => PersianDigits[m.Value[0] - '0'].ToString());

    public static string FixSeparators(string t)
    {
        var output = SeparatorCommaRe.Replace(t, "$1٬$2");
        output = SeparatorPointRe.Replace(output, "$1٫$2");
        return output;
    }

    public static string FixRepeat(string t) => RepeatRe.Replace(t, "$1");

    public static string FixQuotes(string t) => QuotesRe.Replace(t, "«$1»");

    public static string FixChars(string t, FixOptions opts)
    {
        var output = t;
        if (opts.FixArabicYK)
            output = FixArabicYK(output);
        if (opts.FixArabicAlef)
            output = FixArabicAlef(output);
        if (opts.FixArabicDigits)
            output = FixArabicDigits(output);
        if (opts.FixLatinDigits)
            output = FixLatinDigits(output);
        return output;
    }

    public static string FixWords(string t, FixOptions opts)
    {
        var output = t;
        if (opts.FixKashida)
            output = FixKashida(output);
        if (opts.FixTashkeel)
            output = FixTashkeel(output);
        output = NormalizeZWNJSpaces(output);
        output = FixCustomWords(output, opts.CustomWords);
        if (opts.FixZWNJ)
            output = FixZWNJ(output);
        if (opts.FixCompounds)
            output = FixCompounds(output);
        if (opts.FixZWNJ)
            output = FixZWNJ(output);
        if (opts.FixAttachedHa)
            output = FixAttachedHa(output);
        if (opts.FixCommonTypos)
            output = FixCommonTypos(output);
        if (opts.FixCommonTypos)
            output = FixSubs(output);
        if (opts.FixCompounds)
            output = FixStems(output);
        if (opts.FixCompounds)
            output = FixAttachedWords(output);
        if (opts.FixZWNJ)
            output = FixZWNJ(output);
        if (opts.FixSeparators)
            output = FixSeparators(output);
        if (opts.FixPersianPunct)
            output = FixPersianPunct(output);
        if (opts.FixPunctuation)
            output = FixPunctuationSpacing(output);
        if (opts.FixDoubleSpace)
            output = FixDoubleSpace(output);
        if (opts.FixPunctuation && opts.FixDoubleSpace)
            output = FaSpace2Re.Replace(output, " ");
        if (opts.FixRepeat)
            output = FixRepeat(output);
        if (opts.FixQuotes)
            output = FixQuotes(output);
        return output;
    }

    public static (string Closed, string Open) SplitStable(string before)
    {
        var m = StableSplitRe.Match(before);
        if (m.Success && FaOnlyRe.IsMatch(m.Groups[2].Value))
            return (m.Groups[1].Value, m.Groups[2].Value);
        if (FaOnlyRe.IsMatch(before))
            return ("", before);
        return (before, "");
    }

    public static string FixOpenWord(string openText, FixOptions opts)
    {
        if (string.IsNullOrEmpty(openText))
            return openText;
        var output = openText;
        output = FixCustomWords(output, opts.CustomWords);
        if (opts.FixZWNJ && !HitsSet(output, Dictionaries.MiAttachedExceptions) && !HitsSet(output, Dictionaries.NemiAttachedExceptions))
            output = FixAttachedMi(output);
        if (opts.FixTashkeel)
            output = FixTashkeel(output);
        if (opts.FixKashida)
            output = FixKashida(output);
        if (opts.FixAttachedHa)
            output = FixAttachedHa(output);
        if (opts.FixCompounds)
        {
            output = FixCompounds(output);
            output = FixStems(output);
            output = FixAttachedWords(output);
        }
        if (opts.FixCommonTypos)
            output = FixCommonTypos(output);
        if (opts.FixRepeat)
            output = FixRepeat(output);
        return output;
    }

    private static string IndexToLetters(int index)
    {
        var s = "";
        var i = index + 1;
        while (i > 0)
        {
            var m = (i - 1) % 26;
            s = (char)('A' + m) + s;
            i = (i - 1) / 26;
        }
        return s;
    }

    private static int LettersToIndex(string s)
    {
        var index = 0;
        foreach (var ch in s)
            index = index * 26 + (ch - 64);
        return index - 1;
    }

    public static string FixText(string text, FixOptions? opts = null)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        if (!ArabicAnyRe.IsMatch(text))
            return text;
        opts ??= new FixOptions();

        var placeholders = new List<string>();
        var output = UrlRe.Replace(text, m =>
        {
            placeholders.Add(m.Value);
            return "\uE000" + IndexToLetters(placeholders.Count - 1) + "\uE001";
        });
        output = FixChars(output, opts);
        output = FixWords(output, opts);
        output = PlaceholderRe.Replace(output, m =>
        {
            var idx = LettersToIndex(m.Groups[1].Value);
            return idx >= 0 && idx < placeholders.Count ? placeholders[idx] : m.Value;
        });
        return output;
    }

    public static TypingResult FixTypingValue(string value, int? cursor, FixOptions? opts = null)
    {
        opts ??= new FixOptions();
        if (string.IsNullOrEmpty(value))
            return new TypingResult { Text = value, Cursor = cursor ?? 0 };
        var mapped = FixChars(value, opts);
        var c = Math.Max(0, Math.Min(cursor ?? mapped.Length, mapped.Length));
        var before = mapped[..c];
        var after = mapped[c..];
        var (closed, open) = SplitStable(before);
        var fixedClosed = FixWords(closed, opts);
        var fixedOpen = FixOpenWord(open, opts);
        return new TypingResult
        {
            Text = fixedClosed + fixedOpen + after,
            Cursor = fixedClosed.Length + fixedOpen.Length,
        };
    }
}
