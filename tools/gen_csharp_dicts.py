# Generates Dictionaries.cs from the Python fixer data (single source for porting)
import io
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import core.fixer as f


def esc(s):
    out = []
    for ch in s:
        o = ord(ch)
        if ch == "\\":
            out.append("\\\\")
        elif ch == '"':
            out.append('\\"')
        elif 0x20 <= o <= 0x7E:
            out.append(ch)
        else:
            out.append("\\u%04X" % o)
    return '"' + "".join(out) + '"'


pairs = [
    ("Compounds", f.PTF_COMPOUNDS),
    ("CommonTypos", f.PTF_COMMON_TYPOS),
    ("StemFixes", f.PTF_STEM_FIXES),
    ("SubFixes", f.PTF_SUB_FIXES),
    ("AttachedFixes", f.PTF_ATTACHED_FIXES),
]

sets = [
    ("AttachedHaExceptions", f.ATTACHED_HA_EXCEPTIONS),
    ("MiAttachedExceptions", f.MI_ATTACHED_EXCEPTIONS),
    ("NemiAttachedExceptions", f.NEMI_ATTACHED_EXCEPTIONS),
    ("NoticeGotoSuppress", set(f.PTF_NOTICE_GOTO_SUPPRESS)),
]

lines = [
    "// Auto-generated from core/fixer.py dictionaries. Do not edit by hand.",
    "using System.Collections.Generic;",
    "",
    "namespace PersianTypoFixer.Core;",
    "",
    "public static class Dictionaries",
    "{",
]

for name, data in pairs:
    lines.append("    public static readonly (string From, string To)[] %s = new[]" % name)
    lines.append("    {")
    for a, b in data:
        lines.append("        (%s, %s)," % (esc(a), esc(b)))
    lines.append("    };")
    lines.append("")

for name, data in sets:
    lines.append("    public static readonly HashSet<string> %s = new()" % name)
    lines.append("    {")
    for item in sorted(data):
        lines.append("        %s," % esc(item))
    lines.append("    };")
    lines.append("")

lines.append("}")
lines.append("")

target = os.path.join(
    os.path.dirname(__file__),
    "..",
    "dotnet",
    "src",
    "PersianTypoFixer.Core",
    "Dictionaries.cs",
)
with io.open(target, "w", encoding="utf-8", newline="\n") as fh:
    fh.write("\n".join(lines))

print("written", target)
print("compounds", len(f.PTF_COMPOUNDS), "typos", len(f.PTF_COMMON_TYPOS))
