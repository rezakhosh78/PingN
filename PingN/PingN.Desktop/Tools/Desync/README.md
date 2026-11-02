# Bundled Desync runtime

PingN ships the complete Windows x64 `winws2` runtime in this folder. The project file copies this folder into the publish output at `Tools/Desync`, where the per-profile Desync launcher expects it.

Included files:

- `winws2.exe`
- `WinDivert.dll` and `WinDivert64.sys`
- `cygwin1.dll`
- `lua/engine-lib.lua` and `lua/engine-antidpi.lua`
- `desync-host.ps1` elevation helper

The first time Desync starts, Windows asks for Administrator permission so WinDivert can capture and inject packets. The feature is available on Windows x64 for Xray TCP-based TLS and Reality profiles. Presets use fixed values; choose `Custom` to edit split, fooling, Bad Sequence, repeats, Fake SNI, or AutoTTL values.

Third-party provenance and license notices are in `THIRD-PARTY-NOTICES.txt` and the adjacent license files.
