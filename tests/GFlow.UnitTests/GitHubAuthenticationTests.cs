using GFlow.GitHub.Authentication;
using GFlow.GitHub.Contracts;

namespace GFlow.UnitTests;

public sealed class GitHubAuthenticationTests
{
    [Fact]
    public async Task SecureStorePersistsAndRemovesCredential()
    {
        var store = new FakeSecureCredentialStore();
        var credential = GitHubCredentialSecret.Create("id", "secret-token");

        await store.SaveAsync(credential);
        Assert.Equal(credential, await store.GetActiveAsync());

        Assert.True(await store.RemoveAsync("id"));
        Assert.Null(await store.GetActiveAsync());
    }

    [Fact]
    public void PermissionValidatorDoesNotTreatFineGrainedPermissionsAsClassicScopes()
    {
        var validator = new GitHubPermissionValidator();
        var requirement = GitHubPermissionRequirements.RepositoryContentsWrite;

        var classic = validator.Validate(
            GitHubCredentialType.ClassicPersonalAccessToken,
            new HashSet<string>(["repo"]),
            [requirement]);

        var fineGrained = validator.Validate(
            GitHubCredentialType.FineGrainedPersonalAccessToken,
            new HashSet<string>(["repo"]),
            [requirement]);

        Assert.True(classic[0].Satisfied);
        Assert.False(fineGrained[0].Satisfied);
        Assert.Equal("contents", fineGrained[0].MissingPermission);
    }

    [Fact]
    public void PermissionValidatorAcceptsFineGrainedWritePermissionForRead()
    {
        var validator = new GitHubPermissionValidator();
        var result = validator.Validate(
            GitHubCredentialType.FineGrainedPersonalAccessToken,
            new HashSet<string>(["contents:write"]),
            [GitHubPermissionRequirements.RepositoryContentsRead]);

        Assert.True(result[0].Satisfied);
    }

    [Fact]
    public void TokenDetectionSeparatesClassicAndFineGrainedPat()
    {
        Assert.Equal(
            GitHubCredentialType.FineGrainedPersonalAccessToken,
            GitHubAuthenticationService.DetectCredentialType("github_pat_example"));

        Assert.Equal(
            GitHubCredentialType.ClassicPersonalAccessToken,
            GitHubAuthenticationService.DetectCredentialType("ghp_example"));
    }

    [Fact]
    public void OrdinaryAuthenticationModelsContainNoTokenProperty()
    {
        Assert.DoesNotContain(
            typeof(GitHubCredentialInfo).GetProperties(),
            property => property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            typeof(GitHubAccount).GetProperties(),
            property => property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeSecureCredentialStore : ISecureCredentialStore
    {
        private readonly Dictionary<string, GitHubCredentialSecret> _credentials = [];
        private string? _active;

        public Task SaveAsync(GitHubCredentialSecret credential, CancellationToken cancellationToken = default)
        {
            _credentials[credential.CredentialId] = credential;
            _active = credential.CredentialId;
            return Task.CompletedTask;
        }

        public Task<GitHubCredentialSecret?> GetActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_active is null ? null : _credentials.GetValueOrDefault(_active));

        public Task<bool> RemoveAsync(string credentialId, CancellationToken cancellationToken = default)
        {
            var removed = _credentials.Remove(credentialId);
            if (_active == credentialId)
                _active = null;
            return Task.FromResult(removed);
        }

        public Task SetActiveAsync(string credentialId, CancellationToken cancellationToken = default)
        {
            _active = credentialId;
            return Task.CompletedTask;
        }
    }
}
