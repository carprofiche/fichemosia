using System.ComponentModel.DataAnnotations;
using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Movement.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedRamAbilitySystem))]
public sealed partial class RamAbilityComponent : Component
{
    /// <summary>
    /// The action prototype that allows you to ram.
    /// </summary>
    [DataField]
    public EntProtoId RamAction = "ActionRam";

    [DataField, AutoNetworkedField]
    public EntityUid? RamActionEntity;

    /// <summary>
    /// The duration of the windup.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan WindUpDuration = TimeSpan.FromSeconds(1.25);

    /// <summary>
    /// The length of the ram in tiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RamLength = 5f;

    /// <summary>
    /// The duration that another entity will be stunned for when rammed.
    /// If null, it will be stunned for the same amount of time as the ramming entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? OtherStunDuration;

    /// <summary>
    /// The speed modifier that will be applied against the entity's sprint speed during the run.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RamSpeedModifier = 3f;

    /// <summary>
    /// The speed modifier applied to the entity during windup.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WindUpSpeedModifier = 0.2f;

    /// <summary>
    /// Played at the beginning of the windup segment.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? WindUpSound;

    /// <summary>
    /// Used as a step sound during the run segment.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? RunningSound;

    /// <summary>
    /// Shown to everyone in range at the beginning of the windup.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId? WindUpPopup;

    /// <summary>
    /// Shown to the rammer at the beginning of the windup.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId? WindUpPopupSelf;

    /// <summary>
    /// This emote is called at the beginning of the run segment.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<EmotePrototype>? RunEmote;

    /// <summary>
    /// Shown to the entity when it can't ram right now. ):
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId? FailPopup;

    /// <summary>
    /// Damage dealt when hitting something hard.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier? BonkDamage = new()
    {
        DamageDict = new () { { "Blunt", 10 } },
    };
}

public sealed partial class RamEvent : InstantActionEvent;
