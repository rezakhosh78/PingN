# PingN

### PingN is a desktop client built from the established upstream codebase, with Cloudflare WARP, Desync, and SNI Spoofing Plus integrations.

The client supports Windows, Linux, and macOS, including [Xray](https://github.com/XTLS/Xray-core), [sing-box](https://github.com/SagerNet/sing-box), and the other core options supported by the upstream desktop client. PingN's bundled Xray, Desync, and SNI Spoofing Plus runtimes target Windows x64.

---

## Download / 下载

PingN does not have a published release yet. Follow the upstream build instructions to build PingN. A Windows x64 publish includes the bundled Xray core and the Desync/SNI runtime files.

برای PingN هنوز نسخهٔ منتشرشده‌ای وجود ندارد. برای ساخت از دستورهای PingN استفاده کنید؛ خروجی Windows x64 شامل هستهٔ Xray و فایل‌های runtime مربوط به Desync و SNI است.


> [!TIP]
> PingN is the desktop version. For the mobile version, please visit the PingNG \
> PingN 是电脑版，手机版请访问 PingNG
>
> https://github.com/2dust/v2rayNG

---

## Documentation / 使用文档

Read the Wiki for usage guides and configuration details.

请阅读 Wiki 获取使用说明和配置教程。

[https://github.com/2dust/v2rayN/wiki](https://github.com/2dust/v2rayN/wiki)

## PingN additions

- The app display version is `PingN-v7.25.6-PiN52`; its numeric release version remains `7.25.6` for update checks.
- The **Add [WARP AmneziaWG]** menu creates an Oberon-backed profile. Its editor lets you choose IPv4 or IPv6, a fast or expanded endpoint scan, and whether to scan automatically when connecting. All three choices are saved with the profile. Oberon exposes a local SOCKS5 listener, and PingN routes TCP through it. UDP relay is not available in Oberon's local proxy mode.
- The **Add [WARP MASQUE/H2]** editor also saves whether endpoint scanning runs automatically when connecting; the default remains enabled.
- The updated Oberon v0.4.5 source is included under `Oberon/`. Run `powershell -ExecutionPolicy Bypass -File .\Build-Oberon.ps1` with Go 1.25 or later before publishing to build the Windows x64 executable into `PingN/PingN.Desktop/BundledCores/Oberon`. This rebuild is required for the editor's automatic-scan-off option (`-no-scan`) and IPv6 scanning; the bundled executable remains v0.4.3 until the script is run.
- Desync profiles and advanced values are stored per profile. Preset values are fixed; `Custom` exposes split, fooling, Bad Sequence, repeats, Fake SNI, and AutoTTL settings. The bundled `winws2` runtime supports Xray TLS/Reality TCP traffic and requests Administrator approval.
- **SNI Spoofing Plus** has a separate per-profile switch, Fake SNI list, and uTLS fingerprint in both standard and custom Xray profile editors. It runs the bundled CLI through a local TCP listener for standard TLS profiles and requests Administrator approval. The official WinDivert 2.2.2 x64 DLL and driver are next to the CLI; see `WinDivert-LICENSE.txt` and [`Tools/SniSpoofingPlus/README.md`](PingN/PingN.Desktop/Tools/SniSpoofingPlus/README.md).
- Windows x64 publish bundles Xray core `26.10.21`, `wintun.dll`, and the GeoIP/geosite databases in the paths used by the existing core manager. Other target platforms continue to use PingN's core installation flow.
- Desync runtime components are included under [`Tools/Desync`](PingN/PingN.Desktop/Tools/Desync/README.md), with third-party notices and license texts beside them.

---

## Supported Platforms / 支持平台

| Platform / 平台 | x64 | x86 | arm64 | riscv64 | loong64 |
| --- | --- | --- | --- | --- | --- |
| Windows | ✅ | ✅ | ✅ | - | - |
| Linux | ✅ | - | ✅ | ✅ | ✅ |
| macOS | ✅ | - | ✅ | - | - |

Minimum OS requirements: [Release files introduction](https://github.com/2dust/v2rayN/wiki/Release-files-introduction) / 最低系统要求：[发布文件介绍](https://github.com/2dust/v2rayN/wiki/Release-files-introduction)

---

## GPG Verification / GPG 签名校验

Release files are signed with GPG to verify authenticity and integrity, helping prevent mirror, ISP, or CDN hijacking.

发布文件已使用 GPG 签名，可用于校验文件真实性与完整性，预防镜像站、运营商或 CDN 劫持。

### Fingerprint / 公钥指纹

```text
7694 5E9F 3E9A 168F 8070 F195 805D 661C
134D FAF6 8903 C199 463C 31E5 AE90 3AE0
```

---

## Community / 社区

Telegram Group / Telegram 群组：

[https://t.me/v2rayN](https://t.me/v2rayN)

Telegram Channel / Telegram 频道：

[https://t.me/github_2dust](https://t.me/github_2dust)
