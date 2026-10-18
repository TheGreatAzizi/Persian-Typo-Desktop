# Persian Typo Fixer Desktop | core/clipboard.py
# کلیپ‌بورد و شبیه‌سازی Paste برای اصلاح درون-line

import platform
import pyperclip
import pyautogui

SYSTEM = platform.system()


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


def simulate_paste() -> None:
    """ف assumes the target field is focused; sends Ctrl+V."""
    if SYSTEM == "Darwin":
        pyautogui.hotkey("cmd", "v")
    else:
        pyautogui.hotkey("ctrl", "v")


def simulate_copy() -> str:
    """Kills selection into clipboard and returns it."""
    if SYSTEM == "Darwin":
        pyautogui.hotkey("cmd", "c")
    else:
        pyautogui.hotkey("ctrl", "c")
    #おやすみ جدا کوچک تا کلیپ‌بورد آپدیت شود
    import time
    time.sleep(0.05)
    return get_clipboard()


def fix_selection() -> str:
    """متن انتخاب شده را می‌گیرد، اصلاح می‌کند، و جای خود می‌گذارد."""
    selected = simulate_copy()
    if not selected or not selected.strip():
        return ""
    from core.fixer import ptfFixText, PTF_DEFAULTS
    fixed = ptfFixText(selected, PTF_DEFAULTS)
    if fixed == selected:
        return ""
    set_clipboard(fixed)
    simulate_paste()
    return fixed
