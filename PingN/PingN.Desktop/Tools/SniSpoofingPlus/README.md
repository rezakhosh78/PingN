# SNI Spoofing Plus integration

PingN runs the SNI Spoofing Plus **CLI** as a per-profile local TCP proxy. Settings live in each Xray profile editor; disabling the profile switch stops using this tool for that profile. The standalone GUI has been removed.

## Requirements

- Windows x64 and an Xray profile using standard TLS over a TCP-based transport. Reality, WARP WireGuard, MASQUE, and non-TLS profiles are not supported.
- Administrator approval when PingN starts the proxy.
- The official WinDivert 2.2.2 x64 `WinDivert.dll` and `WinDivert64.sys` are included next to the CLI executable in this folder. WinDivert is dual-licensed under LGPL-3.0 or GPL-2.0; see [WinDivert-LICENSE.txt](WinDivert-LICENSE.txt).

PingN rewrites only the selected profile's primary Xray outbound endpoint to a loopback listener while the proxy connects to the original server address. Fake SNI names and the uTLS fingerprint are stored separately on each profile.

The bundled Windows x64 CLI is `sni-spoofing-plus-windows-amd64.exe`. Its upstream project documents flags and supported fingerprints at <https://github.com/aleskxyz/sni-spoofing-go>. The executable is distributed under GPL-3.0; see [LICENSE](LICENSE).
