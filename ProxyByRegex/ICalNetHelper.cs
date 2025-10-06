using System;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Calendar = Ical.Net.Calendar;

namespace CalendarFunctions;

public static class ICalNetHelper
{
    public static IEnumerable<Occurrence> MyGetOccurrences<T>(Calendar cal, CalDateTime? startTime = null) where T : IRecurringComponent
    {
        // Get UID/RECURRENCE-ID combinations that replace occurrences
        var recurrenceIdsAndUids = GetRecurrenceIdsAndUids(cal.Children);

        var occurrences = cal.RecurringItems
            .OfType<T>()
            .Select(recurrable => recurrable.GetOccurrences(startTime, null)
                // Exclude occurrences that are overridden by other components with the same UID and RECURRENCE-ID.
                // This must happen before .OrderedDistinct() because that method would remove duplicates
                // based on the occurrence time, and we need to remove them based on UID + RECURRENCE-ID.
                .Where(r => IsUnmodifiedOccurrence(r, recurrenceIdsAndUids)))

            // Enumerate the list of occurrences (not the occurrences themselves) now to ensure
            // the initialization code is run, including validation and error handling.
            // This way we receive validation errors early, not only when enumeration starts.
            .ToList(); //NOSONAR - deliberately enumerate here

        // Merge the individual sequences into a single one. Take advantage of them
        // being ordered to avoid full enumeration.
        var occ2 = OrderedMergeMany(occurrences);

        // Remove duplicates based on Period.StartTime and take advantage of
        // being ordered to avoid full enumeration.
        var occ3 = OrderedDistinct(occ2);

            // Convert overflow exceptions to expected ones.
            //.HandleEvaluationExceptions();

        return occ2;
    }

    private static Dictionary<(string? Uid, DateTime RecurrenceId), IUniqueComponent> GetRecurrenceIdsAndUids(IEnumerable<ICalendarObject> children)
    {
        return children.OfType<IRecurrable>()
            .Where(r => r.RecurrenceId != null)
            .Select(r => (Component: r as IUniqueComponent, Uid: (r as IUniqueComponent)?.Uid, RecurrenceId: r.RecurrenceId!.Value))
            .Where(x => x is { Uid: not null, Component: not null })
            // Assure we have only one component per (UID, RECURRENCE-ID) pair
            .GroupBy(x => (x.Uid, x.RecurrenceId))
            // Get the last modified component for each (UID, RECURRENCE-ID) pair
            .Select(g =>
            {
                // Try to get the maximum SEQUENCE if present, otherwise fallback to Last()
                var maxSeqItem = g
                    .Where(x => x.Component is CalendarEvent { Sequence: > 0 })
                    .OrderByDescending(x => ((CalendarEvent)x.Component!).Sequence)
                    .FirstOrDefault();

                return maxSeqItem.Component != null ? maxSeqItem : g.Last();
            })
            .ToDictionary(x => (x.Uid, x.RecurrenceId), x => x.Component!);
    }

    private static bool IsUnmodifiedOccurrence(Occurrence r, Dictionary<(string? Uid, DateTime RecurrenceId), IUniqueComponent> recurrenceIdsAndUids)
    {
        return r.Source switch
        {
            // If the occurrence is a modified instance (has RecurrenceId and Uid)
            // and the source is the last modified instance for this RecurrenceId/Uid,
            IUniqueComponent { Uid: not null } uc when r.Source.RecurrenceId != null =>
                recurrenceIdsAndUids.TryGetValue((uc.Uid, r.Source.RecurrenceId.Value),
                    out var lastComponent) && ReferenceEquals(lastComponent, r.Source),

            // If not a modified occurrence, keep if:
            // - It is not a unique component, or
            // - There is no replacement for this UID/StartTime in recurrenceIdsAndUids
            IUniqueComponent uc =>
                !recurrenceIdsAndUids.ContainsKey((uc.Uid, r.Period.StartTime.Value)),

            // If not a unique component, always keep
            _ => true
        };
    }


    public static IEnumerable<T> OrderedMergeMany<T>(IEnumerable<IEnumerable<T>> sequences)
        => OrderedMergeMany(sequences, Comparer<T>.Default);

    public static IEnumerable<T> OrderedMergeMany<T>(IEnumerable<IEnumerable<T>> sequences, IComparer<T> comparer)
    {
        var list = (sequences as IList<IEnumerable<T>>) ?? sequences.ToList();
        return OrderedMergeMany(list, 0, list.Count, comparer);
    }

    private static IEnumerable<T> OrderedMergeMany<T>(IList<IEnumerable<T>> sequences, int offs, int length, IComparer<T> comparer)
    {
        if (length == 0)
            return [];

        if (length == 1)
            return sequences[offs];

        // Compose as a tree to ensure O(N*log(N)) complexity. Composing as a simple chain
        // would result in O(N*N) complexity, which wouldn't be a problem either, as
        // the number of sequences usually is low.
        var mid = length / 2;
        var left = OrderedMergeMany(sequences, offs, mid, comparer);
        var right = OrderedMergeMany(sequences, offs + mid, length - mid, comparer);

        return OrderedMerge(left, right, comparer);
    }

    public static IEnumerable<T> OrderedMerge<T>(IEnumerable<T> items, IEnumerable<T> other, IComparer<T> comparer)
    {
        using var it1 = items.GetEnumerator();
        using var it2 = other.GetEnumerator();

        var has1 = it1.MoveNext();
        var has2 = it2.MoveNext();

        while (has1 || has2)
        {
            var cmp = (has1, has2) switch
            {
                (true, false) => -1,
                (false, true) => 1,
                _ => comparer.Compare(it1.Current, it2.Current)
            };

            if (cmp <= 0)
            {
                yield return it1.Current;
                has1 = it1.MoveNext();
            }
            else
            {
                yield return it2.Current;
                has2 = it2.MoveNext();
            }
        }
    }

    public static IEnumerable<T> OrderedDistinct<T>(IEnumerable<T> items)
        => OrderedDistinct(items, EqualityComparer<T>.Default);

    public static IEnumerable<T> OrderedDistinct<T>(IEnumerable<T> items, IEqualityComparer<T> comparer)
    {
        var prev = default(T);
        var first = true;

        foreach (var item in items)
        {
            if (first || !comparer.Equals(prev!, item))
                yield return item;

            prev = item;
            first = false;
        }
    }

}
