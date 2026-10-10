using Content.Shared.Audio.Jukebox;
using Robust.Client.Audio;
using Robust.Client.UserInterface;
using Robust.Shared.Audio.Components;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using System.Linq;
using Robust.Shared.Audio.Systems; // _SE
namespace Content.Client.Audio.Jukebox;

public sealed partial class JukeboxBoundUserInterface : BoundUserInterface
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    [ViewVariables]
    private JukeboxMenu? _menu;

    public JukeboxBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<JukeboxMenu>();

        _menu.OnPlayPressed += args =>
        {
            if (args)
            {
                SendMessage(new JukeboxPlayingMessage());
            }
            else
            {
                SendMessage(new JukeboxPauseMessage());
            }
        };
        // _SE start
        _menu.OnCassetteSelected += index =>
        {
            SendMessage(new JukeboxSelectCassetteMessage(index));
        };

        _menu.OnCassetteEject += index =>
        {
            // локальный предсказанный звук
            if (EntMan.TryGetComponent(Owner, out JukeboxComponent? juke))
                _audio.PlayPredicted(juke.CassetteEjectSound, Owner, null);

            SendMessage(new JukeboxEjectCassetteMessage(index));
        };
        // _SE end

        _menu.OnStopPressed += () =>
        {
            SendMessage(new JukeboxStopMessage());
        };

        _menu.OnSongSelected += SelectSong;

        // Frontier: Shuffle & Repeat
        _menu.OnModeChanged += playbackMode =>
        {
            SendMessage(new JukeboxSetPlaybackModeMessage(playbackMode));
        };
        // End Frontier: Shuffle & Repeat

        _menu.SetTime += SetTime;
        PopulateMusic();
        Reload();
    }

    /// <summary>
    /// Reloads the attached menu if it exists.
    /// </summary>
    public void Reload()
    {
        if (_menu == null || !EntMan.TryGetComponent(Owner, out JukeboxComponent? jukebox))
            return;

        _menu.SetMenuTitle(jukebox.MenuTitle);
        _menu.SetAudioStream(jukebox.AudioStream);

        if (_protoManager.TryIndex(jukebox.SelectedSongId, out var songProto))
        {
            var length = EntMan.System<AudioSystem>().GetAudioLength(songProto.Path.Path.ToString());
            _menu.SetSelectedSong(songProto.Name, (float) length.TotalSeconds);
        }
        else
        {
            _menu.SetSelectedSong(string.Empty, 0f);
        }
    }

    // _SE start
    public void PopulateMusic()
    {
        if (!EntMan.TryGetComponent(Owner, out JukeboxComponent? jukebox))
            return;

        if (jukebox.Cassettes.Count == 0 ||
            jukebox.SelectedCassetteIndex < 0 ||
            jukebox.SelectedCassetteIndex >= jukebox.Cassettes.Count)
        {
            _menu?.Populate(Enumerable.Empty<JukeboxPrototype>());
            return;
        }

        var cassetteUid = jukebox.Cassettes[jukebox.SelectedCassetteIndex];
        if (!EntMan.TryGetComponent(cassetteUid, out JukeboxCassetteComponent? cassette))
        {
            _menu?.Populate(Enumerable.Empty<JukeboxPrototype>());
            return;
        }

        var songs = cassette.Songs
            .Select(id => _protoManager.TryIndex(id, out JukeboxPrototype? proto) ? proto : null)
            .Where(p => p != null)
            .Cast<JukeboxPrototype>();

        _menu?.Populate(songs);
    }
    // _SE end

    public void SelectSong(ProtoId<JukeboxPrototype> songid)
    {
        SendMessage(new JukeboxSelectedMessage(songid));
    }

    public void SetTime(float time)
    {
        var sentTime = time;

        // You may be wondering, what the fuck is this
        // Well we want to be able to predict the playback slider change, of which there are many ways to do it
        // We can't just use SendPredictedMessage because it will reset every tick and audio updates every frame
        // so it will go BRRRRT
        // Using ping gets us close enough that it SHOULD, MOST OF THE TIME, fall within the 0.1 second tolerance
        // that's still on engine so our playback position never gets corrected.
        if (EntMan.TryGetComponent(Owner, out JukeboxComponent? jukebox) &&
            EntMan.TryGetComponent(jukebox.AudioStream, out AudioComponent? audioComp))
        {
            audioComp.PlaybackPosition = time;
        }

        SendMessage(new JukeboxSetTimeMessage(sentTime));
    }

    // Frontier: Shuffle & Repeat
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        _menu?.UpdateState(state);
        PopulateMusic(); // _SE
    }
    // End Frontier
}
