#!/usr/bin/env bash
# build_appimage.sh — ساخت AppImage برای لینوکس (آرچ و دیگر توزیع‌ها)
set -euo pipefail

APP_NAME="Persian Typo Fixer"
APP_ID="com.thegreatazizi.persiantypofixer"
VERSION="1.0.0"
APP_DIR="AppDir"
USR="$APP_DIR/usr"
BIN="$USR/bin"
LIB="$USR/lib"
SHARE="$USR/share"
ICON="$SHARE/icons/hicolor/128x128/apps"
DESKTOP="$SHARE/applications"

rm -rf "$APP_DIR" dist
mkdir -p "$BIN" "$LIB" "$ICON" "$DESKTOP"

# ۱) نصب وابستگی‌ها در یک env مجازی (اگر venv نیست)
if [ ! -d ".venv" ]; then
  python3 -m venv .venv
fi
source .venv/bin/activate
pip install --upgrade pip
pip install -r requirements.txt
pip install pyinstaller

# ۲) ساخت exe-wrapper لینوکس با PyInstaller (onefile، بدون کنسول)
pyinstaller --noconfirm --clean --onefile --windowed \
  --name "$APP_NAME" \
  --icon assets/icon.png \
  --add-data "assets/icon.png:assets" \
  --hiddenimport PyQt6.QtWidgets \
  --hiddenimport PyQt6.QtGui \
  --hiddenimport PyQt6.QtCore \
  --hiddenimport pyperclip \
  --hiddenimport pyautogui \
  --hiddenimport pynput \
  --hiddenimport core.fixer \
  --hiddenimport core.clipboard \
  --hiddenimport core.hotkey \
  --hiddenimport ui.settings \
  main.py

# ۳) کپی باینری به AppDir
cp "dist/$APP_NAME" "$BIN/$APP_NAME"
chmod +x "$BIN/$APP_NAME"

# ۴) آیکون و دسکتاپ
cp assets/icon.png "$ICON/$APP_ID.png"

cat > "$DESKTOP/$APP_ID.desktop" <<EOF
[Desktop Entry]
Name=$APP_NAME
GenericName=Persian Typo Fixer
Comment=اصلاح خودکار متن فارسی در همه برنامه‌ها
Exec=$BIN/$APP_NAME %F
Icon=$APP_ID
Terminal=false
Type=Application
Categories=Office;TextEditor;Utility;
StartupNotify=true
EOF

# ۵) فایل पRun helper برای AppImage
cat > "$APP_DIR/AppRun" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
HERE="$(dirname "$(readlink -f "$0")")"
export APPDIR="$HERE"
export PATH="$HERE/usr/bin:$PATH"
export LD_LIBRARY_PATH="$HERE/usr/lib:$HERE/usr/lib/x86_64-linux-gnu:${LD_LIBRARY_PATH:-}"
export QT_QPA_PLATFORM_PLUGIN_PATH="$HERE/usr/lib/qt6/plugins"
export QT_PLUGIN_PATH="$HERE/usr/lib/qt6/plugins"
exec "$HERE/usr/bin/Persian Typo Fixer" "$@"
EOF
chmod +x "$APP_DIR/AppRun"

# ۶) ساخت AppImage با appimagetool (اگر نصب نیست دانلود شود)
if ! command -v appimagetool &> /dev/null; then
  echo "appimagetool یافت نشد. لطفاً از https://github.com/AppImage/AppImageKit نصب کنید."
  echo "ساخت AppDir تمام شد در: $APP_DIR"
  exit 0
fi

ARCH=x86_64 appimagetool "$APP_DIR" "dist/${APP_NAME}-${VERSION}-x86_64.AppImage"
echo "AppImage ساخته شد: dist/${APP_NAME}-${VERSION}-x86_64.AppImage"
