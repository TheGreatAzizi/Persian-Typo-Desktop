# Persian Typo Fixer Desktop | core/autofix.py
# اصلاح خودکار متن هنگام تایپ (بدون نیاز به میانبر)

import platform
import re
import threading
import time

from pynput import keyboard, mouse

from core.keyevent import KeyEvent

MAX_BUFFER = 120
DEBOUNCE = 0.45
BOUNDARY_RE = re.compile(r"[\s.,!?؟؛;:،…»)\]}\-–—]$")
TAIL_RE = re.compile(r"(?:[^\s]+\s+)?[^\s]+(?:\s+)?$")

_MODIFIERS = {
    keyboard.Key.ctrl: "ctrl",
    keyboard.Key.ctrl_l: "ctrl",
    keyboard.Key.ctrl_r: "ctrl",
    keyboard.Key.alt: "alt",
    keyboard.Key.alt_l: "alt",
    keyboard.Key.alt_r: "alt",
    keyboard.Key.alt_gr: "alt",
    keyboard.Key.cmd: "cmd",
    keyboard.Key.cmd_l: "cmd",
    keyboard.Key.cmd_r: "cmd",
    keyboard.Key.shift: "shift",
    keyboard.Key.shift_l: "shift",
    keyboard.Key.shift_r: "shift",
}

_SPECIAL = {
    keyboard.Key.backspace: "backspace",
    keyboard.Key.left: "left",
    keyboard.Key.right: "right",
    keyboard.Key.up: "up",
    keyboard.Key.down: "down",
    keyboard.Key.home: "home",
    keyboard.Key.end: "end",
    keyboard.Key.page_up: "page_up",
    keyboard.Key.page_down: "page_down",
    keyboard.Key.delete: "delete",
    keyboard.Key.esc: "esc",
    keyboard.Key.tab: "tab",
    keyboard.Key.enter: "enter",
}

_RESET_KEYS = {"left", "right", "up", "down", "home", "end", "page_up", "page_down", "delete", "esc", "tab", "enter"}


def _from_pynput(key):
    name = _MODIFIERS.get(key) or _SPECIAL.get(key)
    if name is not None:
        return KeyEvent(name=name)
    return KeyEvent(char=getattr(key, "char", None))


class _PynputKeyboardHook:
    def __init__(self, on_press, on_release, should_ignore):
        self._listener = keyboard.Listener(
            on_press=lambda key: on_press(_from_pynput(key)),
            on_release=lambda key: on_release(_from_pynput(key)),
        )

    def start(self):
        self._listener.start()

    def stop(self):
        self._listener.stop()


class AutoFixer:
    def __init__(self, opts):
        self.opts = opts
        self._buffer = ""
        self._lock = threading.Lock()
        self._timer = None
        self._generation = 0
        self._injecting = False
        self._ctrl = False
        self._alt = False
        self._cmd = False
        self._kb_listener = None
        self._mouse_listener = None

    def start(self):
        if self._kb_listener is not None:
            return
        if platform.system() == "Windows":
            from core.winkb import WindowsKeyboardHook

            self._kb_listener = WindowsKeyboardHook(
                self._on_press,
                self._on_release,
                should_ignore=lambda: self._injecting,
            )
        else:
            self._kb_listener = _PynputKeyboardHook(
                self._on_press,
                self._on_release,
                should_ignore=lambda: self._injecting,
            )
        self._kb_listener.start()
        self._mouse_listener = mouse.Listener(on_click=lambda *a: self._reset())
        self._mouse_listener.start()

    def stop(self):
        for listener in (self._kb_listener, self._mouse_listener):
            if listener is not None:
                try:
                    listener.stop()
                except Exception:
                    pass
        self._kb_listener = None
        self._mouse_listener = None
        self._reset()

    def _on_release(self, ev):
        if ev.name == "ctrl":
            self._ctrl = False
        elif ev.name == "alt":
            self._alt = False
        elif ev.name == "cmd":
            self._cmd = False

    def _on_press(self, ev):
        if self._injecting:
            return
        name = ev.name
        if name == "ctrl":
            self._ctrl = True
            self._reset()
            return
        if name == "alt":
            self._alt = True
            self._reset()
            return
        if name == "cmd":
            self._cmd = True
            self._reset()
            return
        if self._ctrl or self._alt or self._cmd:
            return
        if name == "shift":
            return
        if name == "backspace":
            with self._lock:
                self._buffer = self._buffer[:-1]
                self._generation += 1
            self._cancel()
            return
        if name in _RESET_KEYS:
            self._reset()
            return
        ch = ev.char
        if not ch:
            return
        with self._lock:
            self._buffer = (self._buffer + ch)[-MAX_BUFFER:]
            self._generation += 1
            gen = self._generation
        self._schedule(gen)

    def _schedule(self, gen):
        self._cancel()
        timer = threading.Timer(DEBOUNCE, self._maybe_fix, args=(gen,))
        timer.daemon = True
        with self._lock:
            self._timer = timer
        timer.start()

    def _cancel(self):
        with self._lock:
            timer = self._timer
            self._timer = None
        if timer is not None:
            timer.cancel()

    def _reset(self):
        self._cancel()
        with self._lock:
            self._buffer = ""
            self._generation += 1

    def _maybe_fix(self, gen):
        if not self.opts.get("autoFix", True) or not self.opts.get("enabled", True):
            return
        with self._lock:
            if gen != self._generation:
                return
            text = self._buffer
            self._timer = None
        if not text.strip() or not BOUNDARY_RE.search(text):
            return
        match = TAIL_RE.search(text)
        snippet = match.group(0) if match else text
        try:
            from core.fixer import ptfFixText
            fixed = ptfFixText(snippet, self.opts)
        except Exception:
            return
        if fixed == snippet:
            return
        self._replace(snippet, fixed)
        with self._lock:
            if gen == self._generation:
                self._buffer = (text[:-len(snippet)] + fixed)[-MAX_BUFFER:]
                self._generation += 1

    def _replace(self, text, fixed):
        self._injecting = True
        try:
            try:
                from core.clipboard import _paste_key, get_clipboard, press_backspace, set_clipboard

                press_backspace(len(text))
                backup = get_clipboard()
                set_clipboard(fixed)
                time.sleep(0.03)
                _paste_key()
                threading.Timer(0.6, lambda: set_clipboard(backup)).start()
            except Exception:
                controller = keyboard.Controller()
                for _ in range(len(text)):
                    controller.press(keyboard.Key.backspace)
                    controller.release(keyboard.Key.backspace)
                controller.type(fixed)
            time.sleep(0.2)
        finally:
            self._injecting = False
