# Persian Typo Fixer Desktop | main.py
# نقطه ورود برنامه: آیکون سینی سیستم + میانبر سراسری + پنجره تنظیمات

import json
import os
import sys
import time

from PyQt6.QtCore import QObject, pyqtSignal
from PyQt6.QtGui import QAction, QIcon
from PyQt6.QtWidgets import QApplication, QMenu, QMessageBox, QSystemTrayIcon

from core.clipboard import fix_selection
from core.fixer import PTF_DEFAULTS
from core.hotkey import HotkeyManager
from ui.settings import SettingsDialog

CONFIG_NAME = "config.json"
DEFAULT_HOTKEY = "<ctrl>+<shift>+f"


def config_path():
    if getattr(sys, "frozen", False):
        base = os.path.dirname(sys.executable)
    else:
        base = os.path.dirname(os.path.abspath(__file__))
    return os.path.join(base, CONFIG_NAME)


def load_config():
    cfg = {"enabled": True, "hotkey": DEFAULT_HOTKEY}
    cfg.update(PTF_DEFAULTS)
    path = config_path()
    if os.path.exists(path):
        try:
            with open(path, "r", encoding="utf-8") as f:
                saved = json.load(f)
            if isinstance(saved, dict):
                cfg.update(saved)
        except Exception as e:
            print("[Config] load failed:", e)
    return cfg


def save_config(cfg):
    path = config_path()
    try:
        with open(path, "w", encoding="utf-8") as f:
            json.dump(cfg, f, ensure_ascii=False, indent=2)
    except Exception as e:
        print("[Config] save failed:", e)


class App(QObject):
    fix_finished = pyqtSignal(str, str)

    def __init__(self):
        super().__init__()
        self.cfg = load_config()
        self.app = QApplication(sys.argv)
        self.app.setQuitOnLastWindowClosed(False)

        base = os.path.dirname(os.path.abspath(__file__))
        icon_path = os.path.join(base, "assets", "icon.png")
        self.icon = QIcon(icon_path) if os.path.exists(icon_path) else QIcon()

        self.tray = QSystemTrayIcon(self.icon, self.app)
        self.tray.setToolTip("غلط‌گیر فارسی — برای اصلاح متن انتخاب‌شده میانبر را بزنید")

        menu = QMenu()
        self.act_enable = QAction("فعال", self.app)
        self.act_enable.setCheckable(True)
        self.act_enable.setChecked(bool(self.cfg.get("enabled", True)))
        self.act_enable.triggered.connect(self._toggle_enable)
        menu.addAction(self.act_enable)

        menu.addSeparator()
        act_settings = QAction("تنظیمات…", self.app)
        act_settings.triggered.connect(self._open_settings)
        menu.addAction(act_settings)

        act_about = QAction("درباره", self.app)
        act_about.triggered.connect(self._about)
        menu.addAction(act_about)

        menu.addSeparator()
        act_exit = QAction("خروج", self.app)
        act_exit.triggered.connect(self._exit)
        menu.addAction(act_exit)

        self.tray.setContextMenu(menu)
        self.tray.activated.connect(self._tray_activated)
        self.tray.show()

        self.hotkey = None
        self.fix_finished.connect(self._on_fix_finished)
        self._start_hotkey()

    def _start_hotkey(self):
        self.hotkey = HotkeyManager(
            hotkey_str=self.cfg.get("hotkey", DEFAULT_HOTKEY),
            on_fix=self._hotkey_pressed,
        )
        self.hotkey.start()

    def _hotkey_pressed(self):
        if not self.cfg.get("enabled", True):
            self.fix_finished.emit("disabled", "")
            return
        time.sleep(0.18)
        fixed = fix_selection(self.cfg)
        if fixed:
            self.fix_finished.emit("ok", fixed)
        else:
            self.fix_finished.emit("empty", "")

    def _on_fix_finished(self, kind, fixed_text):
        if kind == "disabled":
            self.tray.showMessage(
                "غلط‌گیر فارسی",
                "برنامه خاموش است؛ از منوی آیکون سینی روشنش کنید.",
                QSystemTrayIcon.MessageIcon.Information,
                2000,
            )
            return
        if kind != "ok" or not fixed_text:
            self.tray.showMessage(
                "غلط‌گیر فارسی",
                "متن انتخاب‌شده‌ای پیدا نشد یا نیازی به اصلاح نبود.",
                QSystemTrayIcon.MessageIcon.Information,
                2000,
            )
            return
        preview = fixed_text if len(fixed_text) <= 60 else fixed_text[:60] + "…"
        self.tray.showMessage(
            "غلط‌گیر فارسی",
            "متن اصلاح شد:\n" + preview,
            QSystemTrayIcon.MessageIcon.Information,
            3000,
        )

    def _tray_activated(self, reason):
        if reason == QSystemTrayIcon.ActivationReason.DoubleClick:
            self._open_settings()

    def _toggle_enable(self, checked):
        self.cfg["enabled"] = bool(checked)
        save_config(self.cfg)

    def _open_settings(self):
        dlg = SettingsDialog(self.cfg)
        if dlg.exec():
            save_config(self.cfg)
            if self.hotkey is not None:
                self.hotkey.stop()
            self._start_hotkey()

    def _about(self):
        QMessageBox.information(
            None,
            "درباره غلط‌گیر فارسی",
            "نسخه دسکتاپ Persian Typo Fixer\n\n"
            "متن فارسی را در هر برنامه‌ای انتخاب کنید و میانبر سراسری را بزنید.\n"
            "پیش‌فرض: Ctrl+Shift+F\n\n"
            "توسط TheAzizi — بدون ارسال هیچ داده‌ای به اینترنت.",
        )

    def _exit(self):
        if self.hotkey is not None:
            self.hotkey.stop()
        self.tray.hide()
        self.app.quit()

    def run(self):
        return self.app.exec()


if __name__ == "__main__":
    sys.exit(App().run())
