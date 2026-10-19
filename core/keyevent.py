# Persian Typo Fixer Desktop | core/keyevent.py
# رخداد کلید نرمال‌شده برای هر دو مسیر ویندوز و pynput

class KeyEvent:
    __slots__ = ("char", "name")

    def __init__(self, char=None, name=None):
        self.char = char
        self.name = name

    def __repr__(self):
        return "KeyEvent(char=%r, name=%r)" % (self.char, self.name)
