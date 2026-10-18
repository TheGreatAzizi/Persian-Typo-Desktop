import io
import os

base = os.path.join(os.path.dirname(__file__), '..')
files = []
for root, dirs, fnames in os.walk(base):
    dirs[:] = [d for d in dirs if d not in ('.git', '__pycache__', 'tools')]
    for fn in fnames:
        if fn.endswith(('.py', '.md', '.yml', '.sh', '.txt')):
            files.append(os.path.join(root, fn))

out = []
for fp in files:
    try:
        with io.open(fp, encoding='utf-8') as f:
            lines = f.readlines()
    except Exception as e:
        out.append('%s: READ ERROR %r' % (fp, e))
        continue
    for i, line in enumerate(lines, 1):
        for ch in line:
            o = ord(ch)
            name = None
            if 0xAC00 <= o <= 0xD7AF:
                name = 'KOREAN'
            elif 0x0590 <= o <= 0x05FF:
                name = 'HEBREW'
            elif 0x0400 <= o <= 0x04FF:
                name = 'CYRILLIC'
            elif 0x0600 <= o <= 0x06FF and ch in '\u060C\u061B\u061F':
                name = None
            if name:
                out.append('%s:%d: %s %r in: %r' % (os.path.relpath(fp, base), i, name, ch, line.strip()[:120]))

# French-accented letters often left from other languages inside Persian text
for fp in files:
    try:
        with io.open(fp, encoding='utf-8') as f:
            lines = f.readlines()
    except Exception:
        continue
    for i, line in enumerate(lines, 1):
        if any(c in line for c in '\u00e8\u00e9\u00ea\u00eb\u00e0\u00e7'):
            out.append('%s:%d: FRENCH-ACCENT in: %r' % (os.path.relpath(fp, base), i, line.strip()[:160]))

with io.open(os.path.join(os.path.dirname(__file__), 'suspicious.txt'), 'w', encoding='utf-8') as f:
    f.write('\n'.join(out) if out else 'NONE FOUND\n')
print('scanned files:', len(files), 'findings:', len(out))
