# Persian Typo Fixer Desktop | core/winkb.py
# هوک سراسری کیبورد ویندوز با رمزگشایی کاراکتر بر اساس لی‌اوت پنجرهٔ فعال

import ctypes
import ctypes.wintypes as wt
import threading

from core.keyevent import KeyEvent

WH_KEYBOARD_LL = 13
HC_ACTION = 0
WM_KEYDOWN = 0x0100
WM_KEYUP = 0x0101
WM_SYSKEYDOWN = 0x0104
WM_SYSKEYUP = 0x0105
WM_QUIT = 0x0012
PM_NOREMOVE = 0x0000

VK_SHIFT = 0x10
VK_CONTROL = 0x11
VK_MENU = 0x12
VK_LWIN = 0x5B
VK_RWIN = 0x5C
VK_PACKET = 0xE7
VK_PROCESSKEY = 0xE5

_SPECIAL = {
    0x08: "backspace",
    0x09: "tab",
    0x0D: "enter",
    0x1B: "esc",
    0x25: "left",
    0x26: "up",
    0x27: "right",
    0x28: "down",
    0x24: "home",
    0x23: "end",
    0x21: "page_up",
    0x22: "page_down",
    0x2E: "delete",
    0x10: "shift",
    0xA0: "shift",
    0xA1: "shift",
    0x11: "ctrl",
    0xA2: "ctrl",
    0xA3: "ctrl",
    0x12: "alt",
    0xA4: "alt",
    0xA5: "alt",
    0x5B: "cmd",
    0x5C: "cmd",
}


class _KBDLLHOOKSTRUCT(ctypes.Structure):
    _fields_ = [
        ("vkCode", wt.DWORD),
        ("scanCode", wt.DWORD),
        ("flags", wt.DWORD),
        ("time", wt.DWORD),
        ("dwExtraInfo", ctypes.c_void_p),
    ]


_HOOKPROC = ctypes.WINFUNCTYPE(wt.LPARAM, ctypes.c_int, wt.WPARAM, wt.LPARAM)

_user32 = ctypes.WinDLL("user32", use_last_error=True)
_kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

_SetWindowsHookEx = _user32.SetWindowsHookExW
_SetWindowsHookEx.argtypes = (ctypes.c_int, _HOOKPROC, wt.HINSTANCE, wt.DWORD)
_SetWindowsHookEx.restype = wt.HHOOK

_UnhookWindowsHookEx = _user32.UnhookWindowsHookEx
_UnhookWindowsHookEx.argtypes = (wt.HHOOK,)
_UnhookWindowsHookEx.restype = wt.BOOL

_CallNextHookEx = _user32.CallNextHookEx
_CallNextHookEx.argtypes = (wt.HHOOK, ctypes.c_int, wt.WPARAM, wt.LPARAM)
_CallNextHookEx.restype = wt.LPARAM

_GetMessage = _user32.GetMessageW
_GetMessage.argtypes = (ctypes.POINTER(wt.MSG), wt.HWND, wt.UINT, wt.UINT)
_GetMessage.restype = wt.BOOL

_PeekMessage = _user32.PeekMessageW
_PeekMessage.argtypes = (ctypes.POINTER(wt.MSG), wt.HWND, wt.UINT, wt.UINT, wt.UINT)
_PeekMessage.restype = wt.BOOL

_PostThreadMessage = _user32.PostThreadMessageW
_PostThreadMessage.argtypes = (wt.DWORD, wt.UINT, wt.WPARAM, wt.LPARAM)
_PostThreadMessage.restype = wt.BOOL

_GetForegroundWindow = _user32.GetForegroundWindow
_GetForegroundWindow.restype = wt.HWND

_GetWindowThreadProcessId = _user32.GetWindowThreadProcessId
_GetWindowThreadProcessId.argtypes = (wt.HWND, ctypes.POINTER(wt.DWORD))
_GetWindowThreadProcessId.restype = wt.DWORD

_GetKeyboardLayout = _user32.GetKeyboardLayout
_GetKeyboardLayout.argtypes = (wt.DWORD,)
_GetKeyboardLayout.restype = wt.HKL

_GetAsyncKeyState = _user32.GetAsyncKeyState
_GetAsyncKeyState.argtypes = (ctypes.c_int,)
_GetAsyncKeyState.restype = ctypes.c_short

_ToUnicodeEx = _user32.ToUnicodeEx
_ToUnicodeEx.argtypes = (
    wt.UINT,
    wt.UINT,
    ctypes.c_void_p,
    ctypes.c_void_p,
    ctypes.c_int,
    wt.UINT,
    wt.HKL,
)
_ToUnicodeEx.restype = ctypes.c_int

_GetCurrentThreadId = _kernel32.GetCurrentThreadId
_GetCurrentThreadId.restype = wt.DWORD


def _is_down(vk):
    return bool(_GetAsyncKeyState(vk) & 0x8000)


class WindowsKeyboardHook:
    def __init__(self, on_press, on_release, should_ignore=None):
        self._on_press = on_press
        self._on_release = on_release
        self._should_ignore = should_ignore or (lambda: False)
        self._thread = None
        self._thread_id = None
        self._hook = None
        self._ready = threading.Event()
        self._proc = _HOOKPROC(self._callback)

    def start(self):
        if self._thread is not None:
            return
        self._ready.clear()
        self._thread = threading.Thread(target=self._run, daemon=True)
        self._thread.start()
        self._ready.wait(2)

    def stop(self):
        if self._thread is None:
            return
        if self._thread_id:
            _PostThreadMessage(self._thread_id, WM_QUIT, 0, 0)
        self._thread.join(1)
        self._thread = None
        self._thread_id = None

    def _run(self):
        self._thread_id = _GetCurrentThreadId()
        msg = wt.MSG()
        _PeekMessage(ctypes.byref(msg), None, 0, 0, PM_NOREMOVE)
        self._hook = _SetWindowsHookEx(WH_KEYBOARD_LL, self._proc, None, 0)
        self._ready.set()
        if not self._hook:
            return
        try:
            while _GetMessage(ctypes.byref(msg), None, 0, 0) > 0:
                pass
        finally:
            _UnhookWindowsHookEx(self._hook)
            self._hook = None

    def _callback(self, code, wparam, lparam):
        if code == HC_ACTION:
            data = ctypes.cast(lparam, ctypes.POINTER(_KBDLLHOOKSTRUCT)).contents
            if not self._should_ignore() and not (data.flags & 0x00000002):
                try:
                    self._dispatch(wparam, data)
                except Exception:
                    pass
        return _CallNextHookEx(None, code, wparam, lparam)

    def _dispatch(self, msg, data):
        is_press = msg in (WM_KEYDOWN, WM_SYSKEYDOWN)
        vk = data.vkCode
        if vk == VK_PROCESSKEY:
            return
        name = _SPECIAL.get(vk)
        if name is not None:
            event = KeyEvent(name=name)
        elif vk == VK_PACKET:
            if not is_press:
                return
            event = KeyEvent(char=chr(data.scanCode))
        else:
            if not is_press:
                return
            if _is_down(VK_CONTROL) or _is_down(VK_MENU) or _is_down(VK_LWIN) or _is_down(VK_RWIN):
                event = KeyEvent()
            else:
                event = KeyEvent(char=self._translate(vk, data.scanCode))
        if is_press:
            self._on_press(event)
        else:
            self._on_release(event)

    @staticmethod
    def _translate(vk, scan):
        state = (ctypes.c_ubyte * 256)()
        if _is_down(VK_SHIFT):
            state[VK_SHIFT] = 0x80
        if _GetAsyncKeyState(0x14) & 0x0001:
            state[0x14] = 0x01
        hwnd = _GetForegroundWindow()
        thread_id = _GetWindowThreadProcessId(hwnd, None) if hwnd else 0
        layout = _GetKeyboardLayout(thread_id)
        buffer = ctypes.create_unicode_buffer(8)
        count = _ToUnicodeEx(vk, scan, state, buffer, len(buffer), 0, layout)
        if count > 0 and buffer[0]:
            return buffer[0]
        return None
