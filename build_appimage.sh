#!/usr/bin/env bash
# build_appimage.sh — ساخت AppImage برای لینوکس (آرچ و دیگر توزیع‌ها)
set -euo pipefail

APP_ID="com.thegreatazizi.persiantypofixer"
APP_BIN="PersianTypoFixer"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' \
  dotnet/src/PersianTypoFixer.App/PersianTypoFixer.App.csproj | head -n1)"
VERSION="${VERSION:-2.3.0}"
APP_DIR="AppDir"
USR="$APP_DIR/usr"
BIN="$USR/bin"
SHARE="$USR/share"
ICON="$SHARE/icons/hicolor/256x256/apps"
DESKTOP="$SHARE/applications"

rm -rf "$APP_DIR" dist
mkdir -p "$BIN" "$ICON" "$DESKTOP"

# ۱) ساخت باینری لینوکس با .NET 8 (self-contained، تک‌فایل)
dotnet publish dotnet/src/PersianTypoFixer.App/PersianTypoFixer.App.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o dist/linux
cp "dist/linux/$APP_BIN" "$BIN/$APP_BIN"
chmod +x "$BIN/$APP_BIN"

# ۲) آیکون‌ها و فایل دسکتاپ (فایل دسکتاپ باید در ریشهٔ AppDir هم باشد)
cp assets/icon.png "$ICON/$APP_ID.png"
cp assets/icon.png "$APP_DIR/$APP_ID.png"
cp assets/icon.png "$APP_DIR/.DirIcon"

mkdir -p "$SHARE/metainfo"
cp "linux/$APP_ID.desktop" "$DESKTOP/$APP_ID.desktop"
sed -i -e 's|^Exec=.*|Exec=AppRun|' -e '/^TryExec=/d' "$DESKTOP/$APP_ID.desktop"
cp "$DESKTOP/$APP_ID.desktop" "$APP_DIR/$APP_ID.desktop"
cp "linux/$APP_ID.appdata.xml" "$SHARE/metainfo/$APP_ID.appdata.xml"

# ۳) فایل AppRun برای اجرای AppImage
cat > "$APP_DIR/AppRun" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
HERE="$(dirname "$(readlink -f "$0")")"
export APPDIR="$HERE"
exec "$HERE/usr/bin/PersianTypoFixer" "$@"
EOF
chmod +x "$APP_DIR/AppRun"

# ۴) آماده‌سازی appimagetool
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

OUT="dist/Persian.Typo.Fixer-${VERSION}-x86_64.AppImage"
mkdir -p dist
if ! ARCH=x86_64 "$APPIMAGETOOL" "$APP_DIR" "$OUT"; then
  echo "اجرای عادی ناموفق بود؛ تلاش دوباره بدون FUSE…"
  ARCH=x86_64 "$APPIMAGETOOL" --appimage-extract-and-run "$APP_DIR" "$OUT"
fi
echo "AppImage ساخته شد: $OUT"
