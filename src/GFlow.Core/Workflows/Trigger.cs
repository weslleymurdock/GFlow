using GFlow.Core.Workflows.Yaml;

namespace GFlow.Core.Workflows;

/// <summary>Represents one GitHub Actions workflow trigger event.</summary>
public sealed class Trigger
{
    /// <summary>Creates a trigger.</summary>
    public Trigger(string eventName, YamlValue? configuration = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        EventName = eventName;
        Configuration = configuration;
    }

    /// <summary>The GitHub event name, such as <c>push</c>.</summary>
    public string EventName { get; }

    /// <summary>Optional event configuration.</summary>
    public YamlValue? Configuration { get; set; }
}

