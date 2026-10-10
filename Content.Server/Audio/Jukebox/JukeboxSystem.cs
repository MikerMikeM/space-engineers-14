using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Audio.Jukebox;
using Content.Shared.Power;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Containers;
using JukeboxComponent = Content.Shared.Audio.Jukebox.JukeboxComponent;
using Robust.Shared.Random; // Frontier
using Content.Shared.Interaction; // _SE
using System.Linq; // _SE

namespace Content.Server.Audio.Jukebox;


public sealed partial class JukeboxSystem : SharedJukeboxSystem
{
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private AppearanceSystem _appearanceSystem = default!;
    [Dependency] private IRobustRandom _random = default!; // Frontier
    [Dependency] private TransformSystem _transform = default!; // Frontier
    [Dependency] private UserInterfaceSystem _userInterface = default!; // Frontier
    [Dependency] private SharedContainerSystem _container = default!;   // _SE

    // _SE start
    private const string CassetteContainer = "jukebox-cassettes";
    // _SE end

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<JukeboxComponent, JukeboxSelectedMessage>(OnJukeboxSelected);
        SubscribeLocalEvent<JukeboxComponent, JukeboxPlayingMessage>(OnJukeboxPlay);
        SubscribeLocalEvent<JukeboxComponent, JukeboxPauseMessage>(OnJukeboxPause);
        SubscribeLocalEvent<JukeboxComponent, JukeboxStopMessage>(OnJukeboxStop);
        SubscribeLocalEvent<JukeboxComponent, JukeboxSetPlaybackModeMessage>(OnJukeboxSetPlayback); // Frontier
        SubscribeLocalEvent<JukeboxComponent, JukeboxSetTimeMessage>(OnJukeboxSetTime);
        SubscribeLocalEvent<JukeboxComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<JukeboxComponent, ComponentShutdown>(OnComponentShutdown);

        SubscribeLocalEvent<JukeboxComponent, ComponentStartup>(OnComponentStartup); // Frontier
        SubscribeLocalEvent<JukeboxComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<JukeboxComponent, BoundUIOpenedEvent>(OnUiOpened); // Lua
        SubscribeLocalEvent<JukeboxComponent, BoundUIClosedEvent>(OnUiClosed); // Lua
        SubscribeLocalEvent<JukeboxComponent, JukeboxSelectCassetteMessage>(OnCassetteSelected); // _SE
        SubscribeLocalEvent<JukeboxComponent, JukeboxEjectCassetteMessage>(OnCassetteEject); // _SE
        SubscribeLocalEvent<JukeboxComponent, InteractUsingEvent>(OnInteractUsing); // _SE

        _protoManager.PrototypesReloaded += OnPrototypesReloaded; // _SE
    }

    // _SE start
    public override void Shutdown()
    {
        base.Shutdown();
        _protoManager.PrototypesReloaded -= OnPrototypesReloaded;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<JukeboxPrototype>())
            return;

        var query = EntityQueryEnumerator<JukeboxComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.UiOpen)
                UpdateUI((uid, comp));
        }
    }
    // _SE end

    private void OnComponentInit(EntityUid uid, JukeboxComponent component, ComponentInit args)
    {
        if (HasComp<ApcPowerReceiverComponent>(uid))
        {
            TryUpdateVisualState(uid, component);
        }
    }

    // Frontier: Shuffle & Repeat
    private void OnComponentStartup(Entity<JukeboxComponent> entity, ref ComponentStartup ev)
    {
        UpdateUI(entity);
    }

    private void UpdateUI(Entity<JukeboxComponent> ent)
    {
        // _SE start
        var cassettes = new List<JukeboxCassetteState>();
        foreach (var cassetteUid in ent.Comp.Cassettes)
        {
            if (!TryComp<JukeboxCassetteComponent>(cassetteUid, out var cassette))
                continue;

            cassettes.Add(new JukeboxCassetteState
            {
                Name = cassette.CassetteName,
                Songs = cassette.GetSongs(_protoManager).ToList(),
            });
        }

        var state = new JukeboxInterfaceState(
            ent.Comp.PlaybackMode,
            cassettes,
            ent.Comp.SelectedCassetteIndex);
        // _SE end

        _userInterface.SetUiState(ent.Owner, JukeboxUiKey.Key, state);
    }
    // End Frontier: Shuffle & Repeat

    private void OnJukeboxPlay(EntityUid uid, JukeboxComponent component, ref JukeboxPlayingMessage args)
    {
        if (Exists(component.AudioStream))
        {
            Audio.SetState(component.AudioStream, AudioState.Playing);
        }
        else
        {
            component.AudioStream = Audio.Stop(component.AudioStream);

            // Lua start
            if (component.OnRandomStates != null && component.OnRandomStates.Count > 0)
            {
                component.OnState = _random.Pick(component.OnRandomStates);
                Dirty(uid, component);
            }
            // Lua end

            // _SE start
            // Frontier: Shuffling feature.ы
            if (component.PlaybackMode == JukeboxPlaybackMode.Shuffle
                && !component.FirstPlay
                && component.SelectedCassetteIndex >= 0
                && component.SelectedCassetteIndex < component.Cassettes.Count
                && TryComp<JukeboxCassetteComponent>(
                    component.Cassettes[component.SelectedCassetteIndex], out var cassetteComp))
            {
                var pool = cassetteComp.GetSongs(_protoManager).ToList();
                if (pool.Count > 0)
                    component.SelectedSongId = _random.Pick(pool);
            }
            // End Frontier
            // _SE end

            if (string.IsNullOrEmpty(component.SelectedSongId) ||
                !_protoManager.TryIndex(component.SelectedSongId, out var jukeboxProto))
            {
                return;
            }

            component.AudioStream = Audio.PlayPvs(jukeboxProto.Path, uid, AudioParams.Default.WithMaxDistance(10f))?.Entity;

            // Frontier: wallmount jukebox, shuffle state
            if (TryComp<TransformComponent>(component.AudioStream, out var xform))
                _transform.SetLocalPosition(component.AudioStream.Value, component.AudioOffset, xform);

            component.FirstPlay = false;
            // End Frontier

            Dirty(uid, component);
        }
    }

    private void OnJukeboxPause(Entity<JukeboxComponent> ent, ref JukeboxPauseMessage args)
    {
        Audio.SetState(ent.Comp.AudioStream, AudioState.Paused);
    }

    // Frontier: Shuffle & Repeat
    private void OnJukeboxSetPlayback(Entity<JukeboxComponent> ent, ref JukeboxSetPlaybackModeMessage playbackModeMessage)
    {
        if (ent.Comp.PlaybackMode != playbackModeMessage.PlaybackMode)
        {
            ent.Comp.PlaybackMode = playbackModeMessage.PlaybackMode;
            UpdateUI(ent);
            Dirty(ent);
        }
    }

    public AudioState GetAudioState(EntityUid? entity, AudioComponent? component = null)
    {
        if (entity == null || !Resolve(entity.Value, ref component, false))
            return AudioState.Stopped; // Consider no audio as stopped.

        return component.State;
    }
    // End Frontier: Shuffle & Repeat

    private void OnJukeboxSetTime(EntityUid uid, JukeboxComponent component, JukeboxSetTimeMessage args)
    {
        if (TryComp(args.Actor, out ActorComponent? actorComp))
        {
            var offset = actorComp.PlayerSession.Channel.Ping * 1.5f / 1000f;
            Audio.SetPlaybackPosition(component.AudioStream, args.SongTime + offset);
        }
    }

    private void OnPowerChanged(Entity<JukeboxComponent> entity, ref PowerChangedEvent args)
    {
        TryUpdateVisualState(entity);

        if (!this.IsPowered(entity.Owner, EntityManager))
        {
            Stop(entity);
        }
    }

    private void OnJukeboxStop(Entity<JukeboxComponent> entity, ref JukeboxStopMessage args)
    {
        Stop(entity);
    }

    // Frontier: Modified Stop() function for the Shuffling & Replay features.
    private void Stop(Entity<JukeboxComponent> entity)
    {
        //Audio.SetState(entity.Comp.AudioStream, AudioState.Stopped); // No longer needed since we're removing the AudioStream.
        entity.Comp.AudioStream = Audio.Stop(entity.Comp.AudioStream);
        entity.Comp.FirstPlay = true;
        Dirty(entity);
    }
    // End Frontier

    private void OnJukeboxSelected(EntityUid uid, JukeboxComponent component, JukeboxSelectedMessage args)
    {
        // _SE start
        if (component.SelectedCassetteIndex < 0 ||
            component.SelectedCassetteIndex >= component.Cassettes.Count)
            return;

        if (!TryComp<JukeboxCassetteComponent>(
                component.Cassettes[component.SelectedCassetteIndex], out var cassetteComp))
            return;

        if (!cassetteComp.GetSongs(_protoManager).Contains(args.SongId))
            return;
        // _SE end

        // Frontier: allow selecting songs while they're playing
        bool wasPlaying = Audio.IsPlaying(component.AudioStream);
        component.SelectedSongId = args.SongId;
        DirectSetVisualState(uid, JukeboxVisualState.Select);
        component.Selecting = true;
        component.SelectAccumulator = 0;
        component.AudioStream = Audio.Stop(component.AudioStream);
        component.FirstPlay = true; // Prevent shuffling
        if (wasPlaying)
        {
            var msg = new JukeboxPlayingMessage();
            OnJukeboxPlay(uid, component, ref msg);
        }
        // End Frontier

        Dirty(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<JukeboxComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Selecting && !comp.UiOpen) // Lua add  && !comp.UiOpen
            {
                comp.SelectAccumulator += frameTime;
                if (comp.SelectAccumulator >= 0.5f)
                {
                    comp.SelectAccumulator = 0f;
                    comp.Selecting = false;

                    TryUpdateVisualState(uid, comp);
                }
            }

            // Lua start
            // Frontier: Replay feature. Please pitch in if you have better ideas. This is a pretty bad implementation.
            //if (comp.PlaybackMode != JukeboxPlaybackMode.Single && comp.AudioStream != null &&
            //    GetAudioState(comp.AudioStream) == AudioState.Stopped)
            //{
            //    var msg = new JukeboxPlayingMessage();
            //    OnJukeboxPlay(uid, comp, ref msg);
            //}
            // End Frontier
            var isStopped = comp.AudioStream == null || GetAudioState(comp.AudioStream) == AudioState.Stopped;
            if (isStopped && !comp.UiOpen)
            {
                TryUpdateVisualState(uid, comp);
            }
            // Lua end
        }
    }

    private void OnComponentShutdown(EntityUid uid, JukeboxComponent component, ComponentShutdown args)
    {
        component.AudioStream = Audio.Stop(component.AudioStream);
    }

    private void DirectSetVisualState(EntityUid uid, JukeboxVisualState state)
    {
        _appearanceSystem.SetData(uid, JukeboxVisuals.VisualState, state);
    }

    // _SE start
    /// <summary>
    /// Interaction between player and jukebox - click on the jukebox with casette in the active hand will insert it into the jukebox.
    /// </summary>
    private void OnInteractUsing(Entity<JukeboxComponent> ent, ref InteractUsingEvent args)
    {
        if (!HasComp<JukeboxCassetteComponent>(args.Used))
            return;

        if (ent.Comp.Cassettes.Contains(args.Used))
            return;

        var container = _container.EnsureContainer<Container>(ent.Owner, CassetteContainer);
        if (!_container.Insert(args.Used, container))
            return;

        ent.Comp.Cassettes.Add(args.Used);
        Audio.PlayPvs(ent.Comp.CassetteInsertSound, ent.Owner);
        Dirty(ent);
        UpdateUI(ent);
        args.Handled = true;
    }

    private void OnCassetteEject(Entity<JukeboxComponent> ent, ref JukeboxEjectCassetteMessage msg)
    {
        if (msg.Index < 0 || msg.Index >= ent.Comp.Cassettes.Count)
            return;

        var cassetteUid = ent.Comp.Cassettes[msg.Index];

        if (!_container.TryRemoveFromContainer(cassetteUid))
            return;

        ent.Comp.Cassettes.RemoveAt(msg.Index);

        Audio.PlayPvs(ent.Comp.CassetteEjectSound, ent.Owner);

        if (ent.Comp.SelectedCassetteIndex >= ent.Comp.Cassettes.Count)
            ent.Comp.SelectedCassetteIndex = Math.Max(0, ent.Comp.Cassettes.Count - 1);

        // Если извлекли текущую играющую — остановить
        ent.Comp.AudioStream = Audio.Stop(ent.Comp.AudioStream);
        ent.Comp.SelectedSongId = null;
        ent.Comp.FirstPlay = true;

        Dirty(ent);
        UpdateUI(ent);
    }

    /// <summary>
    /// Menu tabs above the playlist - cassettes. Like categories.
    /// </summary>
    private void OnCassetteSelected(Entity<JukeboxComponent> ent, ref JukeboxSelectCassetteMessage msg)
    {
        if (msg.Index < 0 || msg.Index >= ent.Comp.Cassettes.Count)
            return;
        if (ent.Comp.SelectedCassetteIndex == msg.Index)
            return;

        ent.Comp.SelectedCassetteIndex = msg.Index;
        ent.Comp.SelectedSongId = null;
        ent.Comp.AudioStream = Audio.Stop(ent.Comp.AudioStream);
        ent.Comp.FirstPlay = true;

        Dirty(ent);
        UpdateUI(ent);
    }
    // _SE end

    private void TryUpdateVisualState(EntityUid uid, JukeboxComponent? jukeboxComponent = null)
    {
        if (!Resolve(uid, ref jukeboxComponent))
            return;

        var finalState = JukeboxVisualState.On;

        if (!this.IsPowered(uid, EntityManager))
        {
            finalState = JukeboxVisualState.Off;
        }

        _appearanceSystem.SetData(uid, JukeboxVisuals.VisualState, finalState);
    }

    // Lua start
    private void OnUiOpened(Entity<JukeboxComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is not JukeboxUiKey) return;
        ent.Comp.UiOpen = true;
        if (!string.IsNullOrEmpty(ent.Comp.SelectState))
        {
            DirectSetVisualState(ent, JukeboxVisualState.Select);
            ent.Comp.Selecting = ent.Comp.SelectIsLoop;
            ent.Comp.SelectAccumulator = 0;
            Dirty(ent);
        }
    }

    private void OnUiClosed(Entity<JukeboxComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is not JukeboxUiKey) return;
        ent.Comp.UiOpen = false;
        TryUpdateVisualState(ent);
    }
    // Lua end
}
