# Persian Typo Fixer Desktop | main.py
# نقطه ورود برنامه: تای سیستمی + هاتک سراسری + پنجره تنظیمات

import json
import os
import sys
import platform

from PyQt6.QtWidgets import (
    QApplication, QSystemTrayIcon, QMenu, QMessageBox
)
from PyQt6.QtGui import QAction, QIcon
from PyQt6.QtCore import QTimer

from core.hotkey import HotkeyManager
from core.clipboard import fix_selection
from ui.settings import SettingsDialog

CONFIG_NAME = "config.json"


def config_path():
    if getattr(sys, "frozen", False):
        base = os.path.dirname(sys.executable)
    else:
        base = os.path.dirname(os.path.abspath(__file__))
    return os.path.join(base, CONFIG_NAME)


def load_config():
    path = config_path()
    defaults = {
        "enabled": True,
        "hotkey": "<ctrl>+<shift>+f",
        "startMinimized": True,
        "runAtStartup": False,
    }
    # imported defaults merged
    from core.fixer import PTF_DEFAULTS
    defaults.update(PTF_DEFAULTS)
    if os.path.exists(path):
        try:
            with open(path, "r", encoding="utf-8") as f:
                user = json.load(f)
            defaults.update(user)
        except Exception:
            pass
    return defaults


def save_config(cfg):
    path = config_path()
    try:
        with open(path, "w", encoding="utf-8") as f:
            json.dump(cfg, f, ensure_ascii=False, indent=2)
    except Exception as e:
        print(f"[Config] save failed: {e}")


class App:
    def __init__(self):
        self.cfg = load_config()
        self.app = QApplication(sys.argv)
        self.app.setQuitOnLastWindowClosed(False)

        icon_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "assets", "icon.png")
        self.icon = QIcon(icon_path) if os.path.exists(icon_path) else QIcon()

        self.tray = QSystemTrayIcon(self.icon, self.app)
        self.tray.setToolTip("غلط‌گیر فارسی")

        menu = QMenu()
        self.act_enable = QAction("فعال/غیرفعال", self.app)
        self.act_enable.setCheckable(True)
        self.act_enable.setChecked(self.cfg.get("enabled", True))
        self.act_enable.triggered.connect(self._toggle_enable)
        menu.addAction(self.act_enable)

        menu.addSeparator()
        act_settings = QAction("تنظیمات", self.app)
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

        self.hotkey = HotkeyManager(
            hotkey_str=self.cfg.get("hotkey", "<ctrl>+<shift>+f"),
            on_fix=self._on_fix,
        )
        self.hotkey.start()

    def _tray_activated(self, reason):
        from PyQt6.QtWidgets import QSystemTrayIcon
        if reason == QSystemTrayIcon.ActivationReason.DoubleClick:
            self._open_settings()

    def _toggle_enable(self, checked):
        self.cfg["enabled"] = checked
        save_config(self.cfg)

    def _on_fix(self, fixed_text):
        if not self.cfg.get("enabled", True):
            return
        self.tray.showMessage(
            "غلط‌گیر فارسی",
            f"متن اصلاح شد: {fixed_text[:60]}..." if len(fixed_text) > 60 else f"متن اصلاح شد: {fixed_text}",
            QSystemTrayIcon.MessageIcon.Information,
            3000,
        )

    def _open_settings(self):
        dlg = SettingsDialog(self.cfg, None)
        if dlg.exec():
            save_config(self.cfg)
            # restart hotkey with new combo
            self.hotkey.stop()
            self.hotkey = HotkeyManager(
                hotkey_str=self.cfg.get("hotkey", "<ctrl>+<shift>+f"),
                on_fix=self._on_fix,
            )
            self.hotkey.start()

    def _about(self):
        QMessageBox.information(
            None, "درباره غلط‌گیر فارسی",
            "نسخه دسکتاپ پلاگین Persian Typo Fixer\nتوسط TheAzizi\nاصلاح متن فارسی در همه برنامه‌های ویندوز و لینوکس",
        )

    def _exit(self):
        self.hotkey.stop()
        self.tray.hide()
        self.app.quit()

    def run(self):
        sys.exit(self.app.exec())


if __name__ == "__main__":
    App().run()
