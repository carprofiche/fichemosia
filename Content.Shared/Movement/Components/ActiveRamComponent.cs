using Content.Shared.Damage;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Movement.Components;

/// <summary>
/// Tracking component given to entities currently ramming.
/// Stores some timing and state information for the duration of the ram.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), Access(typeof(SharedRamAbilitySystem))]
public sealed partial class ActiveRamComponent : Component
{
    [AutoNetworkedField]
    public RamState LastState;

    [AutoNetworkedField]
    public TimeSpan WindupEndTime;

    [AutoNetworkedField]
    public EntityCoordinates RunStartPos;

    [AutoNetworkedField]
    public float RunSpeedModifier;

    [AutoNetworkedField]
    public DamageSpecifier? BonkDamage;

    [AutoNetworkedField]
    public SoundSpecifier? BonkSound;

    [AutoNetworkedField]
    public float? OtherStaminaDamage;

    [AutoNetworkedField]
    public Direction Lock;
}

[Serializable, NetSerializable]
public enum RamState : byte
{
    Initial,
    Windup,
    Running,
}
