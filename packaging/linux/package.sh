#!/usr/bin/env bash
# Gera o .deb e o .tar.gz a partir de artifacts/publish/linux-x64. Uso: package.sh 0.1.0
set -euo pipefail
version="$1"
root="$(cd "$(dirname "$0")/../.." && pwd)"
src="$root/artifacts/publish/linux-x64"
pkg="$root/artifacts/deb"
mkdir -p "$root/dist"

tar -C "$src" -czf "$root/dist/YTStudio-$version-linux-x64.tar.gz" .

rm -rf "$pkg"
mkdir -p "$pkg/DEBIAN" "$pkg/opt/ytstudio" "$pkg/usr/bin" "$pkg/usr/share/applications"
cp -R "$src/." "$pkg/opt/ytstudio/"
chmod 755 "$pkg/opt/ytstudio/YTStudio"
ln -s /opt/ytstudio/YTStudio "$pkg/usr/bin/ytstudio"
cp "$root/packaging/linux/ytstudio.desktop" "$pkg/usr/share/applications/"
cat > "$pkg/DEBIAN/control" <<CONTROL
Package: ytstudio
Version: $version
Section: utils
Priority: optional
Architecture: amd64
Maintainer: Mateus Kraiewski
Depends: libwebkit2gtk-4.1-0, libgtk-3-0, libnotify4
Description: YTStudio - gerencie seus canais do YouTube
 Roteiros, agenda de publicação e senhas num único arquivo cifrado.
CONTROL
dpkg-deb --root-owner-group --build "$pkg" "$root/dist/ytstudio_${version}_amd64.deb"
