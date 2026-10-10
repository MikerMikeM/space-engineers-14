using Robust.Shared.Audio.Systems;
using Robust.Shared.Serialization; // Frontier
using System.Collections.Generic;         // _SE
using Robust.Shared.Prototypes;           // _SE

namespace Content.Shared.Audio.Jukebox;

public abstract partial class SharedJukeboxSystem : EntitySystem
{
    [Dependency] protected SharedAudioSystem Audio = default!;
}

// Frontier: Shuffle & Repeat
// _SE start
[Serializable, NetSerializable]
public sealed class JukeboxCassetteState
{
    public string Name = string.Empty;
    public List<ProtoId<JukeboxPrototype>> Songs = new();
}

[Serializable, NetSerializable]
public sealed class JukeboxInterfaceState(
    JukeboxPlaybackMode playbackMode,
    List<JukeboxCassetteState> cassettes,
    int selectedCassetteIndex) : BoundUserInterfaceState
{
    public JukeboxPlaybackMode PlaybackMode { get; set; } = playbackMode;
    public List<JukeboxCassetteState> Cassettes { get; set; } = cassettes;
    public int SelectedCassetteIndex { get; set; } = selectedCassetteIndex;
}
// _SE end
// End Frontier: Shuffle & Repeat
