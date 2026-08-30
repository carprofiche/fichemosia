using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Gravity;
using Content.Shared.Interaction.Components;
using Content.Shared.Jittering;
using Content.Shared.Standing;

namespace Content.Shared.Movement.Systems;

public sealed partial class SharedRamAbilitySystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private ActionBlockerSystem _blockerSystem = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    [Dependency] private EntityQuery<MovementSpeedModifierComponent> _modifierQuery;
    [Dependency] private EntityQuery<StaminaComponent> _stamQuery;


    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveRamComponent, RamAbilityComponent>();
        while (query.MoveNext(out var entity, out var state, out var ram))
        {
            // Do we have any transitions to set up?
            switch (state.LastState)
            {
                case RamState.Initial:
                    state.LastState = RamState.Windup;
                    Dirty(entity, state);
                    WindupTransition((entity, ram));
                    continue;

                case RamState.Windup when _timing.CurTime >= state.WindupEndTimestamp:
                    state.LastState = RamState.Running;
                    Dirty(entity, state);
                    RunTransition((entity, ram));
                    continue;

                case RamState.Running when !_xform.InRange(Transform(entity).Coordinates, state.RunStartPos, ram.RamLength):
                    EndTransition(entity);
                    continue;
            }

            if (state.LastState is RamState.Windup)
                continue;

            // We can only be in the running state now.
            var worldCardinal = GetWorldCardinalVec((entity, Transform(entity)));
            var ramSpeed = GetRamSpeed((entity, ram));
            _physics.SetLinearVelocity(entity, worldCardinal * ramSpeed);
        }
    }

    private float GetRamSpeed(Entity<RamAbilityComponent> entity)
    {
        return GetRamSpeed(entity, entity.Comp.RamSpeedModifier);
    }

    private float GetRamSpeed(Entity<ActiveRamComponent> entity)
    {
        return GetRamSpeed(entity, entity.Comp.RamSpeedModifier);
    }

    private float GetRamSpeed(EntityUid entity, float ramSpeedModifier)
    {
        var sprintSpeed = _modifierQuery.CompOrNull(entity)?.CurrentSprintSpeed ?? MovementSpeedModifierComponent.DefaultBaseSprintSpeed;
        return sprintSpeed * ramSpeedModifier;
    }

    private Vector2 GetWorldCardinalVec(Entity<TransformComponent> entity)
    {
        var rotation = entity.Comp.LocalRotation;
        var cardinalWorldRot = _xform.GetWorldRotation(entity.Owner) + (rotation - rotation.GetCardinalDir().ToAngle());
        return cardinalWorldRot.ToWorldVec();
    }

    private void WindupTransition(Entity<RamAbilityComponent> entity)
    {
        var selfMessage = entity.Comp.WindUpPopupSelf.HasValue
            ? Loc.GetString(entity.Comp.WindUpPopupSelf)
            : null;
        var othersMessage = entity.Comp.WindUpPopup.HasValue
            ? Loc.GetString(entity.Comp.WindUpPopup, ("name", Identity.Entity(entity, EntityManager)))
            : null;
        _popup.PopupEntity(selfMessage, othersMessage, entity, entity, PopupType.MediumCaution);

        _movementMod.TryAddMovementSpeedModDuration(entity,
            MovementModStatusSystem.Ramming,
            entity.Comp.WindUpDuration,
            entity.Comp.WindUpSpeedModifier);
    }

    private void RunTransition(Entity<RamAbilityComponent> entity)
    {
        EnsureComp<BlockMovementComponent>(entity);
        _blockerSystem.UpdateCanMove(entity);

        if (entity.Comp.RunEmote.HasValue)
            _chat.TryEmoteWithChat(entity, entity.Comp.RunEmote);

        _jitter.DoJitter(entity, TimeSpan.FromSeconds(0.1), false);
    }

    [SubscribeLocalEvent]
    private void OnRamCollide(Entity<ActiveRamComponent> entity, ref StartCollideEvent args)
    {
        if (entity.Comp.LastState != RamState.Running)
            return;

        if (args.OtherFixture.Hard && entity.Comp.BonkDamage is not null)
            _damage.TryChangeDamage(entity.Owner, entity.Comp.BonkDamage);

        var impulseMod = entity.Comp.RamSpeedModifier * GetRamSpeed(entity);
        var cardinal = GetWorldCardinalVec((entity.Owner, Transform(entity)));
        _physics.ApplyLinearImpulse(entity.Owner, Angle.FromDegrees(180).RotateVec(cardinal) * (impulseMod * 25)); // bounce off lol
        _stamina.TakeStaminaDamage(args.OtherEntity, _stamQuery.CompOrNull(args.OtherEntity)?.CritThreshold ?? 0);

        EndTransition(entity.Owner);
    }

    private void EndTransition(EntityUid entity)
    {
        RemComp<ActiveRamComponent>(entity);
        RemComp<BlockMovementComponent>(entity);
        _blockerSystem.UpdateCanMove(entity);
        _stamina.TakeStaminaDamage(entity, _stamQuery.CompOrNull(entity)?.CritThreshold ?? 0, ignoreResist: true);
    }

    [SubscribeLocalEvent]
    private void OnRam(Entity<RamAbilityComponent> entity, ref RamEvent args)
    {
        if (_gravity.IsWeightless(args.Performer) || _standing.IsDown(args.Performer))
        {
            var popup = entity.Comp.FailPopup.HasValue
                ? Loc.GetString(entity.Comp.FailPopup)
                : null;
            _popup.PopupEntity(popup, entity.Owner, entity.Owner);
            return;
        }
        _jitter.DoJitter(entity, TimeSpan.FromSeconds(0.1), false);
        EnsureComp<ActiveRamComponent>(entity, out var state);
        state.WindupEndTimestamp = _timing.CurTime + entity.Comp.WindUpDuration;
        state.RunStartPos = Transform(entity).Coordinates;
        state.RamSpeedModifier = entity.Comp.RamSpeedModifier;
        state.BonkDamage = entity.Comp.BonkDamage;
        Dirty(entity, state);

        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnInit(Entity<RamAbilityComponent> entity, ref MapInitEvent args)
    {
        if (!TryComp(entity, out ActionsComponent? actions))
            return;

        _actions.AddAction(entity, ref entity.Comp.RamActionEntity, entity.Comp.RamAction, component: actions);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<RamAbilityComponent> entity, ref ComponentShutdown args)
    {
        _actions.RemoveAction(entity.Owner, entity.Comp.RamActionEntity);
    }
}
