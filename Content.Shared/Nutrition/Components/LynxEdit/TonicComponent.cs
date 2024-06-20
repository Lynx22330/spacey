using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Tonic.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Tonic.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(TonicSystem))]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class TonicComponent : Component
{
    // Base stuff
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("baseDecayRate")]
    [AutoNetworkedField]
    public float BaseDecayRate = 0.1f;

    [ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float ActualDecayRate;

    [DataField]
    public string Solution = "tonic";

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public TonicThreshold CurrentTonicThreshold;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public TonicThreshold LastTonicThreshold;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("startingTonicLevels")]
    [AutoNetworkedField]
    public float CurrentTonicLevels = -1f;

    /// <summary>
    /// The time when the hunger will update next.
    /// </summary>
    [DataField("nextUpdateTime", customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan NextUpdateTime;

    /// <summary>
    /// Damage dealt when your current threshold is at TonicThreshold.Dead
    /// (TEMPORARY, MUST REPLACE WITH A DEBUFF INSTEAD OF DAMAGE.)
    /// </summary>
    [DataField("tonicDamage")]
    public DamageSpecifier? TonicDamage;

    /// <summary>
    /// The time between each update.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(1);

    [DataField("thresholds")]
    [AutoNetworkedField]
    public Dictionary<TonicThreshold, float> TonicThresholds = new()
    {
        {TonicThreshold.OverDosed, 150.0f},
        {TonicThreshold.Lush, 100.0f},
        {TonicThreshold.Normal, 75.0f},
        {TonicThreshold.Scarce, 50.0f},
        {TonicThreshold.Dead, 0.0f},
    };

    [DataField]
    public ProtoId<AlertCategoryPrototype> TonicCategory = "Tonic";

    public static readonly Dictionary<TonicThreshold, ProtoId<AlertPrototype>> TonicThresholdAlertTypes = new()
    {
        {TonicThreshold.OverDosed, "OverDosed"},
        {TonicThreshold.Lush, "Lush"},
        {TonicThreshold.Normal, "Normal"},
        {TonicThreshold.Scarce, "Scarce"},
        {TonicThreshold.Dead, "Scarce"},
    };
}

[Flags]
public enum TonicThreshold : byte
{
    // Tonic lovers unite! Idk lol
    Dead = 0,
    Scarce = 1 << 0,
    Normal = 1 << 1,
    Lush = 1 << 2,
    OverDosed = 1 << 3,
}
