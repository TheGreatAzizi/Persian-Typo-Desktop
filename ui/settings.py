# Persian Typo Fixer Desktop | ui/settings.py
# پنجره تنظیمات با PyQt6

from PyQt6.QtWidgets import (
    QDialog, QVBoxLayout, QHBoxLayout, QGroupBox, QCheckBox,
    QLabel, QSpinBox, QPushButton, QTabWidget, QWidget,
    QScrollArea, QMessageBox, QLineEdit, QListWidget,
    QListWidgetItem, QInputDialog, QDialogButtonBox
)
from PyQt6.QtCore import Qt
from core.fixer import PTF_DEFAULTS


class SettingsDialog(QDialog):
    def __init__(self, config, parent=None):
        super().__init__(parent)
        self.config = config
        self.setWindowTitle("تنظیمات غلط‌گیر فارسی")
        self.resize(700, 520)
        self.setLayoutDirection(Qt.LayoutDirection.RightToLeft)

        tabs = QTabWidget()
        tabs.addTab(self._general_tab(), "عمومی")
        tabs.addTab(self._fixes_tab(), "اصلاحات")
        tabs.addTab(self._custom_tab(), "دیکشنری شخصی")

        btns = QDialogButtonBox(QDialogButtonBox.StandardButton.Save | QDialogButtonBox.StandardButton.Cancel)
        btns.accepted.connect(self.save_and_accept)
        btns.rejected.connect(self.reject)

        lay = QVBoxLayout()
        lay.addWidget(tabs)
        lay.addWidget(btns)
        self.setLayout(lay)

        self._load()

    def _chk(self, key, label):
        cb = QCheckBox(label)
        cb.setChecked(self.config.get(key, PTF_DEFAULTS[key]))
        cb.setLayoutDirection(Qt.LayoutDirection.RightToLeft)
        return key, cb

    def _general_tab(self):
        w = QWidget()
        lay = QVBoxLayout()
        self.general_cbs = []
        for key, label in [
            ("fixArabicYK", "اصلاح حروف عربی ی و ک (ي/ى -> ی ، ك -> ک)"),
            ("fixArabicAlef", "اصلاح الف عربية (أ/إ -> ا ، ة -> ه)"),
            ("fixArabicDigits", "تبدیل ارقام عربی به فارسی (٠ -> ۰)"),
            ("fixLatinDigits", "تبدیل ار 부딪ه لاتین به فارسی (0 -> ۰)"),
            ("fixKashida", "حذف کشیده ـ"),
            ("fixTashkeel", "حذف حرکات عربية"),
        ]:
            key, cb = self._chk(key, label)
            self.general_cbs.append((key, cb))
            lay.addWidget(cb)
        w.setLayout(lay)
        return w

    def _fixes_tab(self):
        w = QWidget()
        lay = QVBoxLayout()
        self.fixes_cbs = []
        for key, label in [
            ("fixZWNJ", "نیم‌فاصله (می stellten -> می stellten)"),
            ("fixAttachedHa", "ضمیر چسبیده (کتابها -> کتاب‌ها)"),
            ("fixCompounds", "ترکیبات (کتاب خانه -> کتابخانه)"),
            ("fixCommonTypos", "غلط‌های رایج (انشاالله -> ان‌شاءالله)"),
            ("fixPersianPunct", "علائم نگارشی فارسی (, -> ،)"),
            ("fixPunctuation", "فاصله‌گذاری نقطه‌گذاری"),
            ("fixDoubleSpace", "حذف فاصله‌های تکراری"),
            ("fixRepeat", "حذف حروف تکراری (سلااام -> سلام)"),
            ("fixQuotes", "نقل‌قول فرانسیسی به << >>"),
            ("fixSeparators", "جداکننده اعداد فارسی"),
        ]:
            key, cb = self._chk(key, label)
            self.fixes_cbs.append((key, cb))
            lay.addWidget(cb)
        w.setLayout(lay)
        sc = QScrollArea()
        sc.setWidget(w)
        sc.setWidgetResizable(True)
        return sc

    def _custom_tab(self):
        w = QWidget()
        lay = QVBoxLayout()

        self.list = QListWidget()
        self.list.setLayoutDirection(Qt.LayoutDirection.RightToLeft)
        lay.addWidget(QLabel("کلمات شخصی (غلط -> درست):"))
        lay.addWidget(self.list)

        btn_lay = QHBoxLayout()
        btn_add = QPushButton("افزودن")
        btn_edit = QPushButton("ویرایش")
        btn_del = QPushButton("حذف")
        btn_add.clicked.connect(self._add)
        btn_edit.clicked.connect(self._edit)
        btn_del.clicked.connect(self._delete)
        btn_lay.addWidget(btn_add)
        btn_lay.addWidget(btn_edit)
        btn_lay.addWidget(btn_del)
        lay.addLayout(btn_lay)
        w.setLayout(lay)
        return w

    def _load(self):
        self.list.clear()
        for item in self.config.get("customWords", []):
            self.list.addItem(f"{item.get('from','')} -> {item.get('to','')}")

    def _add(self):
        frm, ok1 = QInputDialog.getText(self, "از ابتدا", "کلمه غلط:")
        if not ok1 or not frm:
            return
        to, ok2 = QInputDialog.getText(self, "به درست", "کلمه درست:")
        if not ok2:
            return
        self.config.setdefault("customWords", []).append({"from": frm, "to": to, "on": True})
        self.list.addItem(f"{frm} -> {to}")

    def _edit(self):
        row = self.list.currentRow()
        if row < 0:
            return
        items = self.config.setdefault("customWords", [])
        item = items[row]
        frm, ok1 = QInputDialog.getText(self, "ویرایش", "کلمه غلط:", text=item.get("from", ""))
        if not ok1:
            return
        to, ok2 = QInputDialog.getText(self, "ویرایش", "کلمه درست:", text=item.get("to", ""))
        if not ok2:
            return
        item["from"] = frm
        item["to"] = to
        self.list.item(row).setText(f"{frm} -> {to}")

    def _delete(self):
        row = self.list.currentRow()
        if row < 0:
            return
        items = self.config.setdefault("customWords", [])
        items.pop(row)
        self.list.takeItem(row)

    def save_and_accept(self):
        for key, cb in self.general_cbs + self.fixes_cbs:
            self.config[key] = cb.isChecked()
        self.accept()
