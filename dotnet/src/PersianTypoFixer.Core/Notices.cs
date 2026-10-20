using System.Text.RegularExpressions;

namespace PersianTypoFixer.Core;

public sealed record SemanticNotice(string Id, string Word, string Message);

public static class Notices
{
    private const string FaLetters = "\\u0621-\\u064A\\u067E\\u0686\\u0698\\u06A9\\u06AF\\u06CC\\u06BE\\u06C0";
    private const string FaEnd = "(?![" + FaLetters + "\\u200C])";
    private const string FaPre = "(^|[\\s\\u200C\\(\\[\"'«.,،:؛!؟…?;\\)\\]»])";

    private static readonly string[] GozarSuppress = { "نماز", "خدمت", "خواب", "شکر", "سپاس" };

    private static readonly Regex PersianRe = new("[\\u0600-\\u06FF]", RegexOptions.Compiled);
    private static readonly Regex ZarRe = new(
        FaPre + @"((ن)?می\u200C?زار(م|ی|ه|یم|ید|ن)?)" + FaEnd, RegexOptions.Compiled);
    private static readonly Regex BzarRe = new(
        FaPre + @"(بزار(م|ی|ه|یم|ید|ن)?)" + FaEnd, RegexOptions.Compiled);
    private static readonly Regex GozarRe = new(
        FaPre + @"((ن)?می\u200C?گزار(م|ی|ه|یم|ید|ن)?)" + FaEnd, RegexOptions.Compiled);
    private static readonly Regex KhastRe = new(
        FaPre + @"((ن)?می\u200C?خاست(م|ی|ه|یم|ید|ن)?|خاست(ه|ی|یم|ید|ن|گاری?)?)" + FaEnd, RegexOptions.Compiled);
    private static readonly Regex HayatRe = new(
        FaPre + @"(حیات)" + FaEnd, RegexOptions.Compiled);

    public static List<SemanticNotice> Find(string? text)
    {
        var result = new List<SemanticNotice>();
        if (string.IsNullOrEmpty(text) || text.Length > 2000)
            return result;
        if (!PersianRe.IsMatch(text))
            return result;

        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Push(string id, string word, string message)
        {
            if (!seen.Add(id + ":" + word))
                return;
            result.Add(new SemanticNotice(id, word, message));
        }

        foreach (Match match in ZarRe.Matches(text))
        {
            var word = match.Groups[2].Value;
            Push("zar", word,
                $"«{word}» یعنی «زار زدن» (گریه و زاری). اگر منظورتان «گذاشتن» است، درست آن «می\u200Cگذارم» (رسمی) یا «می\u200Cذارم» (محاوره\u200Cای) است.");
        }

        foreach (Match match in BzarRe.Matches(text))
        {
            var word = match.Groups[2].Value;
            var suggest = word.Replace("بزار", "بذار");
            Push("bzar", word,
                $"«{word}» برای «گذاشتن» درست نیست؛ شکل محاوره\u200Cای آن «{suggest}» (رسمی: بگذار) است.");
        }

        foreach (Match match in GozarRe.Matches(text))
        {
            var word = match.Groups[2].Value;
            var beforeStart = Math.Max(0, match.Index - 16);
            var before = text.Substring(beforeStart, match.Index - beforeStart);
            if (GozarSuppress.Any(before.Contains))
                continue;
            Push("gozar", word,
                $"«{word}» از «گزاردن» است یعنی انجام دادن (مانند نماز گزاردن). اگر منظورتان «گذاشتن» است، بنویسید «می\u200Cگذارم» یا «می\u200Cذارم».");
        }

        foreach (Match match in KhastRe.Matches(text))
        {
            var word = match.Groups[2].Value;
            Push("khast", word,
                $"«{word}»؟ «خاستن» (بدون واو) یعنی برخاستن؛ اگر منظورتان «خواستن» (میل و اراده) است با «واو» بنویسید: «می\u200Cخواست».");
        }

        foreach (Match match in HayatRe.Matches(text))
        {
            var word = match.Groups[2].Value;
            Push("hayat", word,
                "«حیات» یعنی زندگی؛ اگر منظورتان محوطه خانه است درست آن «حیاط» است — مطمئنی منظورت همینه؟");
        }

        return result;
    }
}
