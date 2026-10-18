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

# ۱) نصب وابستگی‌ها در یک محیط مجازی
if [ ! -d ".venv" ]; then
  python3 -m venv .venv
fi
source .venv/bin/activate
pip install --upgrade pip
pip install -r requirements.txt
pip install pyinstaller

# ۲) ساخت باینری لینوکس با PyInstaller (onefile، بدون کنسول)
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

# ۴) آیکون‌ها و فایل دسکتاپ
cp assets/icon.png "$ICON/$APP_ID.png"
cp assets/icon.png "$APP_DIR/$APP_ID.png"
cp assets/icon.png "$APP_DIR/.DirIcon"

cat > "$DESKTOP/$APP_ID.desktop" <<EOF
[Desktop Entry]
Name=$APP_NAME
GenericName=Persian Typo Fixer
Comment=اصلاح خودکار متن فارسی در همه برنامه‌ها
Exec=AppRun %F
Icon=$APP_ID
Terminal=false
Type=Application
Categories=Office;Utility;
StartupNotify=true
EOF

# ۵) فایل AppRun برای اجرای AppImage
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

# ۶) آماده‌سازی appimagetool
if command -v appimagetool &> /dev/null; then
  APPIMAGETOOL="appimagetool"
elif [ -x "./appimagetool" ]; then
  APPIMAGETOOL="./appimagetool"
else
  echo "appimagetool یافت نشد؛ در حال دانلود…"
  if ! wget -q -O appimagetool "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"; then
    wget -q -O appimagetool "https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage"
  fi
  chmod +x appimagetool
  APPIMAGETOOL="./appimagetool"
fi

OUT="dist/${APP_NAME}-${VERSION}-x86_64.AppImage"
mkdir -p dist
if ! ARCH=x86_64 "$APPIMAGETOOL" "$APP_DIR" "$OUT"; then
  echo "اجرای عادی ناموفق بود؛ تلاش دوباره بدون FUSE…"
  ARCH=x86_64 "$APPIMAGETOOL" --appimage-extract-and-run "$APP_DIR" "$OUT"
fi
echo "AppImage ساخته شد: $OUT"
