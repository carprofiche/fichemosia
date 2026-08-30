using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Gravity;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Jittering;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;

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
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    [Dependency] private EntityQuery<StaminaComponent> _stamQuery;

    public override void Initialize()
    {
        SubscribeLocalEvent<ActiveRamComponent, UseAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<ActiveRamComponent, PickupAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<ActiveRamComponent, ThrowAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<ActiveRamComponent, AttackAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<ActiveRamComponent, ChangeDirectionAttemptEvent>(OnAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveRamComponent, RamAbilityComponent, InputMoverComponent>();
        while (query.MoveNext(out var entity, out var state, out var ram, out var moverComp))
        {
            // Do we have any transitions to set up?
            switch (state.LastState)
            {
                case RamState.Initial:
                    state.LastState = RamState.Windup;
                    DirtyField(entity, state, "LastState");

                    if (_status.TrySetStatusEffectDuration(entity, MovementModStatusSystem.Ramming, out EntityUid? moveStatus1))
                        _movementMod.TryUpdateMovementStatus(entity, moveStatus1.Value, ram.WindupSpeedModifier);

                    _jitter.DoJitter(entity, TimeSpan.FromSeconds(0.1), false, 5f, 2f);

                    var windupEmote = ram.WindupEmote?.Text;
                    if (windupEmote is not null)
                        _chat.TrySendInGameICMessage(entity, Loc.GetString(windupEmote), InGameICChatType.Emote, false);

                    _audio.PlayPredicted(ram.WindupEmote?.Sound, entity, entity);
                    continue;

                case RamState.Windup when _timing.CurTime >= state.WindupEndTime:
                    state.LastState = RamState.Running;
                    DirtyField(entity, state, "LastState");

                    if (_status.TryGetStatusEffect(entity, MovementModStatusSystem.Ramming, out EntityUid? moveStatus))
                        _movementMod.TryUpdateMovementStatus(entity, moveStatus.Value, ram.RunSpeedModifier);

                    _jitter.DoJitter(entity, TimeSpan.FromSeconds(0.1), false, 5f, 2f);

                    var runEmote = ram.RunEmote?.Text;
                    if (runEmote != null)
                        _chat.TrySendInGameICMessage(entity, Loc.GetString(runEmote), InGameICChatType.Emote, false);

                    _audio.PlayPredicted(ram.RunEmote?.Sound, entity, entity);
                    continue;

                case RamState.Running when !_xform.InRange(Transform(entity).Coordinates, state.RunStartPos, ram.RunLength):
                    EndTransition(entity);
                    continue;
            }

            if (state.LastState is RamState.Windup)
                continue;

            // We now can only be running.
            _mover.SetSprinting((entity, moverComp), _timing.TickFraction, false);
            moverComp.CurTickSprintMovement = state.Lock.ToVec();

            moverComp.LastInputTick = _timing.CurTick;
            moverComp.LastInputSubTick = ushort.MaxValue;
            Dirty(entity, moverComp);
        }
    }

    private void EndTransition(EntityUid entity)
    {
        RemComp<ActiveRamComponent>(entity);
        RemComp<NoRotateOnMoveComponent>(entity);

        _status.TryRemoveStatusEffect(entity, MovementModStatusSystem.Ramming);
        _stamina.TakeStaminaDamage(entity, _stamQuery.CompOrNull(entity)?.CritThreshold ?? 0, ignoreResist: true);
    }

    [SubscribeLocalEvent]
    private void OnRamCollide(Entity<ActiveRamComponent> entity, ref StartCollideEvent args)
    {
        if (entity.Comp.LastState != RamState.Running)
            return;

        if (args.OurFixture.Hard && args.OtherFixture.Hard)
        {
            _physics.ApplyLinearImpulse(entity.Owner, Angle.FromDegrees(180).RotateVec(_xform.GetWorldRotation(entity.Owner).ToWorldVec()) * 1000);

            if (entity.Comp.BonkDamage is not null)
                _damage.TryChangeDamage(entity.Owner, entity.Comp.BonkDamage);

            _audio.PlayPredicted(entity.Comp.BonkSound, entity.Owner, entity.Owner);
        }

        var crit = _stamQuery.CompOrNull(args.OtherEntity)?.CritThreshold;
        _stamina.TakeStaminaDamage(args.OtherEntity, entity.Comp.OtherStaminaDamage ?? crit ?? 0);

        if (_stamQuery.TryComp(args.OtherEntity, out StaminaComponent? otherStam))
            Dirty<StaminaComponent>((args.OtherEntity, otherStam));

        EndTransition(entity.Owner);
    }

    private void OnAttempt(EntityUid uid, ActiveRamComponent component, CancellableEntityEventArgs args)
    {
        args.Cancel();
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

        EnsureComp<ActiveRamComponent>(entity.Owner, out var state);
        EnsureComp<NoRotateOnMoveComponent>(entity.Owner);
        state.WindupEndTime = _timing.CurTime + entity.Comp.WindupDuration;
        state.RunStartPos = Transform(entity).Coordinates;
        state.BonkDamage = entity.Comp.BonkDamage;
        state.BonkSound = entity.Comp.BonkSound;
        state.OtherStaminaDamage = entity.Comp.OtherStaminaDamage;
        state.Lock = Transform(entity).LocalRotation.GetCardinalDir();
        Dirty(entity.Owner, state);

        _xform.SetLocalRotation(entity.Owner, state.Lock.ToAngle());

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
