using Content.Server._Sunrise.BloodCult.GameRule;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Server.Player;

namespace Content.Server._Sunrise.BloodCult.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class AddCultTargetCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public string Command => "bloodcult_addtarget";
    public string Description => Loc.GetString("bloodcult-addtarget-description");
    public string Help => Loc.GetString("bloodcult-addtarget-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("bloodcult-addtarget-usage"));
            return;
        }

        var ckey = args[0];
        if (!_playerManager.TryGetSessionByUsername(ckey, out var session) || session.AttachedEntity == null)
        {
            shell.WriteError(Loc.GetString("bloodcult-addtarget-player-not-found", ("ckey", ckey)));
            return;
        }

        var entityUid = session.AttachedEntity.Value;

        if (!_entManager.EntitySysManager.TryGetEntitySystem<BloodCultRuleSystem>(out var cultRuleSystem))
        {
            shell.WriteError(Loc.GetString("bloodcult-addtarget-system-not-found"));
            return;
        }

        var rule = cultRuleSystem.GetRule();
        if (rule == null)
        {
            shell.WriteError(Loc.GetString("bloodcult-addtarget-rule-not-found"));
            return;
        }

        // Use the system method to add target properly
        if (!cultRuleSystem.AddSpecificCultTarget(entityUid, rule))
        {
            shell.WriteError(Loc.GetString("bloodcult-addtarget-already-target"));
            return;
        }

        var targetName = _entManager.TryGetComponent<MetaDataComponent>(entityUid, out var meta)
            ? meta.EntityName
            : Loc.GetString("bloodcult-unknown-entity");
        shell.WriteLine(Loc.GetString("bloodcult-addtarget-success", ("name", targetName)));
    }
}
