import os
import sys
import types

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

fake = types.ModuleType('pyautogui')
fake.FAILSAFE = True
fake.PAUSE = 0.1
fake.hotkey = lambda *a, **k: None
sys.modules['pyautogui'] = fake

import pyperclip
from core import clipboard

fails = []


def check(name, cond, info=''):
    if cond:
        print('PASS:', name)
    else:
        fails.append(name)
        print('FAIL:', name, info)


def scenario(name, copy_text, expect_change):
    pyperclip.copy('CLIPBOARD-BACKUP')
    calls = {'n': 0}

    def fake_copy_key():
        calls['n'] += 1
        if copy_text is not None:
            pyperclip.copy(copy_text)

    orig_copy = clipboard._copy_key
    orig_paste = clipboard._paste_key
    pasted = {'text': None, 'count': 0}

    def fake_paste_key():
        pasted['text'] = pyperclip.paste()
        pasted['count'] += 1

    clipboard._copy_key = fake_copy_key
    clipboard._paste_key = fake_paste_key
    try:
        result = clipboard.fix_selection()
    finally:
        clipboard._copy_key = orig_copy
        clipboard._paste_key = orig_paste

    if expect_change:
        check(name + ' -> returned fixed text', bool(result), repr(result))
        check(name + ' -> pasted once', pasted['count'] == 1, str(pasted))
        check(name + ' -> pasted content == result', pasted['text'] == result, repr(pasted['text']))
    else:
        check(name + ' -> returned empty', result == '', repr(result))
        check(name + ' -> did not paste', pasted['count'] == 0, str(pasted))
        check(name + ' -> clipboard restored', pyperclip.paste() == 'CLIPBOARD-BACKUP', repr(pyperclip.paste()))


scenario('typo text', 'این یه متن غلط ازمایشی میخواستم', True)
scenario('correct text', 'سلام خوبی', False)
scenario('no selection', None, False)

print()
print('RESULT:', 'ALL PASS' if not fails else 'FAILURES: %d' % len(fails))
sys.exit(1 if fails else 0)
