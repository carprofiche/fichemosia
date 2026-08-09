using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Content.Shared.Chat;
using Content.Shared.Interaction.Components;
using Content.Shared.Jittering;
using Content.Shared.Throwing;

namespace Content.Shared.Movement.Systems;

public sealed partial class SharedRamAbilitySystem : EntitySystem
{
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private ActionBlockerSystem _blockerSystem = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedMoverController _mover = default!;


    [Dependency] private EntityQuery<MovementSpeedModifierComponent> _modifierQuery;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
    }

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
                    WindupTransition(entity, ram);
                    continue;

                case RamState.Windup when _timing.CurTime >= state.WindupEndTimestamp:
                    state.LastState = RamState.Running;
                    Dirty(entity, state);
                    RunTransition(entity, ram);
                    continue;

                case RamState.Running when !_xform.InRange(Transform(entity).Coordinates, state.RunStartPos, ram.RamLength):
                    EndTransition(entity, ram);
                    continue;
            }

            // We don't have anything special to do during the windup. From now on we're running.
            if (state.LastState is RamState.Windup)
                continue;

            var moveSpeedComponent = _modifierQuery.CompOrNull(entity);
            var sprintSpeed = moveSpeedComponent?.CurrentSprintSpeed ?? MovementSpeedModifierComponent.DefaultBaseSprintSpeed;

            //_physics.GetLinearVelocity(entity, );

            var worldRot = _xform.GetWorldRotation(entity).RoundToCardinalAngle();
            var velocity = worldRot.ToWorldVec() * (sprintSpeed * ram.RamSpeedModifier);
            _physics.SetLinearVelocity(entity, velocity);
        }
    }

    private void WindupTransition(EntityUid entity, RamAbilityComponent ram)
    {
        var selfMessage = ram.WindUpPopupSelf.HasValue
            ? Loc.GetString(ram.WindUpPopupSelf)
            : null;
        var othersMessage = ram.WindUpPopup.HasValue
            ? Loc.GetString(ram.WindUpPopup, ("name", Identity.Entity(entity, EntityManager)))
            : null;
        _popup.PopupEntity(selfMessage, othersMessage, entity, entity, PopupType.MediumCaution);

        _movementMod.TryAddMovementSpeedModDuration(entity,
            MovementModStatusSystem.Ramming,
            ram.WindUpDuration,
            ram.WindUpSpeedModifier);
    }

    private void RunTransition(EntityUid entity, RamAbilityComponent ram)
    {
        EnsureComp<BlockMovementComponent>(entity);
        _blockerSystem.UpdateCanMove(entity);

        if (ram.RunEmote.HasValue)
            _chat.TryEmoteWithChat(entity, ram.RunEmote);

        _jitter.DoJitter(entity, TimeSpan.FromSeconds(0.1), false);
    }

    [SubscribeLocalEvent]
    private void OnRamCollide(Entity<ActiveRamComponent> entity, ref StartCollideEvent args)
    {
        if (entity.Comp.LastState != RamState.Running)
            return;

        if ( _xformQuery.CompOrNull(args.OtherEntity)?.Anchored ?? false )
            _throwing.TryThrow(entity.Owner, Transform(entity.Owner).Coordinates.Offset(new Vector2(0,-1))); // bounce off lol

        EndTransition(entity, args.OtherEntity, Comp<RamAbilityComponent>(entity.Owner));
    }

    private void EndTransition(EntityUid entity, EntityUid otherEntity, RamAbilityComponent ram)
    {
        _stun.TryAddParalyzeDuration(otherEntity, TimeSpan.FromSeconds(3));
        EndTransition(entity, ram);
    }

    private void EndTransition(EntityUid entity, RamAbilityComponent ram)
    {
        RemComp<ActiveRamComponent>(entity);
        RemComp<BlockMovementComponent>(entity);
        _blockerSystem.UpdateCanMove(entity);
        _stun.TryAddParalyzeDuration(entity, TimeSpan.FromSeconds(3));
    }

    [SubscribeLocalEvent]
    private void OnRam(Entity<RamAbilityComponent> entity, ref RamEvent args)
    {
        _jitter.DoJitter(entity, TimeSpan.FromSeconds(0.1), false);
        EnsureComp<ActiveRamComponent>(entity, out var state);
        state.WindupEndTimestamp = _timing.CurTime + entity.Comp.WindUpDuration;
        state.RunStartPos = Transform(entity).Coordinates;
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
