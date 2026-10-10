using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared.Audio.Jukebox;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class JukeboxCassetteComponent : Component
{
    /// <summary>
    /// Usual track list.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<JukeboxPrototype>> Songs = new();

    /// <summary>
    /// If the category is set in the prototype of the cassette,
    /// the songs from the category will go into this cassette
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? Category;

    [DataField, AutoNetworkedField]
    public string CassetteName = "Cassette";
}
public static class JukeboxCassetteHelpers
{
    /// <summary>
    /// Resolves tracks on the cassette, songs at first, categories at second
    /// </summary>
    public static IEnumerable<ProtoId<JukeboxPrototype>> GetSongs(
        this JukeboxCassetteComponent cassette,
        IPrototypeManager proto)
    {
        var seen = new HashSet<string>();

        foreach (var song in cassette.Songs)
        {
            if (seen.Add(song.Id))
                yield return song;
        }

        if (string.IsNullOrEmpty(cassette.Category))
            yield break;

        foreach (var p in proto.EnumeratePrototypes<JukeboxPrototype>())
        {
            if (p.Category == cassette.Category && seen.Add(p.ID))
                yield return p.ID;
        }
    }
}
