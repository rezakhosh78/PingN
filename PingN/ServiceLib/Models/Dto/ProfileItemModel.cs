namespace ServiceLib.Models.Dto;

[Serializable]
public partial class ProfileItemModel : ReactiveObject
{
    public PsiphonConnectionState Psiphon { get; set; } = new();
    public bool IsActive { get; set; }
    public string IndexId { get; set; }
    public EConfigType ConfigType { get; set; }
    public string ConfigTypeLabel { get; set; }
    public string Remarks { get; set; }
    public string? ProtoExtra { get; set; }
    public bool DesyncEnabled { get; set; }
    public bool SniSpoofingEnabled { get; set; }
    public string Mode => string.Join(" · ", new[] { DesyncEnabled ? "Desync" : null, SniSpoofingEnabled ? "SNI Spoofing" : null }.Where(value => value is not null));
    public string Address { get; set; }
    public string AddressDisplay { get; set; }
    public string RemarksDisplay => IsActive ? $"{Remarks} [{ResUI.TipActiveServer}]" : Remarks;
    public int Port { get; set; }
    public string Network { get; set; }
    public string StreamSecurity { get; set; }
    public string Subid { get; set; }
    public string SubRemarks { get; set; }
    public int Sort { get; set; }
    [Reactive]
    public partial int Delay { get; set; }

    public decimal Speed { get; set; }

    [Reactive]
    public partial string DelayVal { get; set; }

    [Reactive]
    public partial string SpeedVal { get; set; }

    [Reactive]
    public partial string IpInfo { get; set; }

    [Reactive]
    public partial string TodayUp { get; set; }

    [Reactive]
    public partial string TodayDown { get; set; }

    [Reactive]
    public partial string TotalUp { get; set; }

    [Reactive]
    public partial string TotalDown { get; set; }

    public string GetSummary()
    {
        var summary = $"[{(ConfigTypeLabel.IsNotEmpty() ? ConfigTypeLabel : ConfigType.ToString())}] {Remarks}";
        if (!ConfigType.IsComplexType())
        {
            summary += $"({Address}:{Port})";
        }

        return summary;
    }
}
