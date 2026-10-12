using System.Collections.Concurrent;
using RogueNet.Logic.Game;
using RogueNet.Logic.MapGeneration;

namespace RogueNet.API.Game;

public sealed class DungeonDiveRegistry(DungeonMapGenerator mapGenerator)
{
    private readonly ConcurrentDictionary<Guid, DungeonDive> _dives = new();

    public DungeonDiveSnapshot Start(int? seed)
    {
        var actualSeed = seed ?? Random.Shared.Next();
        var map = mapGenerator.Generate(random: new Random(actualSeed));
        var dive = new DungeonDive(actualSeed, map);
        _dives.TryAdd(dive.Id, dive);
        return dive.GetSnapshot();
    }

    public bool TryGet(Guid id, out DungeonDiveSnapshot? snapshot)
    {
        if (_dives.TryGetValue(id, out var dive))
        {
            snapshot = dive.GetSnapshot();
            return true;
        }

        snapshot = null;
        return false;
    }

    public bool TrySubmitAction(Guid id, DungeonAction action, out DungeonDiveSnapshot? snapshot)
    {
        if (_dives.TryGetValue(id, out var dive))
        {
            snapshot = dive.SubmitAction(action);
            return true;
        }

        snapshot = null;
        return false;
    }
}