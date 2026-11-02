namespace ServiceLib.Models.Dto;

/// <summary>One live state per profile; the row keeps this reference across refreshes.</summary>
public partial class PsiphonConnectionState : ReactiveObject
{
    [Reactive] public partial string Text { get; set; } = "Disconnect";
    [Reactive] public partial string Color { get; set; } = "#DC2626";
}
