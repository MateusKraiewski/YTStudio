#!/usr/bin/env bash
# Monta YTStudio.app e o .dmg a partir de artifacts/publish/<rid>. Uso: package.sh osx-arm64 0.1.0
set -euo pipefail
rid="$1"; version="$2"
root="$(cd "$(dirname "$0")/../.." && pwd)"
src="$root/artifacts/publish/$rid"
stage="$root/artifacts/dmg-$rid"
app="$stage/YTStudio.app"

rm -rf "$stage" && mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$root/dist"
cp -R "$src/." "$app/Contents/MacOS/"
sed "s/__VERSION__/$version/g" "$root/packaging/macos/Info.plist" > "$app/Contents/Info.plist"

# Assinatura ad hoc: obrigatória para rodar em Apple Silicon. Não é a assinatura paga da Apple,
# então na primeira vez o usuário abre com botão direito → Abrir.
codesign --force --deep --sign - "$app"

ln -s /Applications "$stage/Applications"
arch="${rid#osx-}"
hdiutil create -volname "YTStudio" -srcfolder "$stage" -ov -format UDZO "$root/dist/YTStudio-$version-macos-$arch.dmg"
