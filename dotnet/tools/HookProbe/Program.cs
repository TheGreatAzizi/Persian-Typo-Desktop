using System.Text;
using PersianTypoFixer.Core;
using PersianTypoFixer.Core.Platform;

var path = args.Length > 0 ? args[0] : "hookprobe.txt";
using var writer = new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = true };
var hook = new WindowsKeyboardHook(
    ev => writer.WriteLine("P " + (ev.Name ?? ev.Char?.ToString() ?? "")),
    ev => writer.WriteLine("R " + (ev.Name ?? ev.Char?.ToString() ?? "")),
    () => false);
hook.Start();
writer.WriteLine("STARTED");
Thread.Sleep(TimeSpan.FromSeconds(20));
hook.Stop();
writer.WriteLine("STOPPED");
