namespace PersianTypoFixer.Core;

public sealed class KeyEvent
{
    public char? Char { get; init; }
    public string? Name { get; init; }

    public static KeyEvent FromChar(char c) => new() { Char = c };
    public static KeyEvent FromName(string name) => new() { Name = name };

    public override string ToString() => Name != null ? $"<{Name}>" : Char?.ToString() ?? "";
}

public interface IInputBackend
{
    string GetClipboard();
    string? GetSelection();
    void SetClipboard(string text);
    void PressBackspace(int count);
    void Paste();
    void Copy();
}

public interface IKeyboardHook
{
    void Start();
    void Stop();
}
