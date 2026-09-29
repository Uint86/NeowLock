namespace NeowLock;

public sealed class NeowLockSettings
{
    // Empty means normal game behavior. The value is a vanilla relic ModelId.Entry.
    public string RelicId { get; set; } = string.Empty;

    // Empty means the game's normal first-act roll; otherwise a native act-selection key.
    public string FirstAct { get; set; } = string.Empty;

    // Empty means the native Ancient reward roll is unrestricted for that act.
    public string SecondActRelicId { get; set; } = string.Empty;
    public string ThirdActRelicId { get; set; } = string.Empty;
}
