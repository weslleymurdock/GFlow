namespace GFlow.Core.Workflows;

/// <summary>Defines the top-level GitHub Actions permissions representation.</summary>
public enum PermissionPolicy
{
    /// <summary>Uses explicit permissions for individual scopes.</summary>
    Explicit,

    /// <summary>Grants read access to all available scopes.</summary>
    ReadAll,

    /// <summary>Grants write access to all available scopes.</summary>
    WriteAll
}
