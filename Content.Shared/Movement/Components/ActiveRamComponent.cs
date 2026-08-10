using Content.Shared.Damage;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Movement.Components;

/// <summary>
/// Tracking component given to entities currently ramming.
/// Stores some timing and state information for the duration of the ram.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedRamAbilitySystem))]
public sealed partial class ActiveRamComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan WindupEndTimestamp;

    [AutoNetworkedField]
    public EntityCoordinates RunStartPos;

    [AutoNetworkedField]
    public RamState LastState;

    [AutoNetworkedField]
    public DamageSpecifier? BonkDamage;

    [AutoNetworkedField]
    public float RamSpeedModifier = 3f;
}

[Serializable, NetSerializable]
public enum RamState : byte
{
    Initial,
    Windup,
    Running,
}
