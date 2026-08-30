using System.ComponentModel.DataAnnotations;
using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Movement.Components;

/// <summary>
/// A component allowing an entity to perform a ram attack.
/// The action is choreographed into discrete windup, running, then ending portions.
/// </summary>
/// <remarks>
/// To give the ram action to an entity use <see cref="ActionGrantComponent"/> and <see cref="ItemActionGrantComponent"/>.
/// The basic action prototype is "ActionRam".
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedRamAbilitySystem))]
public sealed partial class RamAbilityComponent : Component
{
    /// <summary>
    /// The action prototype and entity that allows you to ram.
    /// </summary>
    [DataField]
    public EntProtoId RamAction = "ActionRam";

    [DataField, AutoNetworkedField]
    public EntityUid? RamActionEntity;

    /// <summary>
    /// Shown to the entity when it can't ram right now.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId? FailPopup = "ram-ability-failure";

    #region windup

    /// <summary>
    /// The duration of the windup.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan WindupDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Speed modifier applied to the entity during the windup.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WindupSpeedModifier = 0.2f;

    /// <summary>
    /// Shown as an emote at the start of the windup.
    /// </summary>
    [DataField, AutoNetworkedField]
    public (LocId? Text, SoundSpecifier? Sound)? WindupEmote = ("ram-ability-windup", new SoundPathSpecifier("/Audio/_Carpmosia/Effects/gallop.ogg"));

    #endregion

    #region run

    /// <summary>
    /// The length of the ram in tiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RunLength = 6f;

    /// <summary>
    /// How fast the ram will move forward, applied against the entity's sprint speed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RunSpeedModifier = 1.25f;

    /// <summary>
    /// Shown as an emote at the start of the run.
    /// </summary>
    [DataField, AutoNetworkedField]
    public (LocId? Text, SoundSpecifier? Sound)? RunEmote = ("ram-ability-run", new SoundPathSpecifier("/Audio/_Carpmosia/Effects/gallop.ogg"));

    #endregion

    #region end

    /// <summary>
    /// The amount of stamina damage given to the rammer when the action ends.
    /// If null, will guarantee stamcrit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float? SelfStaminaDamage;

    /// <summary>
    /// The amount of stamina damage given to an entity if it collides with the rammer.
    /// If null, will guarantee stamcrit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float? OtherStaminaDamage = 100;

    /// <summary>
    /// Damage dealt when hitting something with a hard fixture.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier? BonkDamage = new()
    {
        DamageDict = new()
        {
            { "Blunt", 20 }, // OWWWW
        },
    };

    /// <summary>
    /// Damage dealt when hitting something with a hard fixture.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? BonkSound = new SoundPathSpecifier("/Audio/Effects/hit_kick.ogg");

    #endregion
}

public sealed partial class RamEvent : InstantActionEvent;
