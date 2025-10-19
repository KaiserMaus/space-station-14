using Content.Server.Medical.Components;
using Content.Server.PowerCell;
using System.Diagnostics.CodeAnalysis;
using Content.Server.AbstractAnalyzer;
using Content.Server.Body.Components;
using Content.Server.Temperature.Components;
using Content.Shared.Body.Components;
using Content.Shared._Sunrise.Research.Artifact;
using Content.Shared.Traits.Assorted;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.MedicalScanner;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Temperature.Components;
using Content.Shared.Traits.Assorted;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Server.GameObjects;

namespace Content.Server.Medical;

public sealed class HealthAnalyzerSystem : AbstractAnalyzerSystem<HealthAnalyzerComponent, HealthAnalyzerDoAfterEvent>
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    /// <inheritdoc/>
    public override void UpdateScannedUser(EntityUid healthAnalyzer, EntityUid target, bool scanMode)
    {
        if (!_uiSystem.HasUi(healthAnalyzer, HealthAnalyzerUiKey.Key))
            return;

        if (!TryComp<DamageableComponent>(target, out var damageableComponent)) // Sunrise-Edit
            return;

        // Sunrise-Start
        if (!TryComp<HealthAnalyzerComponent>(healthAnalyzer, out var healthAnalyzerComp))
            return;

        if (healthAnalyzerComp.DamageContainers is not null &&
            damageableComponent.DamageContainerID is not null &&
            !healthAnalyzerComp.DamageContainers.Contains(damageableComponent.DamageContainerID))
            return;
        // Sunrise-End

        var bodyTemperature = float.NaN;

        if (TryComp<TemperatureComponent>(target, out var temp))
            bodyTemperature = temp.CurrentTemperature;

        var bloodAmount = float.NaN;
        var bleeding = false;
        var unrevivable = false;

        if (TryComp<BloodstreamComponent>(target, out var bloodstream) &&
            _solutionContainerSystem.ResolveSolution(target, bloodstream.BloodSolutionName,
                ref bloodstream.BloodSolution, out var bloodSolution))
        {
            bloodAmount = bloodSolution.FillFraction;
            bleeding = bloodstream.BleedAmount > 0;
        }

        if (TryComp<UnrevivableComponent>(target, out var unrevivableComp) && unrevivableComp.Analyzable)
            unrevivable = true;

        // Collect hunger and thirst data as percentages
        float hungerLevel = -1;
        float thirstLevel = -1;

        if (TryComp<HungerComponent>(target, out var hunger))
        {
            // Calculate hunger as percentage (max hunger is 200.0f from Overfed threshold)
            hungerLevel = (hunger.LastAuthoritativeHungerValue / 200.0f) * 100.0f;
        }

        if (TryComp<ThirstComponent>(target, out var thirst))
        {
            // Calculate thirst as percentage (max thirst is 600.0f from OverHydrated threshold)
            thirstLevel = (thirst.CurrentThirst / 600.0f) * 100.0f;
        }

        // Sunrise edit start - новый триггер
        RaiseLocalEvent(target, new EntityAnalyzedEvent ());
        // Sunrise edit end

        _uiSystem.ServerSendUiMessage(healthAnalyzer, HealthAnalyzerUiKey.Key, new HealthAnalyzerScannedUserMessage(
            GetNetEntity(target),
            bodyTemperature,
            bloodAmount,
            scanMode,
            bleeding,
            unrevivable,
            hungerLevel,
            thirstLevel
        ));
    }

    protected override Enum GetUiKey()
    {
        return HealthAnalyzerUiKey.Key;
    }

    protected override bool ScanTargetPopupMessage(Entity<HealthAnalyzerComponent> uid, AfterInteractEvent args, [NotNullWhen(true)] out string? message)
    {
        message = Loc.GetString("health-analyzer-popup-scan-target", ("user", Identity.Entity(args.User, EntityManager)));
        return true;
    }

    protected override bool ValidScanTarget(EntityUid? target)
    {
        return HasComp<MobStateComponent>(target);
    }
}
