# Persian Typo Fixer Desktop | core/hotkey.py
# مدیریت میانبر سراسری و فراخوانی اصلاح

from pynput import keyboard


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
            if self.on_fix:
                self.on_fix()

        try:
            self._listener = keyboard.GlobalHotKeys({self.hotkey_str: on_activate})
            self._listener.start()
        except Exception as e:
            print("[Hotkey] failed to start (%s): %s" % (self.hotkey_str, e))
            self._running = False

    def stop(self):
        self._running = False
        if self._listener:
            try:
                self._listener.stop()
            except Exception:
                pass
            self._listener = None
