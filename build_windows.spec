# -*- mode: python ; coding: utf-8 -*-

import sys
sys.setrecursionlimit(5000)

block_cipher = None

a = Analysis(
    ['main.py'],
    pathex=[],
    binaries=[],
    datas=[('assets/icon.png', 'assets')],
    hiddenimports=[
        'PyQt6', 'PyQt6.QtWidgets', 'PyQt6.QtGui', 'PyQt6.QtCore',
        'pyperclip', 'pyautogui', 'pynput',
        'core.fixer', 'core.clipboard', 'core.hotkey', 'ui.settings',
    ],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.zipfiles,
    a.datas,
    [],
    name='Persian Typo Fixer',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon='assets/icon.png',
)
