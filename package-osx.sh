#!/bin/bash

Arch="$1"
OutputPath="$2"
Version="$3"

FileName="PingN-${Arch}.zip"
BundleName="v2rayN-${Arch}.zip"
wget -nv -O "$FileName" "https://github.com/2dust/v2rayN-core-bin/raw/refs/heads/master/$BundleName"
7z x "$FileName"
mkdir -p "$OutputPath"
cp -rf "v2rayN-${Arch}/." "$OutputPath/"

PackagePath="PingN-Package-${Arch}"
mkdir -p "$PackagePath/PingN.app/Contents/Resources"
cp -rf "$OutputPath" "$PackagePath/PingN.app/Contents/MacOS"
cp -f "$PackagePath/PingN.app/Contents/MacOS/PingN.icns" "$PackagePath/PingN.app/Contents/Resources/AppIcon.icns"
echo "When this file exists, app will not store configs under this folder" > "$PackagePath/PingN.app/Contents/MacOS/NotStoreConfigHere.txt"
chmod +x "$PackagePath/PingN.app/Contents/MacOS/PingNG"

cat >"$PackagePath/PingN.app/Contents/Info.plist" <<-EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleLocalizations</key>
  <array>
    <string>zh-Hans</string>
    <string>zh-Hant</string>
    <string>en</string>
    <string>fa</string>
    <string>fr</string>
    <string>ru</string>
    <string>hu</string>
  </array>
  <key>CFBundleDisplayName</key>
  <string>PingN</string>
  <key>CFBundleExecutable</key>
  <string>PingNG</string>
  <key>CFBundleIconFile</key>
  <string>AppIcon</string>
  <key>CFBundleIconName</key>
  <string>AppIcon</string>
  <key>CFBundleIdentifier</key>
  <string>2dust.PingN</string>
  <key>CFBundleName</key>
  <string>PingN</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>${Version}</string>
  <key>CSResourcesFileMapped</key>
  <true/>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>LSMinimumSystemVersion</key>
  <string>13.6</string>
</dict>
</plist>
EOF

create-dmg \
    --volname "PingN Installer" \
    --window-size 700 420 \
    --icon-size 100 \
    --icon "PingN.app" 160 185 \
    --hide-extension "PingN.app" \
    --app-drop-link 500 185 \
    "PingN-${Arch}.dmg" \
    "$PackagePath/PingN.app"
