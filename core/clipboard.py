# Persian Typo Fixer Desktop | core/clipboard.py
# کلیپ‌بورد و شبیه‌سازی copy/paste برای اصلاح متن انتخاب‌شده

import platform
import threading
import time

import pyautogui
import pyperclip

SYSTEM = platform.system()
SENTINEL = "\u0000PTF_CLIPBOARD_SENTINEL\u0000"


def get_clipboard() -> str:
    try:
        return pyperclip.paste() or ""
    except Exception:
        return ""


def set_clipboard(text: str) -> None:
    try:
        pyperclip.copy(text)
    except Exception:
        pass


def _copy_key() -> None:
    if SYSTEM == "Darwin":
        pyautogui.hotkey("cmd", "c")
    else:
        pyautogui.hotkey("ctrl", "c")


def _paste_key() -> None:
    if SYSTEM == "Darwin":
        pyautogui.hotkey("cmd", "v")
    else:
        pyautogui.hotkey("ctrl", "v")


def grab_selection(timeout: float = 0.8) -> str:
    set_clipboard(SENTINEL)
    _copy_key()
    deadline = time.time() + timeout
    while time.time() < deadline:
        value = get_clipboard()
        if value != SENTINEL:
            return value
        time.sleep(0.02)
    return ""


def fix_selection(opts=None) -> str:
    from core.fixer import PTF_DEFAULTS, ptfFixText

    try:
        backup = get_clipboard()
        selected = grab_selection()
        if not selected or not selected.strip():
            set_clipboard(backup)
            return ""
        fixed = ptfFixText(selected, opts or PTF_DEFAULTS)
        if fixed == selected:
            set_clipboard(backup)
            return ""
        set_clipboard(fixed)
        time.sleep(0.05)
        _paste_key()
        threading.Timer(0.8, lambda: set_clipboard(backup)).start()
        return fixed
    except Exception as e:
        print("[Clipboard] fix failed:", e)
        return ""
