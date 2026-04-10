using Content.Shared.Roles.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Roles.Components;

/// <summary>
/// Added to mind role entities to mark a S.E.L.F agent.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SELFAgentRoleComponent : BaseMindRoleComponent;
