using System.Collections.Generic;
using UnityEngine;

public class PriorityQueue<T>
{
    private List<PriorityQueueEntry<T>> entries { get; set; } = new List<PriorityQueueEntry<T>>();

    public bool HasValues()
    {
        return entries.Count > 0;
    }

    public void AddEntry(PriorityQueueEntry<T> entry)
    {
        for (int ii = 0; ii < entries.Count; ii++)
        {
            if (entry.Score > entries[ii].Score)
            {
                entries.Insert(0, entry);
                return;
            }
        }

        this.entries.Add(entry);
    }

    public bool TryGetHighestScoringValue(out PriorityQueueEntry<T> entry)
    {
        if (entries.Count == 0)
        {
            entry = default(PriorityQueueEntry<T>);
            return false;
        }

        entry = entries[0];
        entries.RemoveAt(0);
        return true;
    }

    public bool TryGetLowestScoringValue(out PriorityQueueEntry<T> entry)
    {
        if (entries.Count == 0)
        {
            entry = default(PriorityQueueEntry<T>);
            return false;
        }

        entry = entries[entries.Count - 1];
        entries.RemoveAt(entries.Count - 1);
        return true;
    }
}
