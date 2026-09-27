namespace GFlow.GitHub.Authentication;

/// <summary>Validates GitHub permission evidence without treating caller-provided strings as token authority.</summary>
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
                    HasFineGrainedPermission(grantedPermissions, requirement.FineGrainedPermission, requirement.Access, requirement.Capability),
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
                satisfied ? null : $"Missing {missing} permission.",
                true);
        }).ToArray();
    }

    /// <summary>Maps evidence observed from authenticated GitHub API operations to application capabilities.</summary>
    public IReadOnlyList<GitHubPermissionResult> ValidateEvidence(
        GitHubCredentialType credentialType,
        IReadOnlyDictionary<string, GitHubPermissionObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);

        return GitHubPermissionRequirements.ProductLoop.Select(requirement =>
        {
            if (!observations.TryGetValue(requirement.Capability, out var observation))
            {
                return new GitHubPermissionResult(
                    requirement.Capability,
                    false,
                    credentialType == GitHubCredentialType.ClassicPersonalAccessToken
                        ? requirement.ClassicScope
                        : requirement.FineGrainedPermission,
                    "No GitHub-derived evidence is available for this capability.",
                    false);
            }

            return observation.Status switch
            {
                GitHubPermissionVerificationStatus.Verified =>
                    new GitHubPermissionResult(requirement.Capability, true, null, observation.Detail, true),
                GitHubPermissionVerificationStatus.Missing =>
                    new GitHubPermissionResult(
                        requirement.Capability,
                        false,
                        credentialType == GitHubCredentialType.ClassicPersonalAccessToken
                            ? requirement.ClassicScope
                            : requirement.FineGrainedPermission,
                        observation.Detail,
                        true),
                _ =>
                    new GitHubPermissionResult(
                        requirement.Capability,
                        false,
                        credentialType == GitHubCredentialType.ClassicPersonalAccessToken
                            ? requirement.ClassicScope
                            : requirement.FineGrainedPermission,
                        observation.Detail,
                        false)
            };
        }).ToArray();
    }

    private static bool HasClassicScope(IReadOnlySet<string> granted, string? required) =>
        string.IsNullOrWhiteSpace(required) ||
        granted.Contains(required) ||
        granted.Contains("repo") && required is "public_repo" or "repo" ||
        granted.Contains("user") && required == "read:user";

    private static bool HasFineGrainedPermission(
        IReadOnlySet<string> granted,
        string required,
        GitHubPermissionAccess access,
        string capability)
    {
        if (capability == "authenticated-user")
            return true;

        if (capability == "repository-creation")
            return granted.Contains("repository-creation:write") || granted.Contains("administration:write");

        if (capability == "workflow-files-write")
        {
            return granted.Contains("contents:write") && granted.Contains("workflows:write");
        }

        if (granted.Contains($"{required}:write"))
            return true;

        if (access == GitHubPermissionAccess.Read && granted.Contains($"{required}:read"))
            return true;

        return granted.Contains(required);
    }
}
