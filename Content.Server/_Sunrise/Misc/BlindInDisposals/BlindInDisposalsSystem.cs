using Content.Server.Disposal.Unit;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Flash;
using Content.Shared.StatusEffect;
using Content.Shared.Starlight.Medical.Surgery.Steps.Parts;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Misc.BlindInDisposals;

/// <summary>
/// Blocks normal vision while an entity is travelling through disposal tubes.
/// </summary>
public sealed class BlindInDisposalsSystem : EntitySystem
{
    [Dependency] private readonly BlindableSystem _blindable = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedFlashSystem _flash = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan FlashInterval = TimeSpan.FromSeconds(6);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BeingDisposedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BeingDisposedComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<BeingDisposedComponent, CanSeeAttemptEvent>(OnCanSee);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveDisposalsFlashComponent, BeingDisposedComponent>();
        while (query.MoveNext(out var uid, out var flashComp, out _))
        {
            if (_timing.CurTime < flashComp.NextFlashTime)
                continue;

            ApplyFlash(uid);
            flashComp.NextFlashTime = _timing.CurTime + FlashInterval;
        }
    }

    private void OnStartup(Entity<BeingDisposedComponent> ent, ref ComponentStartup args)
    {
        if (!HasComp<BlindableComponent>(ent))
            return;

        if (!HasEyes(ent.Owner))
        {
            StartFlashing(ent.Owner);
            return;
        }

        _blindable.UpdateIsBlind(ent.Owner);
    }

    private void OnRemove(Entity<BeingDisposedComponent> ent, ref ComponentRemove args)
    {
        if (!HasComp<BlindableComponent>(ent))
            return;

        RemComp<ActiveDisposalsFlashComponent>(ent);

        if (!HasEyes(ent.Owner))
            return;

        _blindable.UpdateIsBlind(ent.Owner);
    }

    private void OnCanSee(Entity<BeingDisposedComponent> ent, ref CanSeeAttemptEvent args)
    {
        if (!HasComp<BlindableComponent>(ent))
            return;

        if (!HasEyes(ent.Owner))
            return;

        args.Cancel();
    }

    private bool HasEyes(EntityUid uid)
    {
        if (!TryComp<BodyComponent>(uid, out var body))
            return true;

        return _body.GetBodyOrganEntityComps<OrganEyesComponent>((uid, body)).Count > 0;
    }

    private void StartFlashing(EntityUid uid)
    {
        if (!_statusEffects.CanApplyEffect(uid, _flash.FlashedKey))
            return;

        ApplyFlash(uid);
        var flashComp = EnsureComp<ActiveDisposalsFlashComponent>(uid);
        flashComp.NextFlashTime = _timing.CurTime + FlashInterval;
    }

    private void ApplyFlash(EntityUid uid)
    {
        _flash.Flash(
            target: uid,
            user: null,
            used: null,
            flashDuration: FlashInterval,
            slowTo: 0.8f,
            displayPopup: false);
    }
}
