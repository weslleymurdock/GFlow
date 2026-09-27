namespace GFlow.Core.Workflows;

/// <summary>Permission levels supported by GitHub Actions permissions.</summary>
public enum PermissionLevel
{
    /// <summary>Disables the permission.</summary>
    None,

    /// <summary>Grants read access to the permission.</summary>
    Read,

    /// <summary>Grants read and write access to the permission.</summary>
    Write
}
