import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))
from core.fixer import (
    PTF_COMPOUNDS, PTF_COMMON_TYPOS, PTF_STEM_FIXES, PTF_SUB_FIXES,
    PTF_ATTACHED_FIXES, ATTACHED_HA_EXCEPTIONS, MI_ATTACHED_EXCEPTIONS,
    NEMI_ATTACHED_EXCEPTIONS,
)

with open(os.path.join(os.path.dirname(__file__), 'js_dicts.json'), encoding='utf-8') as f:
    js = json.load(f)

py = {
    'PTF_COMPOUNDS': [list(x) for x in PTF_COMPOUNDS],
    'PTF_COMMON_TYPOS': [list(x) for x in PTF_COMMON_TYPOS],
    'PTF_STEM_FIXES': [list(x) for x in PTF_STEM_FIXES],
    'PTF_SUB_FIXES': [list(x) for x in PTF_SUB_FIXES],
    'PTF_ATTACHED_FIXES': [list(x) for x in PTF_ATTACHED_FIXES],
    'ATTACHED_HA_EXCEPTIONS': sorted(ATTACHED_HA_EXCEPTIONS),
    'MI_ATTACHED_EXCEPTIONS': sorted(MI_ATTACHED_EXCEPTIONS),
    'NEMI_ATTACHED_EXCEPTIONS': sorted(NEMI_ATTACHED_EXCEPTIONS),
}

lines = []

for key in ['PTF_COMPOUNDS', 'PTF_COMMON_TYPOS', 'PTF_STEM_FIXES', 'PTF_SUB_FIXES', 'PTF_ATTACHED_FIXES']:
    jset = {tuple(x) for x in js[key]}
    pset = {tuple(x) for x in py[key]}
    missing = [x for x in js[key] if tuple(x) not in pset]
    extra = [x for x in py[key] if tuple(x) not in jset]
    lines.append(f'== {key}: js={len(jset)} py={len(pset)} missing={len(missing)} extra={len(extra)}')
    for m in missing:
        lines.append(f'  MISSING: {repr(m[0])} -> {repr(m[1])}')
    for e in extra:
        lines.append(f'  EXTRA:   {repr(e[0])} -> {repr(e[1])}')

for key in ['ATTACHED_HA_EXCEPTIONS', 'MI_ATTACHED_EXCEPTIONS', 'NEMI_ATTACHED_EXCEPTIONS']:
    jset = set(js[key])
    pset = set(py[key])
    lines.append(f'== {key}: js={len(jset)} py={len(pset)} missing={sorted(jset - pset)} extra={sorted(pset - jset)}')

# non-Persian / suspicious characters in python dicts
def suspicious(s):
    bad = []
    for ch in s:
        o = ord(ch)
        if ch in '\u200c':
            continue
        if 'A-Za-z0-9'.find(ch) >= 0:
            bad.append(('LATIN', ch))
        elif 0x0590 <= o <= 0x05FF:
            bad.append(('HEBREW', ch))
        elif 0x0400 <= o <= 0x04FF:
            bad.append(('CYRILLIC', ch))
        elif 0xAC00 <= o <= 0xD7AF:
            bad.append(('KOREAN', ch))
    return bad

lines.append('== SUSPICIOUS CHARS IN PYTHON DICTS ==')
for key in ['PTF_COMPOUNDS', 'PTF_COMMON_TYPOS', 'PTF_STEM_FIXES', 'PTF_SUB_FIXES', 'PTF_ATTACHED_FIXES']:
    for pair in py[key]:
        for item in pair:
            s = suspicious(item)
            if s:
                lines.append(f'  {key}: {repr(pair)} -> {s}')
for key in ['ATTACHED_HA_EXCEPTIONS', 'MI_ATTACHED_EXCEPTIONS', 'NEMI_ATTACHED_EXCEPTIONS']:
    for item in py[key]:
        s = suspicious(item)
        if s:
            lines.append(f'  {key}: {repr(item)} -> {s}')

out = '\n'.join(lines)
with open(os.path.join(os.path.dirname(__file__), 'dict_diff.txt'), 'w', encoding='utf-8') as f:
    f.write(out)
print(out)
