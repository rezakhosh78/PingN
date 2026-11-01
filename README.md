<p align="center">
  <img src="PingN/PingN.Desktop/PingN.png" width="112" alt="PingN logo">
</p>

<h1 align="center">PingN</h1>

<p align="center">🛡️ A v2rayN-based desktop proxy client with Cloudflare WARP, Desync, and SNI Spoofing Plus.</p>
<p align="center">🚀 Current source build: <code>v7.25.6-PiN52</code></p>
<p align="center"><a href="README.fa.md">فارسی</a> · <a href="README.md">English</a></p>

---

## ✨ About PingN

**PingN** is a desktop proxy client based on the open-source **v2rayN** project. It keeps the familiar profile, subscription, routing, DNS, system proxy, and TUN workflow, then adds dedicated Cloudflare WARP and connection tools.

🧩 On first launch, PingN creates a **WARP** subscription with **WARP MASQUE/H2** and **WARP AmneziaWG** profiles. Their saved registration credentials or keys are reused on reconnect; pressing **Enter** on a selected WARP profile toggles its connection on or off.

⚡ **Auto** tries a direct connection first, then retries through **Proxy-1** after 8 seconds.

## 💖 Support

**TRON:**  
TUnX9wBWzhD2AK9GqB9FJekKaxCvYJNh18

**Nowpayments:**  
https://nowpayments.io/donation/pingplas

## 📊 PingN compared with V2rayN

The V2rayN column refers to the upstream desktop code used as PingN’s base. It does not describe features that may have appeared in later V2rayN releases.

| Feature | Upstream V2rayN base | PingN |
| --- | --- | --- |
| Profiles and subscriptions | ✅ | ✅ |
| Routing, DNS, system proxy, and TUN | ✅ | ✅ |
| Xray and sing-box cores | ✅ | ✅ |
| WARP profile setup | ❌ | ✅ |
| WARP AmneziaWG | ❌ | ✅ |
| WARP MASQUE/H2 | ❌ | ✅ |
| Auto connection | ❌ | ✅ |
| Desync | ❌ | ✅ |
| SNI Spoofing Plus | ❌ | ✅ |

## 💻 Supported platforms

| Platform | Architectures |
| --- | --- |
| 🪟 Windows | x64, x86, ARM64 |
| 🐧 Linux | x64, ARM64, riscv64, loong64 |
| 🍎 macOS | x64, ARM64 |

PingN’s added integrations have narrower support:

| 🔧 PingN integration | Supported platforms |
| --- | --- |
| 🛡️ WARP AmneziaWG | Windows x64 |
| 🌐 WARP MASQUE/H2 | Windows x64 and ARM64 |
| 🌀 Desync | Windows x64 |
| 🎭 SNI Spoofing Plus | Windows x64 |

⚠️ Desync and SNI Spoofing Plus use WinDivert and request Windows Administrator approval when started. SNI Spoofing Plus applies to standard TLS over TCP; it does not support Reality, MASQUE, WARP WireGuard, or non-TLS profiles.

## 📥 Downloads

Download the package for your operating system and architecture from this repository’s **Releases** page. Release assets are signed; the release includes the public key and detached GPG signatures.

## 📜 License

PingN’s main source is licensed under [GPL-3.0](LICENSE). Bundled components keep their own licenses and notices; see <code>third_party/licenses/</code> and the notices beside each component.

PingN is an independent project based on v2rayN and is not an official v2rayN release. PingNG is the separate Android client.

## 🧰 Projects used

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
