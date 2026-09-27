namespace GFlow.Core.Workflows;

/// <summary>Contains the trigger events for a workflow.</summary>
public sealed class TriggerCollection : IReadOnlyList<Trigger>
{
    private readonly List<Trigger> _items = [];

    /// <summary>The number of configured triggers.</summary>
    public int Count => _items.Count;

    /// <summary>Gets a trigger by index.</summary>
    public Trigger this[int index] => _items[index];

    /// <summary>Adds a trigger.</summary>
    public void Add(Trigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        _items.Add(trigger);
    }

    /// <summary>Returns an enumerator.</summary>
    public IEnumerator<Trigger> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

