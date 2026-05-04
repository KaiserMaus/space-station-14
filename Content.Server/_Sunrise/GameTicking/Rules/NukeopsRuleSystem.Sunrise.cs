using Content.Server.AlertLevel;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.NukeOps;
using Robust.Shared.Timing;

namespace Content.Server.GameTicking.Rules;

public sealed partial class NukeopsRuleSystem
{
    // Sunrise-Start
    [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    // Sunrise-End

    // Sunrise-Start: auto set martial law after war declaration delay.
    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<NukeopsRuleComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (!comp.CanChangeAlertLevel)
                continue;

            if (_gameTiming.CurTime < comp.AlertlevelTime)
                continue;

            if (comp.SetAlertlevel == null || comp.TargetStation == null)
                continue;

            _alertLevelSystem.SetLevel(comp.TargetStation.Value, comp.SetAlertlevel, true, true, true, true);
            comp.CanChangeAlertLevel = false;
        }
    }
    // Sunrise-End

    // Sunrise-Start: schedule delayed martial-law alert level switch.
    private void ApplySunriseWarDeclarationAdjustments(NukeopsRuleComponent nukeops)
    {
        nukeops.AlertlevelTime = Timing.CurTime + TimeSpan.FromSeconds(nukeops.AlertlevelDelay);
        nukeops.CanChangeAlertLevel = true;
    }
    // Sunrise-End

    // Sunrise-Start: relaxed war declaration checks (test/balance tuning).
    private bool TryGetSunriseWarCondition(NukeopsRuleComponent nukieRule, WarConditionStatus? oldStatus, out WarConditionStatus status)
    {
        if (oldStatus == WarConditionStatus.YesWar)
        {
            status = WarConditionStatus.WarReady;
            return true;
        }

        status = WarConditionStatus.YesWar;
        return true;
    }
    // Sunrise-End
}
