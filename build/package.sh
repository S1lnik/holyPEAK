#!/usr/bin/env bash
# Builds self-contained, ready-to-play packages of Holy Peak.
#
#   build/package.sh [version] [rid ...]
#
# RIDs: win-x64 linux-x64 osx-arm64 osx-x64 (default: all).
# Output goes to dist/: one .zip (Windows, macOS) or .tar.gz (Linux) per platform.
# macOS packages are proper .app bundles; on a Mac they are ad-hoc code signed.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="${1:-2.1.0}"
shift || true
RIDS=("$@")
[ ${#RIDS[@]} -eq 0 ] && RIDS=(win-x64 linux-x64 osx-arm64 osx-x64)

DIST="$ROOT/dist"
STAGE="$DIST/stage"
mkdir -p "$DIST"

publish() {
    local rid="$1" out="$2"
    rm -rf "$out"
    dotnet publish "$ROOT/src/HolyPeak/HolyPeak.csproj" -c Release -r "$rid" --self-contained true \
        -p:Version="$VERSION" -p:DebugType=none -p:DebugSymbols=false -o "$out"
}

for rid in "${RIDS[@]}"; do
    echo "==> $rid"
    case "$rid" in
        win-*)
            dir="$STAGE/$rid/HolyPeak"
            publish "$rid" "$dir"
            cp "$ROOT/build/PLAYING.txt" "$dir/README.txt"
            name="HolyPeak-$VERSION-windows-${rid#win-}.zip"
            (cd "$STAGE/$rid" && rm -f "$DIST/$name" && zip -qr9 "$DIST/$name" HolyPeak)
            ;;
        linux-*)
            dir="$STAGE/$rid/HolyPeak"
            publish "$rid" "$dir"
            cp "$ROOT/build/PLAYING.txt" "$dir/README.txt"
            chmod +x "$dir/HolyPeak"
            name="HolyPeak-$VERSION-linux-${rid#linux-}.tar.gz"
            tar -C "$STAGE/$rid" -czf "$DIST/$name" HolyPeak
            ;;
        osx-*)
            app="$STAGE/$rid/Holy Peak.app"
            rm -rf "$app"
            mkdir -p "$app/Contents/Resources"
            publish "$rid" "$app/Contents/MacOS"
            cp "$ROOT/build/macos/Icon.icns" "$app/Contents/Resources/Icon.icns"
            sed "s/@VERSION@/$VERSION/g" "$ROOT/build/macos/Info.plist" > "$app/Contents/Info.plist"
            chmod +x "$app/Contents/MacOS/HolyPeak"
            if command -v codesign >/dev/null 2>&1; then
                codesign --force --deep --sign - "$app"
            fi
            cp "$ROOT/build/PLAYING.txt" "$STAGE/$rid/README.txt"
            arch="${rid#osx-}"; [ "$arch" = "arm64" ] && arch="apple-silicon" || arch="intel"
            name="HolyPeak-$VERSION-macos-$arch.zip"
            rm -f "$DIST/$name"
            if command -v ditto >/dev/null 2>&1; then
                (cd "$STAGE/$rid" && ditto -c -k --keepParent "Holy Peak.app" "$DIST/$name" && zip -q "$DIST/$name" README.txt)
            else
                (cd "$STAGE/$rid" && zip -qr9y "$DIST/$name" "Holy Peak.app" README.txt)
            fi
            ;;
        *) echo "unknown rid $rid" >&2; exit 1 ;;
    esac
    echo "    $DIST/$name"
done
