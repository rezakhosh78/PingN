<p align="center">
  <img src="PingN/PingN.Desktop/PingN.png" width="112" alt="PingN logo">
</p>

<h1 align="center">PingN</h1>

<p align="center">A V2rayN-based desktop proxy client with WARP, Desync, and SNI additions.</p>
<p align="center"><a href="README.fa.md">فارسی</a> · <a href="README.md">English</a></p>

---

## About PingN

**PingN** is a desktop proxy client based on the open-source **v2rayN** project. It keeps the familiar profile, subscription, routing, DNS, system proxy, and TUN workflow, then adds dedicated Cloudflare WARP and connection tools.

## Support
TRON:
TUnX9wBWzhD2AK9GqB9FJekKaxCvYJNh18

## PingN compared with V2rayN

The V2rayN column refers to the upstream desktop code used as PingN’s base. It does not describe features that may have appeared in later V2rayN releases.

| Feature | Upstream V2rayN base | PingN |
| --- | --- | --- |
| Profile and subscription management | Included | Inherited from V2rayN |
| Routing, DNS, system proxy, and TUN | Included | Inherited from V2rayN |
| Xray and sing-box cores | Supported | Supported |
| WARP AmneziaWG | No PingN-specific integration in the base | Oberon integration; IPv4/IPv6 endpoint scans, fast or expanded scan, and a per-profile automatic-scan switch. Local SOCKS5 mode carries TCP. |
| WARP MASQUE/H2 | No PingN-specific integration in the base | usque integration; endpoint scanning and a per-profile automatic-scan switch. Uses a dedicated local SOCKS5 port, preferring <code>20808</code> and choosing another free port if needed, separate from PingN’s main <code>10808</code> listener. |
| Desync | No PingN-specific integration in the base | Per-profile presets and custom settings, plus a scanner for candidate settings. |
| SNI Spoofing Plus | No PingN-specific integration in the base | Per-profile Fake SNI and uTLS settings for standard Xray TLS-over-TCP profiles. |

## Supported platforms

| Platform | Architectures |
| --- | --- |
| Windows | x64, x86, ARM64 |
| Linux | x64, ARM64, riscv64, loong64 |
| macOS | x64, ARM64 |

PingN’s added integrations have narrower support:

| PingN integration | Supported platforms |
| --- | --- |
| WARP AmneziaWG | Windows x64 |
| WARP MASQUE/H2 | Windows x64 and ARM64 |
| Desync | Windows x64 |
| SNI Spoofing Plus | Windows x64 |

Desync and SNI Spoofing Plus use WinDivert and request Windows Administrator approval when started. SNI Spoofing Plus applies to standard TLS over TCP; it does not support Reality, MASQUE, WARP WireGuard, or non-TLS profiles.

## Downloads

Download the package for your operating system and architecture from this repository’s **Releases** page. Release assets are signed; the release includes the public key and detached GPG signatures.

## License

PingN’s main source is licensed under [GPL-3.0](LICENSE). Bundled components keep their own licenses and notices; see <code>third_party/licenses/</code> and the notices beside each component.

PingN is an independent project based on v2rayN and is not an official v2rayN release. PingNG is the separate Android client.

## Projects used

| Project | Use in PingN |
| --- | --- |
| [2dust/v2rayN](https://github.com/2dust/v2rayN) | Desktop client foundation and inherited profile, routing, and core-management features. |
| [XTLS/Xray-core](https://github.com/XTLS/Xray-core) | Supported proxy core. |
| [SagerNet/sing-box](https://github.com/SagerNet/sing-box) | Supported proxy core. |
| [oberon-core/oberon](https://github.com/oberon-core/oberon) and [AmneziaWG](https://github.com/amnezia-vpn/amneziawg-go) | WARP AmneziaWG profile creation, endpoint scanning, and local proxy runtime. |
| [Diniboy1123/usque](https://github.com/Diniboy1123/usque) | WARP MASQUE/H2 runtime. |
| [rezakhosh78/BlackHole](https://github.com/rezakhosh78/BlackHole) and [bol-van/zapret2](https://github.com/bol-van/zapret2) | Source of bundled Windows Desync runtime files. BlackHole’s GUI and application source are not copied into PingN. |
| [aleskxyz/sni-spoofing-go](https://github.com/aleskxyz/sni-spoofing-go) | CLI used by the SNI Spoofing Plus integration. |
| [basil00/WinDivert](https://github.com/basil00/WinDivert) | Packet-diversion driver used by the Windows Desync and SNI tools. |
| [vernette/warpscout](https://github.com/vernette/warpscout) | WARP IPv4 and IPv6 endpoint-pool definitions used by Oberon’s scanner. |
| [Avalonia UI](https://github.com/AvaloniaUI/Avalonia), [Semi.Avalonia](https://github.com/irihitech/Semi.Avalonia), and [ReactiveUI](https://github.com/reactiveui/ReactiveUI) | Cross-platform desktop UI framework and components. |
| [Loyalsoldier/v2ray-rules-dat](https://github.com/Loyalsoldier/v2ray-rules-dat), [2dust/sing-box-rules](https://github.com/2dust/sing-box-rules), and [MetaCubeX/meta-rules-dat](https://github.com/MetaCubeX/meta-rules-dat) | Routing rules and GeoIP/geosite data. |
| [rezakhosh78/PingNG](https://github.com/rezakhosh78/PingNG) | WARP behavior reference for the mobile client; PingNG app source is not copied into PingN. |

