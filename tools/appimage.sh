#!/usr/bin/env bash
# Builds dist/Ironfield-x86_64.AppImage: a self-contained linux-x64 publish in an AppDir (needs no .NET on the target; Fedora
# supplies the GL, X11/Wayland and audio libraries). Saves and settings go to ~/.config/Ironfield as usual.
# Needs `appimagetool` (on PATH, or dist/appimagetool-x86_64.AppImage, or APPIMAGETOOL=/path). Without it, stops after the AppDir.
set -euo pipefail
cd "$(dirname "$0")/.."
OUT=dist; APPDIR=$OUT/Ironfield.AppDir
rm -rf "$APPDIR"; mkdir -p "$APPDIR/usr/bin" "$OUT"
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=false -o "$APPDIR/usr/bin"
cat > "$APPDIR/AppRun" <<'R'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
cd "$HERE/usr/bin"
exec "$HERE/usr/bin/Ironfield" "$@"
R
chmod +x "$APPDIR/AppRun"
cat > "$APPDIR/Ironfield.desktop" <<'D'
[Desktop Entry]
Type=Application
Name=Ironfield
Comment=A 2.5D medieval battle and campaign game
Exec=Ironfield
Icon=ironfield
Categories=Game;StrategyGame;
Terminal=false
D
magick Icon.bmp -background none -filter point -resize 256x256 "$APPDIR/ironfield.png"
cp "$APPDIR/ironfield.png" "$APPDIR/.DirIcon"
TOOL=${APPIMAGETOOL:-$(command -v appimagetool || true)}
[ -n "$TOOL" ] || { [ -x "$OUT/appimagetool-x86_64.AppImage" ] && TOOL=$OUT/appimagetool-x86_64.AppImage; }
if [ -z "${TOOL:-}" ]; then echo "AppDir ready at $APPDIR; appimagetool not found, so no AppImage was packed."; exit 0; fi
ARCH=x86_64 "$TOOL" --no-appstream "$APPDIR" "$OUT/Ironfield-x86_64.AppImage"
ls -lh "$OUT/Ironfield-x86_64.AppImage"
