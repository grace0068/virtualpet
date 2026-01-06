using System.Collections.Generic;

public class AnchorMemory
{
    private readonly Dictionary<string, AnchorRecord> _mem = new();

    public IReadOnlyCollection<AnchorRecord> All => _mem.Values;

    public void UpdateFromVisible(List<AnchorObservation> visible, float now)
    {
        for (int i = 0; i < visible.Count; i++)
        {
            var obs = visible[i];
            if (obs == null || string.IsNullOrEmpty(obs.id)) continue;

            if (_mem.TryGetValue(obs.id, out var rec))
            {
                rec.lastSeenTime = now;
                rec.seenCount += 1;

                rec.label = obs.label;
                rec.position = obs.position;
                rec.rotation = obs.rotation;
                rec.size = obs.size;

                rec.lastVisibilityScore = obs.visibilityScore;
            }
            else
            {
                _mem[obs.id] = new AnchorRecord
                {
                    id = obs.id,
                    label = obs.label,
                    position = obs.position,
                    rotation = obs.rotation,
                    size = obs.size,

                    firstSeenTime = now,
                    lastSeenTime = now,
                    seenCount = 1,
                    lastVisibilityScore = obs.visibilityScore
                };
            }
        }
    }

    public AnchorRecord FindByLabel(string label)
    {
        foreach (var r in _mem.Values)
            if (r.label == label) return r;
        return null;
    }
}
