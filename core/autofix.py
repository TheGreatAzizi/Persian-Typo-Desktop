# Persian Typo Fixer Desktop | core/autofix.py
# اصلاح خودکار متن هنگام تایپ (بدون نیاز به میانبر)

import re
import threading

from pynput import keyboard, mouse

MAX_BUFFER = 120
DEBOUNCE = 0.45
BOUNDARY_RE = re.compile(r"[\s.,!?؟؛;:،…»)\]}\-–—]$")
LETTER_RE = re.compile(r"[A-Za-z0-9\u0600-\u06FF\u200C]")
TAIL_RE = re.compile(r"(?:[^\s]+\s+)?[^\s]+(?:\s+)?$")


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
        self._kb_listener = keyboard.Listener(
            on_press=self._on_press,
            on_release=self._on_release,
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

    def _on_release(self, key):
        if key in (keyboard.Key.ctrl, keyboard.Key.ctrl_l, keyboard.Key.ctrl_r):
            self._ctrl = False
        elif key in (keyboard.Key.alt, keyboard.Key.alt_l, keyboard.Key.alt_r, keyboard.Key.alt_gr):
            self._alt = False
        elif key in (keyboard.Key.cmd, keyboard.Key.cmd_l, keyboard.Key.cmd_r):
            self._cmd = False

    def _on_press(self, key):
        if self._injecting:
            return
        if key in (keyboard.Key.ctrl, keyboard.Key.ctrl_l, keyboard.Key.ctrl_r):
            self._ctrl = True
            self._reset()
            return
        if key in (keyboard.Key.alt, keyboard.Key.alt_l, keyboard.Key.alt_r, keyboard.Key.alt_gr):
            self._alt = True
            self._reset()
            return
        if key in (keyboard.Key.cmd, keyboard.Key.cmd_l, keyboard.Key.cmd_r):
            self._cmd = True
            self._reset()
            return
        if self._ctrl or self._alt or self._cmd:
            return
        if key in (keyboard.Key.shift, keyboard.Key.shift_l, keyboard.Key.shift_r):
            return
        if key == keyboard.Key.backspace:
            with self._lock:
                self._buffer = self._buffer[:-1]
                self._generation += 1
            self._cancel()
            return
        if key in (
            keyboard.Key.left, keyboard.Key.right, keyboard.Key.up, keyboard.Key.down,
            keyboard.Key.home, keyboard.Key.end, keyboard.Key.page_up, keyboard.Key.page_down,
            keyboard.Key.delete, keyboard.Key.esc, keyboard.Key.tab, keyboard.Key.enter,
        ):
            self._reset()
            return
        try:
            ch = key.char
        except AttributeError:
            ch = None
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
            controller = keyboard.Controller()
            for _ in range(len(text)):
                controller.press(keyboard.Key.backspace)
                controller.release(keyboard.Key.backspace)
            controller.type(fixed)
        except Exception:
            pass
        finally:
            self._injecting = False
