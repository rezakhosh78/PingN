namespace ServiceLib.Models.Dto;

[Serializable]
public sealed record PsiphonRegionOption(string Code, string Name)
{
    public static IReadOnlyList<PsiphonRegionOption> All { get; } =
    [
        new("", "Automatic (fastest)"),
        new("AU", "Australia"),
        new("AT", "Austria"),
        new("BE", "Belgium"),
        new("BR", "Brazil"),
        new("BG", "Bulgaria"),
        new("CA", "Canada"),
        new("HR", "Croatia"),
        new("CZ", "Czech Republic"),
        new("DK", "Denmark"),
        new("EE", "Estonia"),
        new("FI", "Finland"),
        new("FR", "France"),
        new("DE", "Germany"),
        new("HU", "Hungary"),
        new("IN", "India"),
        new("IE", "Ireland"),
        new("IT", "Italy"),
        new("JP", "Japan"),
        new("LV", "Latvia"),
        new("NL", "Netherlands"),
        new("NO", "Norway"),
        new("PL", "Poland"),
        new("PT", "Portugal"),
        new("RO", "Romania"),
        new("RS", "Serbia"),
        new("SG", "Singapore"),
        new("SK", "Slovakia"),
        new("ES", "Spain"),
        new("SE", "Sweden"),
        new("CH", "Switzerland"),
        new("UA", "Ukraine"),
        new("GB", "United Kingdom"),
        new("US", "United States"),
    ];
}
