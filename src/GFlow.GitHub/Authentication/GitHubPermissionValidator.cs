namespace GFlow.GitHub.Authentication;

/// <summary>Validates classic PAT scopes and fine-grained permissions without conflating the two models.</summary>
public sealed class GitHubPermissionValidator : IGitHubPermissionValidator
{
    /// <inheritdoc />
    public IReadOnlyList<GitHubPermissionResult> Validate(
        GitHubCredentialType credentialType,
        IReadOnlySet<string> grantedPermissions,
        IEnumerable<GitHubPermissionRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(grantedPermissions);
        ArgumentNullException.ThrowIfNull(requirements);

        return requirements.Select(requirement =>
        {
            var satisfied = credentialType switch
            {
                GitHubCredentialType.ClassicPersonalAccessToken =>
                    HasClassicScope(grantedPermissions, requirement.ClassicScope),
                GitHubCredentialType.FineGrainedPersonalAccessToken =>
                    HasFineGrainedPermission(grantedPermissions, requirement.FineGrainedPermission, requirement.Access),
                _ => false
            };

            var missing = satisfied
                ? null
                : credentialType == GitHubCredentialType.ClassicPersonalAccessToken
                    ? requirement.ClassicScope
                    : requirement.FineGrainedPermission;

            return new GitHubPermissionResult(
                requirement.Capability,
                satisfied,
                missing,
                satisfied ? null : $"Missing {missing} permission.");
        }).ToArray();
    }

    private static bool HasClassicScope(IReadOnlySet<string> granted, string? required)
    {
        if (string.IsNullOrWhiteSpace(required))
            return true;

        if (granted.Contains("repo") && required is "public_repo" or "repo")
            return true;

        if (granted.Contains("user") && required == "read:user")
            return true;

        return granted.Contains(required);
    }

    private static bool HasFineGrainedPermission(
        IReadOnlySet<string> granted,
        string required,
        GitHubPermissionAccess access)
    {
        if (granted.Contains(required))
            return true;

        var suffix = access == GitHubPermissionAccess.Write ? ":write" : ":read";
        return granted.Contains($"{required}{suffix}") ||
               (access == GitHubPermissionAccess.Read && granted.Contains($"{required}:write"));
    }
}
