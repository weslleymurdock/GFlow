namespace GFlow.Core.Workflows;

/// <summary>Represents a shell command workflow step.</summary>
public sealed class RunStep : Step
{
    /// <summary>Creates a run step.</summary>
    public RunStep(string run)
    {
        ArgumentException.ThrowIfNullOrEmpty(run);
        Run = run;
    }

    /// <summary>The shell command or multiline script.</summary>
    public string Run { get; set; }

    /// <summary>Optional shell selector.</summary>
    public string? Shell { get; set; }
}

