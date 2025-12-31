using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct PriorityQueueEntry<T>
{
    public readonly T Value;
    public readonly double Score;
    public readonly double TravelCost;
    public readonly IReadOnlyCollection<PriorityQueueEntry<T>> PreviousEntries;

    public PriorityQueueEntry(T value)
    {
        this.Value = value;
        this.Score = 0;
        this.TravelCost = 0;
        this.PreviousEntries = new List<PriorityQueueEntry<T>>();
    }

    public PriorityQueueEntry(T value, double score, double travelCost, PriorityQueueEntry<T> previousValue)
    {
        this.Value = value;
        this.Score = score;
        this.TravelCost = travelCost;

        List<PriorityQueueEntry<T>> previousEntryTrain = new List<PriorityQueueEntry<T>>(previousValue.PreviousEntries);
        previousEntryTrain.Insert(0, previousValue);
        this.PreviousEntries = previousEntryTrain;
    }

    public List<T> AllEntriesToList()
    {
        List<T> entries = new List<T>();

        entries.Add(this.Value);
        foreach (PriorityQueueEntry<T> entry in PreviousEntries)
        {
            entries.Insert(0, entry.Value);
        }

        return entries;
    }
}
