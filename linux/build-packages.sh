#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${VERSION:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' \
  "$ROOT/dotnet/src/PersianTypoFixer.App/PersianTypoFixer.App.csproj" | head -n1)}"
VERSION="${VERSION:-2.3.0}"
NFPM="${NFPM:-nfpm}"
BIN="$ROOT/dist/linux/PersianTypoFixer"
OUT="$ROOT/dist/packages"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

if [ ! -f "$BIN" ]; then
  echo "Linux binary not found: $BIN" >&2
  echo "Run the dotnet publish step first." >&2
  exit 1
fi

if ! command -v "$NFPM" >/dev/null 2>&1; then
  echo "nfpm not found. Install it from https://nfpm.goreleaser.com" >&2
  exit 1
fi

mkdir -p "$OUT"
sed "s/__VERSION__/$VERSION/g" "$ROOT/linux/nfpm.yaml" > "$TMP/nfpm.yaml"
sed "s/^arch: amd64$/arch: x86_64/" "$TMP/nfpm.yaml" > "$TMP/nfpm-archlinux.yaml"
cd "$ROOT"

"$NFPM" package --config "$TMP/nfpm.yaml" --packager deb \
  --target "$OUT/persian-typo-fixer_${VERSION}-1_amd64.deb"
"$NFPM" package --config "$TMP/nfpm.yaml" --packager rpm \
  --target "$OUT/persian-typo-fixer-${VERSION}-1.x86_64.rpm"
"$NFPM" package --config "$TMP/nfpm-archlinux.yaml" --packager archlinux \
  --target "$OUT/persian-typo-fixer-${VERSION}-1-x86_64.pkg.tar.zst"

STAGE="$TMP/Persian.Typo.Fixer-${VERSION}-linux-x86_64"
mkdir -p "$STAGE"
cp "$BIN" "$STAGE/PersianTypoFixer"
cp "$ROOT/LICENSE" "$STAGE/LICENSE"
cp "$ROOT/assets/icon.png" "$STAGE/icon.png"
cp "$ROOT/linux/portable-README.txt" "$STAGE/README.txt"
chmod 0755 "$STAGE" "$STAGE/PersianTypoFixer"
chmod 0644 "$STAGE/LICENSE" "$STAGE/icon.png" "$STAGE/README.txt"
tar -C "$TMP" -czf "$OUT/Persian.Typo.Fixer-${VERSION}-linux-x86_64.tar.gz" \
  "Persian.Typo.Fixer-${VERSION}-linux-x86_64"

echo "Packages created in $OUT:"
find "$OUT" -maxdepth 1 -type f -printf '  %f\n' | sort
