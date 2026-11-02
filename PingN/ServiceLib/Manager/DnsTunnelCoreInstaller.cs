using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace ServiceLib.Manager;

/// <summary>Immutable version directories allow updating without overwriting a running core.</summary>
public static class DnsTunnelCoreInstaller
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private static string Root(string kind) => Path.Combine(Utils.GetConfigPath(), "DnsTunnelCores", kind);
    public static string? InstalledPath(string kind)
    {
        var pointer = Path.Combine(Root(kind), "current.txt");
        if (!File.Exists(pointer))
        {
            var bundled = Path.Combine(AppContext.BaseDirectory, "bin", "dnstunnels", kind,
                kind == "masterdns" ? "masterdnsvpn-client.exe" : "dnstt-client.exe");
            return File.Exists(bundled) ? bundled : null;
        }
        var version = File.ReadAllText(pointer).Trim();
        if (!Regex.IsMatch(version, "^[a-fA-F0-9]{64}$")) return null;
        var path = Path.Combine(Root(kind), version, kind == "masterdns" ? "masterdnsvpn-client.exe" : "dnstt-client.exe");
        return File.Exists(path) ? path : null;
    }

    public static Dictionary<string, string> InstalledMasterDefaults()
    {
        var exe = InstalledPath("masterdns");
        var sample = exe is null ? null : Path.Combine(Path.GetDirectoryName(exe)!, "client_config.toml.simple");
        return sample is not null && File.Exists(sample)
            ? DnsTunnelSettings.ReadMasterFields(File.ReadAllText(sample)) : [];
    }

    private sealed record ClientRelease(string Url, string Expected, string Version);
    public sealed record ClientUpdateInfo(string Version, bool HasUpdate);

    public static string ClientAssetName(string kind, Architecture architecture)
    {
        var arch = architecture switch
        {
            Architecture.X64 => "amd64", Architecture.X86 => "386", Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException("Unsupported Windows CPU architecture.")
        };
        return kind switch
        {
            "masterdns" => $"MasterDnsVPN_Client_Windows_{(arch == "386" ? "X86" : arch.ToUpperInvariant())}.zip",
            "dnstt-ssh" => $"dnstt-client-windows-{arch}.exe",
            _ => throw new ArgumentException("Unknown DNS client.")
        };
    }

    public static string SelectMasterClientEntry(IEnumerable<string> entryNames, Architecture architecture)
    {
        var releaseName = Path.GetFileNameWithoutExtension(ClientAssetName("masterdns", architecture));
        var allowed = new[] { "masterdnsvpn-client.exe", releaseName + ".exe", releaseName };
        var versioned = "^" + Regex.Escape(releaseName) + @"_v[0-9][A-Za-z0-9._-]*\.exe$";
        var matches = entryNames.Where(path => allowed.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            || Regex.IsMatch(Path.GetFileName(path), versioned, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).ToList();
        if (matches.Count != 1) throw new IOException("Expected one MasterDNS client executable; archive entries: " + string.Join(", ", entryNames.Select(Path.GetFileName)));
        return matches[0];
    }

    private static string? InstalledArchiveHash(string kind)
    {
        var path = InstalledPath(kind);
        if (path is null) return null;
        var folder = Path.GetDirectoryName(path)!;
        if (Regex.IsMatch(Path.GetFileName(folder), "^[a-fA-F0-9]{64}$")) return Path.GetFileName(folder);
        var source = Path.Combine(folder, "SOURCE.json");
        if (!File.Exists(source)) return null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(source));
            var key = kind == "masterdns" ? "archiveSha256" : "sha256";
            return document.RootElement.TryGetProperty(key, out var digest) ? digest.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    public static string InstalledVersion(string kind)
    {
        var path = InstalledPath(kind);
        if (path is null) return "Client not installed";
        var versionFile = Path.Combine(Path.GetDirectoryName(path)!, "version.txt");
        return File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "Bundled client";
    }

    public static async Task<ClientUpdateInfo> CheckOnlyAsync(string kind)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DNS clients currently support Windows.");
        var release = await ResolveClientReleaseAsync(kind);
        var current = InstalledPath(kind);
        var matches = current is not null && string.Equals(InstalledArchiveHash(kind), release.Expected, StringComparison.OrdinalIgnoreCase);
        return new ClientUpdateInfo(release.Version, !matches);
    }

    private static async Task<ClientRelease> ResolveClientReleaseAsync(string kind)
    {
        if (kind is not ("masterdns" or "dnstt-ssh")) throw new ArgumentException("Unknown DNS client.");
        var assetName = ClientAssetName(kind, RuntimeInformation.OSArchitecture);
        var repository = kind == "masterdns" ? "masterking32/MasterDnsVPN" : "NNdroid/dnstt";
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repository}/releases/latest");
        request.Headers.UserAgent.ParseAdd("PingN/1.0");
        using var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var release = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var asset = release.RootElement.GetProperty("assets").EnumerateArray()
            .FirstOrDefault(x => string.Equals(x.GetProperty("name").GetString(), assetName, StringComparison.OrdinalIgnoreCase));
        if (asset.ValueKind == JsonValueKind.Undefined) throw new IOException($"No matching Windows client asset: {assetName}");
        var url = asset.GetProperty("browser_download_url").GetString()!;
        var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
        if (digest is null || !Regex.IsMatch(digest, "^sha256:[a-fA-F0-9]{64}$"))
            throw new IOException("The client publisher has not provided a valid SHA256 digest. Existing client was retained.");
        return new ClientRelease(url, digest[7..], release.RootElement.GetProperty("tag_name").GetString() ?? digest);
    }

    public static async Task<string> EnsureAsync(string kind)
        => InstalledPath(kind) ?? await UpdateAsync(kind);

    public static async Task<string> UpdateAsync(string kind)
    {
        if (kind is not ("masterdns" or "dnstt-ssh")) throw new ArgumentException("Unknown DNS core.");
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DNS tunnel core installer currently supports Windows.");
        await Gate.WaitAsync();
        string? staging = null;
        try
        {
            Directory.CreateDirectory(Root(kind));
            staging = Path.Combine(Root(kind), "staging-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            var release = await ResolveClientReleaseAsync(kind);
            var (url, expected, version) = release;
            var current = InstalledPath(kind);
            if (current is not null && string.Equals(InstalledArchiveHash(kind), expected, StringComparison.OrdinalIgnoreCase))
            {
                NoticeManager.Instance.Enqueue($"{kind}: already up to date ({version}).");
                return current;
            }
            var download = Path.Combine(staging, "download");
            using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var output = File.Create(download);
                await response.Content.CopyToAsync(output);
            }
            var actual = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(download)));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("Core checksum mismatch. Existing installation was retained.");
            var targetName = kind == "masterdns" ? "masterdnsvpn-client.exe" : "dnstt-client.exe";
            if (kind == "masterdns")
            {
                using var zip = ZipFile.OpenRead(download);
                var entryName = SelectMasterClientEntry(zip.Entries.Select(x => x.FullName), RuntimeInformation.OSArchitecture);
                var executable = zip.GetEntry(entryName)!;
                executable.ExtractToFile(Path.Combine(staging, targetName));
                var sample = zip.Entries.FirstOrDefault(x => x.Name == "client_config.toml.simple")
                    ?? zip.Entries.FirstOrDefault(x => x.Name == "client_config.toml");
                sample?.ExtractToFile(Path.Combine(staging, "client_config.toml.simple"));
                foreach (var name in new[] { "client_resolvers.simple", "LICENSE" })
                {
                    var entry = zip.Entries.FirstOrDefault(x => x.Name == name);
                    entry?.ExtractToFile(Path.Combine(staging, name));
                }
                File.Delete(download);
            }
            else File.Move(download, Path.Combine(staging, targetName));
            // Reject HTML/error payloads before changing the active version.
            using (var exe = File.OpenRead(Path.Combine(staging, targetName)))
                if (exe.ReadByte() != 'M' || exe.ReadByte() != 'Z') throw new IOException("Downloaded file is not a Windows executable.");
            await File.WriteAllTextAsync(Path.Combine(staging, "version.txt"), version);
            var finalDirectory = Path.Combine(Root(kind), actual.ToLowerInvariant());
            if (!Directory.Exists(finalDirectory)) Directory.Move(staging, finalDirectory);
            var pointer = Path.Combine(Root(kind), "current.txt");
            await File.WriteAllTextAsync(pointer + ".new", actual.ToLowerInvariant());
            File.Move(pointer + ".new", pointer, true);
            NoticeManager.Instance.Enqueue($"{kind}: installed / up to date ({version}).");
            return Path.Combine(finalDirectory, targetName);
        }
        finally
        {
            if (staging is not null && Directory.Exists(staging)) Directory.Delete(staging, true);
            Gate.Release();
        }
    }
}
