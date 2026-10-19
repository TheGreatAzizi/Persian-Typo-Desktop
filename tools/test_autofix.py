import os
import re
import sys
import time
import types
from types import SimpleNamespace

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

Key = SimpleNamespace(
    ctrl='CTRL', ctrl_l='CTRL_L', ctrl_r='CTRL_R',
    alt='ALT', alt_l='ALT_L', alt_r='ALT_R', alt_gr='ALT_GR',
    cmd='CMD', cmd_l='CMD_L', cmd_r='CMD_R',
    shift='SHIFT', shift_l='SHIFT_L', shift_r='SHIFT_R',
    backspace='BACKSPACE', left='LEFT', right='RIGHT', up='UP', down='DOWN',
    home='HOME', end='END', page_up='PAGE_UP', page_down='PAGE_DOWN',
    delete='DELETE', esc='ESC', tab='TAB', enter='ENTER',
)


class FakeListener:
    def __init__(self, *a, **k):
        pass

    def start(self):
        pass

    def stop(self):
        pass


class FakeController:
    instances = []

    def __init__(self):
        self.presses = []
        self.typed = []
        FakeController.instances.append(self)

    def press(self, key):
        self.presses.append(('press', key))

    def release(self, key):
        self.presses.append(('release', key))

    def type(self, text):
        self.typed.append(text)


fake_kb = types.ModuleType('pynput.keyboard')
fake_kb.Key = Key
fake_kb.Listener = FakeListener
fake_kb.Controller = FakeController

fake_mouse = types.ModuleType('pynput.mouse')
fake_mouse.Listener = FakeListener

fake_pynput = types.ModuleType('pynput')
fake_pynput.keyboard = fake_kb
fake_pynput.mouse = fake_mouse
sys.modules['pynput'] = fake_pynput
sys.modules['pynput.keyboard'] = fake_kb
sys.modules['pynput.mouse'] = fake_mouse

clip_events = []
fake_clip = types.ModuleType('core.clipboard')
fake_clip.get_clipboard = lambda: 'BACKUP'
fake_clip.set_clipboard = lambda text: clip_events.append(('set', text))
fake_clip.press_backspace = lambda count: clip_events.append(('backspace', count))
fake_clip._paste_key = lambda: clip_events.append(('paste', None))
sys.modules['core.clipboard'] = fake_clip

from core.autofix import AutoFixer
from core.fixer import PTF_DEFAULTS, ptfFixText
from core.keyevent import KeyEvent

fails = []


def check(name, cond, info=''):
    if cond:
        print('PASS:', name)
    else:
        fails.append(name)
        print('FAIL:', name, ascii(info))


def run(typed, opts=None):
    o = dict(PTF_DEFAULTS)
    o['autoFix'] = True
    o['enabled'] = True
    if opts:
        o.update(opts)
    fx = AutoFixer(o)
    calls = []
    fx._replace = lambda text, fixed: calls.append((text, fixed))
    tokens = re.findall(r'<BS>|<LEFT>|<CLICK>|\n|.', typed)
    for ch in tokens:
        if ch == '<BS>':
            fx._on_press(KeyEvent(name='backspace'))
        elif ch == '<LEFT>':
            fx._on_press(KeyEvent(name='left'))
        elif ch == '<CLICK>':
            fx._reset()
        elif ch == '\n':
            fx._on_press(KeyEvent(name='enter'))
        else:
            fx._on_press(KeyEvent(char=ch))
        fx._cancel()
    fx._maybe_fix(fx._generation)
    return calls, fx


calls, _ = run('میخواستم ')
check('attached mi + space', calls == [('میخواستم ', ptfFixText('میخواستم ', PTF_DEFAULTS))], calls)

calls, _ = run('کتاب خانه ')
check('compound two words', calls == [('کتاب خانه ', 'کتابخانه ')], calls)

calls, _ = run('علي ')
check('arabic yeh', calls == [('علي ', 'علی ')], calls)

calls, _ = run('تاثیر ')
check('common typo', calls == [('تاثیر ', 'تأثیر ')], calls)

calls, _ = run('میخوا')
check('mid-word no boundary -> no fix', calls == [], calls)

calls, _ = run('سلام خوب')
check('two words no trailing boundary -> no fix', calls == [], calls)

calls, _ = run('این ')
check('single short word -> no fix', calls == [], calls)

calls, _ = run('میخواستم <BS><BS><BS><BS>م ')
check('backspace tracking', calls == [('میخوام ', ptfFixText('میخوام ', PTF_DEFAULTS))], calls)

calls, _ = run('میخواستم <CLICK>')
check('mouse click resets buffer', calls == [], calls)

calls, _ = run('میخواستم\n')
check('enter resets buffer', calls == [], calls)

calls, _ = run('میخواستم ', opts={'autoFix': False})
check('autoFix off -> no fix', calls == [], calls)

calls, _ = run('میخواستم ', opts={'enabled': False})
check('app disabled -> no fix', calls == [], calls)

calls, _ = run('سلام دنیا ')
check('already correct -> no fix', calls == [], calls)

fx = AutoFixer(dict(PTF_DEFAULTS, autoFix=True, enabled=True))
injection = []
fx._replace = lambda t, f: injection.append((t, f))
for ch in 'کتاب خانه ':
    fx._on_press(KeyEvent(char=ch))
fx._cancel()
gen = fx._generation
fx._maybe_fix(gen)
check('buffer updated after replace', fx._buffer == 'کتابخانه ', repr(fx._buffer))

fx._on_press(KeyEvent(char='خ'))
check('new char appends to fixed buffer', fx._buffer == 'کتابخانه خ'[-120:], repr(fx._buffer))

FakeController.instances.clear()
clip_events.clear()
fx = AutoFixer(dict(PTF_DEFAULTS, autoFix=True, enabled=True))
fx._replace('abc ', 'abd ')
check('replace: one backspace per char', ('backspace', 4) in clip_events, clip_events)
check('replace: fixed text set on clipboard', ('set', 'abd ') in clip_events, clip_events)
check('replace: paste shortcut sent', ('paste', None) in clip_events, clip_events)
time.sleep(0.7)
check('replace: clipboard restored', ('set', 'BACKUP') in clip_events, clip_events)

print()
print('RESULT:', 'ALL PASS' if not fails else 'FAILURES: %d' % len(fails))
sys.exit(1 if fails else 0)
