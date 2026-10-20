using System.Text;
using System.Text.Json;
using PersianTypoFixer.Core;

if (args.Length >= 2 && args[0] == "--debug")
{
    var dbgText = args[1];
    string Hex(string s) => string.Join(",", s.Select(c => ((int)c).ToString("X4")));
    void Dump(string label, string value) => Console.WriteLine($"{label}=[{Esc(value)}] hex={Hex(value)}");

    Dump("in", dbgText);
    var dbg = dbgText;
    dbg = Fixer.FixArabicYK(dbg); Dump("arabicYK", dbg);
    dbg = Fixer.FixArabicAlef(dbg); Dump("arabicAlef", dbg);
    dbg = Fixer.NormalizeZWNJSpaces(dbg); Dump("norm", dbg);

    var field = typeof(Fixer).GetField("ZwnjBaseSuffixRe", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    var re = (System.Text.RegularExpressions.Regex)field.GetValue(null);
    var m = re.Match(dbg);
    Console.WriteLine($"baseSuffix success={m.Success} index={m.Index} value=[{Esc(m.Value)}] g1=[{Esc(m.Groups[1].Value)}] g2=[{Esc(m.Groups[2].Value)}]");
    dbg = Fixer.FixZWNJ(dbg); Dump("zwnj", dbg);
    return 0;
}

var root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
var tools = Path.Combine(root, "tools");
var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

int total = 0;
int fails = 0;

string Esc(string s)
{
    if (s == null)
        return "<null>";
    var sb = new StringBuilder();
    foreach (var ch in s)
    {
        if (ch >= 0x20 && ch <= 0x7E)
            sb.Append(ch);
        else
            sb.Append("\\u").Append(((int)ch).ToString("X4"));
    }
    return sb.ToString();
}

void Check(string label, string input, string expected, string actual, string extra = "")
{
    total++;
    if (expected == actual)
        return;
    fails++;
    if (fails <= 25)
        Console.WriteLine($"FAIL {label} {extra} in=[{Esc(input)}] expected=[{Esc(expected)}] actual=[{Esc(actual)}]");
}

T Load<T>(string path) =>
    JsonSerializer.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8), jsonOptions);

// 1) corpus (default options)
var py = Load<PyResults>(Path.Combine(tools, "py_results.json"));
var def = new FixOptions();
foreach (var c in py.Texts)
    Check("corpus", c.In, c.Out, Fixer.FixText(c.In, def));

foreach (var c in py.Typing)
{
    var r = Fixer.FixTypingValue(c.In, c.Cursor, def);
    Check("corpus-typing", c.In, c.Out, r.Text, $"cursor={c.Cursor}");
    total++;
    if (r.Cursor != c.OutCursor)
    {
        fails++;
        if (fails <= 25)
            Console.WriteLine($"FAIL corpus-typing-cursor in=[{Esc(c.In)}] expected={c.OutCursor} actual={r.Cursor}");
    }
}

// 2) corpus2 (option sets)
var corpus2 = Load<Corpus2>(Path.Combine(tools, "corpus2.json"));
var py2 = Load<List<OptCase>>(Path.Combine(tools, "py_results2.json"));
foreach (var c in py2)
{
    var opts = c.OptIndex >= 0 && c.OptIndex < corpus2.Options.Count
        ? corpus2.Options[c.OptIndex]
        : new FixOptions();
    Check("corpus2-" + c.OptIndex, c.In, c.Out, Fixer.FixText(c.In, opts));
}

// 3) fuzz1
var f1 = Load<Fuzz1>(Path.Combine(tools, "fuzz_js_results.json"));
for (var i = 0; i < f1.Cases.Count; i++)
    Check("fuzz1", f1.Cases[i], f1.Results[i], Fixer.FixText(f1.Cases[i], def));

// 4) fuzz2 (text + typing)
var f2 = Load<Fuzz2>(Path.Combine(tools, "fuzz2_js.json"));
for (var i = 0; i < f2.TextCases.Count; i++)
    Check("fuzz2-text", f2.TextCases[i], f2.TextResults[i], Fixer.FixText(f2.TextCases[i], def));
for (var i = 0; i < f2.TypingCases.Count; i++)
{
    var r = Fixer.FixTypingValue(f2.TypingCases[i].Value, f2.TypingCases[i].Cursor, def);
    Check("fuzz2-typing", f2.TypingCases[i].Value, f2.TypingResults[i].Text, r.Text, $"cursor={f2.TypingCases[i].Cursor}");
    total++;
    if (r.Cursor != f2.TypingResults[i].Cursor)
    {
        fails++;
        if (fails <= 25)
            Console.WriteLine($"FAIL fuzz2-typing-cursor in=[{Esc(f2.TypingCases[i].Value)}] expected={f2.TypingResults[i].Cursor} actual={r.Cursor}");
    }
}

// 5) fuzz3 (random option sets)
var f3 = Load<Fuzz3>(Path.Combine(tools, "fuzz3_js.json"));
for (var i = 0; i < f3.Cases.Count; i++)
    Check("fuzz3", f3.Cases[i].Value, f3.Results[i], Fixer.FixText(f3.Cases[i].Value, f3.Cases[i].Opts));

Console.WriteLine($"TOTAL={total} FAILS={fails}");
return fails == 0 ? 0 : 1;

internal sealed class InOut
{
    public string In { get; set; }
    public string Out { get; set; }
}

internal sealed class TypingCase
{
    public string In { get; set; }
    public int Cursor { get; set; }
    public string Out { get; set; }
    public int OutCursor { get; set; }
}

internal sealed class PyResults
{
    public List<InOut> Texts { get; set; }
    public List<TypingCase> Typing { get; set; }
}

internal sealed class Corpus2
{
    public List<string> Texts { get; set; }
    public List<FixOptions> Options { get; set; }
}

internal sealed class OptCase
{
    public int OptIndex { get; set; }
    public string In { get; set; }
    public string Out { get; set; }
}

internal sealed class Fuzz1
{
    public List<string> Cases { get; set; }
    public List<string> Results { get; set; }
}

internal sealed class TypingIn
{
    public string Value { get; set; }
    public int Cursor { get; set; }
}

internal sealed class TypingOut
{
    public string Text { get; set; }
    public int Cursor { get; set; }
}

internal sealed class Fuzz2
{
    public List<string> TextCases { get; set; }
    public List<string> TextResults { get; set; }
    public List<TypingIn> TypingCases { get; set; }
    public List<TypingOut> TypingResults { get; set; }
}

internal sealed class Fuzz3Case
{
    public string Value { get; set; }
    public FixOptions Opts { get; set; }
}

internal sealed class Fuzz3
{
    public List<Fuzz3Case> Cases { get; set; }
    public List<string> Results { get; set; }
}
