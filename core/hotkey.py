# Persian Typo Fixer Desktop | core/hotkey.py
# گوش देनेِ زière سراسری و فعال‌سازی اصلاح

from pynput import keyboard
from core.clipboard import fix_selection


class HotkeyManager:
    def __init__(self, hotkey_str="<ctrl>+<shift>+f", on_fix=None):
        self.hotkey_str = hotkey_str
        self.on_fix = on_fix
        self._listener = None
        self._running = False

    def start(self):
        if self._running:
            return
        self._running = True

        def on_activate():
            fixed = fix_selection()
            if self.on_fix:
                self.on_fix(fixed)

        try:
            self._listener = keyboard.GlobalHotKeys({self.hotkey_str: on_activate})
            self._listener.start()
        except Exception as e:
            print(f"[Hotkey] Failed to start: {e}")
            self._running = False

    def stop(self):
        self._running = False
        if self._listener:
            try:
                self._listener.stop()
            except Exception:
                pass
            self._listener = None
