import io
import os

path = os.path.join(os.path.dirname(__file__), '..', 'core', 'fixer.py')
log = []
ZWNJ = '\u200c'

with io.open(path, encoding='utf-8') as f:
    lines = f.readlines()

HEBREW_TO_PERSIAN = {
    '\u05dc': 'ل',  # lamed -> lam
    '\u05e8': 'ر',  # resh -> reh
    '\u05d6': 'ز',  # zayin -> zeh
    '\u05d4': 'ه',  # he -> heh
}

fixed = []
for line in lines:
    orig = line
    # 1) Hebrew letters inside dictionary lines
    for hb, fa in HEBREW_TO_PERSIAN.items():
        if hb in line:
            line = line.replace(hb, fa)
    # 2) Latin "tor" (from bad port) -> Persian طور only when preceded by ZWNJ
    if ZWNJ + 'tor"' in line:
        line = line.replace(ZWNJ + 'tor"', ZWNJ + 'طور"')
    # 3) بین الملل value typo
    if '"بین الملل"' in line and ZWNJ + 'المل"]' in line:
        line = line.replace(ZWNJ + 'المل"]', ZWNJ + 'الملل"]')
    # 4) invented entry به لحظه -> original به هنگام/بهنگام
    if '"به لحظه"' in line:
        line = line.replace('"به لحظه", "به' + ZWNJ + 'لحظه"', '"به هنگام", "بهنگام"')
    # 5) به کندی value typo
    if '"به کندی"' in line and ZWNJ + 'کنند"]' in line:
        line = line.replace(ZWNJ + 'کنند"]', ZWNJ + 'کندی"]')
    if line != orig:
        log.append('FIXED: ' + repr(orig.strip()) + '  =>  ' + repr(line.strip()))
    fixed.append(line)

with io.open(path, 'w', encoding='utf-8') as f:
    f.writelines(fixed)

with io.open(os.path.join(os.path.dirname(__file__), 'fix_log.txt'), 'w', encoding='utf-8') as f:
    f.write('\n'.join(log) + '\n\ntotal fixed lines: %d\n' % len(log))
print('done, fixed lines:', len(log))
