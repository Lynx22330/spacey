using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusIcon;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Nutrition.EntitySystems;

[UsedImplicitly]
public sealed class TonicSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedJetpackSystem _jetpack = default!;

    [ValidatePrototypeId<StatusIconPrototype>]
    private const string TonicIconLushId = "TonicIconLush";

    [ValidatePrototypeId<StatusIconPrototype>]
    private const string TonicIconNormalId = "TonicIconNormal";

    [ValidatePrototypeId<StatusIconPrototype>]
    private const string TonicIconScarceId = "TonicIconScarce";

    private StatusIconPrototype? _tonicIconLush = null;
    private StatusIconPrototype? _tonicIconNormal = null;
    private StatusIconPrototype? _tonicIconScarce = null;

    public override void Initialize()
    {
        base.Initialize();

        DebugTools.Assert(_prototype.TryIndex(TonicIconLushId, out _tonicIconLush) &&
                          _prototype.TryIndex(TonicIconNormalId, out _tonicIconNormal) &&
                          _prototype.TryIndex(TonicIconScarceId, out _tonicIconScarce));

        SubscribeLocalEvent<TonicComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<TonicComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TonicComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnMapInit(EntityUid uid, TonicComponent component, MapInitEvent args)
    {
        // Do not change behavior unless starting value is explicitly defined
        if (component.CurrentTonicLevels < 0)
        {
            component.CurrentTonicLevels = _random.Next(
                (int) component.TonicThresholds[TonicThreshold.Normal] + 10,
                (int) component.TonicThresholds[TonicThreshold.Lush] - 1);
        }
        component.NextUpdateTime = _timing.CurTime;
        component.CurrentTonicThreshold = GetTonicThreshold(component, component.CurrentTonicLevels);
        component.LastTonicThreshold = TonicThreshold.Lush; // TODO: Potentially change this -> Used Lush because no effects.
        // TODO: Check all thresholds make sense and throw if they don't.
        UpdateEffects(uid, component);

        TryComp(uid, out MovementSpeedModifierComponent? moveMod);
            _movement.RefreshMovementSpeedModifiers(uid, moveMod);
    }

    private void OnRefreshMovespeed(EntityUid uid, TonicComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        // TODO: This should really be taken care of somewhere else
        if (_jetpack.IsUserFlying(uid))
            return;

        var mod = component.CurrentTonicThreshold <= TonicThreshold.Scarce ? 0.75f : 1.0f;
        args.ModifySpeed(mod, mod);
    }

    private void OnRejuvenate(EntityUid uid, TonicComponent component, RejuvenateEvent args)
    {
        SetTonic(uid, component, component.TonicThresholds[TonicThreshold.Lush]);
    }

    private TonicThreshold GetTonicThreshold(TonicComponent component, float amount)
    {
        TonicThreshold result = TonicThreshold.Dead;
        var value = component.TonicThresholds[TonicThreshold.Lush];
        foreach (var threshold in component.TonicThresholds)
        {
            if (threshold.Value <= value && threshold.Value >= amount)
            {
                result = threshold.Key;
                value = threshold.Value;
            }
        }

        return result;
    }

    public void ModifyTonic(EntityUid uid, TonicComponent component, float amount)
    {
        SetTonic(uid, component, component.CurrentTonicLevels + amount);
    }

    public void SetTonic(EntityUid uid, TonicComponent component, float amount)
    {
        component.CurrentTonicLevels = Math.Clamp(amount,
            component.TonicThresholds[TonicThreshold.Dead],
            component.TonicThresholds[TonicThreshold.Lush]
        );
        Dirty(uid, component);
    }

    private bool IsMovementThreshold(TonicThreshold threshold)
    {
        switch (threshold)
        {
            case TonicThreshold.Dead:
            case TonicThreshold.Scarce:
                return true;
            case TonicThreshold.Normal:
            case TonicThreshold.Lush:
            case TonicThreshold.OverDosed:
                return false;
            default:
                throw new ArgumentOutOfRangeException(nameof(threshold), threshold, null);
        }
    }

    public bool TryGetStatusIconPrototype(TonicComponent component, out StatusIconPrototype? prototype)
    {
        switch (component.CurrentTonicThreshold)
        {
            case TonicThreshold.Lush:
                prototype = _tonicIconLush;
                return true;

            case TonicThreshold.Normal:
                prototype = _tonicIconNormal;
                return true;

            case TonicThreshold.Scarce:
                prototype = _tonicIconScarce;
                return true;

            default:
                prototype = null;
                return false;
        }
    }

    private void UpdateEffects(EntityUid uid, TonicComponent component)
    {
        if (IsMovementThreshold(component.LastTonicThreshold) != IsMovementThreshold(component.CurrentTonicThreshold) &&
                TryComp(uid, out MovementSpeedModifierComponent? movementSlowdownComponent))
        {
            _movement.RefreshMovementSpeedModifiers(uid, movementSlowdownComponent);
        }

        // Update UI
        if (TonicComponent.TonicThresholdAlertTypes.TryGetValue(component.CurrentTonicThreshold, out var alertId))
        {
            _alerts.ShowAlert(uid, alertId);
        }
        else
        {
            _alerts.ClearAlertCategory(uid, component.TonicCategory);
        }

        switch (component.CurrentTonicThreshold)
        {
            case TonicThreshold.OverDosed:
                component.LastTonicThreshold = component.CurrentTonicThreshold;
                component.ActualDecayRate = component.BaseDecayRate * 2f;
                return;

            case TonicThreshold.Lush:
                component.LastTonicThreshold = component.CurrentTonicThreshold;
                component.ActualDecayRate = component.BaseDecayRate;
                return;

            case TonicThreshold.Normal:
                component.LastTonicThreshold = component.CurrentTonicThreshold;
                component.ActualDecayRate = component.BaseDecayRate * 1.2f;
                return;
            case TonicThreshold.Scarce:
                _movement.RefreshMovementSpeedModifiers(uid);
                component.LastTonicThreshold = component.CurrentTonicThreshold;
                component.ActualDecayRate = component.BaseDecayRate * 0.6f;
                return;

            case TonicThreshold.Dead:
                return;

            default:
                Log.Error($"No tonic threshold found for {component.CurrentTonicThreshold}");
                throw new ArgumentOutOfRangeException($"No tonic threshold found for {component.CurrentTonicThreshold}");
        }
    }
    private void DoContinuousTonicEffects(EntityUid uid, TonicComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.CurrentTonicThreshold <= TonicThreshold.Dead &&
            component.TonicDamage is { } damage &&
            !_mobState.IsDead(uid))
        {
            _damageable.TryChangeDamage(uid, damage, true, false);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TonicComponent>();
        while (query.MoveNext(out var uid, out var tonic))
        {
            if (_timing.CurTime < tonic.NextUpdateTime)
                continue;

            tonic.NextUpdateTime += tonic.UpdateRate;

            ModifyTonic(uid, tonic, -tonic.ActualDecayRate);
            DoContinuousTonicEffects(uid, tonic);
            var calculatedTonicThreshold = GetTonicThreshold(tonic, tonic.CurrentTonicLevels);

            if (calculatedTonicThreshold == tonic.CurrentTonicThreshold)
                continue;

            tonic.CurrentTonicThreshold = calculatedTonicThreshold;
            UpdateEffects(uid, tonic);
        }
    }
}
