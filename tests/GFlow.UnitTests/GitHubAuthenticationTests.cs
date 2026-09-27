using GFlow.GitHub.Authentication;
using GFlow.GitHub.Contracts;
using GFlow.GitHub.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions;

namespace GFlow.UnitTests;

public sealed class GitHubAuthenticationTests
{
    [Fact]
    public void AuthenticationGraphResolvesWithoutCircularDependency()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISecureCredentialStore, FakeSecureCredentialStore>();
        services.AddGFlowGitHubAuthenticated();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        using var scope = provider.CreateScope();
        var authentication = scope.ServiceProvider.GetRequiredService<IGitHubAuthenticationService>();
        var credentialProvider = scope.ServiceProvider.GetRequiredService<IGitHubCredentialProvider>();
        var adapter = scope.ServiceProvider.GetRequiredService<IRequestAdapter>();

        Assert.NotNull(authentication);
        Assert.NotNull(credentialProvider);
        Assert.NotNull(adapter);
    }

    [Fact]
    public async Task SecureStorePersistsAndRemovesCredential()
    {
        var store = new FakeSecureCredentialStore();
        var credential = GitHubCredentialSecret.Create("id", "secret-token");

        await store.SaveAsync(credential, TestContext.Current.CancellationToken);
        Assert.Equal(credential, await store.GetActiveAsync(TestContext.Current.CancellationToken));
        Assert.True(await store.RemoveAsync("id", TestContext.Current.CancellationToken));
        Assert.Null(await store.GetActiveAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void PermissionValidatorSeparatesClassicScopesFromFineGrainedPermissions()
    {
        var validator = new GitHubPermissionValidator();
        var requirement = GitHubPermissionRequirements.WorkflowFilesWrite;

        var classic = validator.Validate(
            GitHubCredentialType.ClassicPersonalAccessToken,
            new HashSet<string>(["workflow"]),
            [requirement]);

        var fineGrained = validator.Validate(
            GitHubCredentialType.FineGrainedPersonalAccessToken,
            new HashSet<string>(["workflow"]),
            [requirement]);

        Assert.True(classic[0].Satisfied);
        Assert.False(fineGrained[0].Satisfied);
        Assert.Equal("workflows", fineGrained[0].MissingPermission);
    }

    [Fact]
    public void PermissionValidatorAcceptsFineGrainedWritePermissionForRead()
    {
        var validator = new GitHubPermissionValidator();
        var result = validator.Validate(
            GitHubCredentialType.FineGrainedPersonalAccessToken,
            new HashSet<string>(["actions:write"]),
            [GitHubPermissionRequirements.ActionsRead]);

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
    public async Task AuthenticationServiceTransitionsToAuthenticatedWithoutExposingToken()
    {
        var store = new FakeSecureCredentialStore();
        var userService = new FakeUserService(new GitHubUserInfo(42, "octocat", "Mona", "https://github.com/octocat"));
        var service = new GitHubAuthenticationService(store, userService, new FakeEffectivePermissionValidator());

        var result = await service.AuthenticateAsync("ghp_test", "test", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(GitHubAuthenticationState.Authenticated, result.State);
        Assert.Equal("octocat", result.Account?.Login);
        Assert.Null(result.Credential?.Label is null ? null : result.Credential.GetType().GetProperty("Token"));
        Assert.Equal("ghp_test", (await store.GetActiveAsync(TestContext.Current.CancellationToken))?.Token);
    }

    [Fact]
    public async Task MissingCredentialTransitionsToMissingState()
    {
        var service = new GitHubAuthenticationService(
            new FakeSecureCredentialStore(),
            new FakeUserService(null),
            new FakeEffectivePermissionValidator());

        var result = await service.ValidateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(GitHubAuthenticationState.MissingCredential, result.State);
        Assert.Null(result.Account);
    }

    [Fact]
    public async Task InvalidCredentialTransitionsToInvalidState()
    {
        var store = new FakeSecureCredentialStore();
        await store.SaveAsync(GitHubCredentialSecret.Create("id", "bad"), TestContext.Current.CancellationToken);
        var userService = new FakeUserService(exception: new GitHubServiceException(
            GitHubErrorCategory.Authentication,
            "Authentication failed.",
            401));

        var service = new GitHubAuthenticationService(store, userService, new FakeEffectivePermissionValidator());
        var result = await service.ValidateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(GitHubAuthenticationState.InvalidCredential, result.State);
        Assert.Null(result.Account);
    }

    [Fact]
    public async Task PatAuthenticationProviderAddsBearerHeaderWithoutLoggingOrReturningToken()
    {
        var provider = new GitHubPatAuthenticationProvider(
            new FakeCredentialProvider(GitHubCredentialSecret.Create("id", "secret-token")));

        var request = new RequestInformation();
        await provider.AuthenticateRequestAsync(request, null, TestContext.Current.CancellationToken);

        Assert.Equal("Bearer secret-token", request.Headers["Authorization"].First());
        Assert.DoesNotContain("secret-token", request.Headers.ToString());
    }

    [Fact]
    public async Task AuthenticationServiceMarksVerifiedMissingPermissionAsInsufficient()
    {
        var store = new FakeSecureCredentialStore();
        var userService = new FakeUserService(new GitHubUserInfo(42, "octocat", "Mona", "https://github.com/octocat"));
        var missing = new GitHubPermissionResult("actions-write", false, "actions", "Forbidden.", true);
        var service = new GitHubAuthenticationService(
            store,
            userService,
            new FakeEffectivePermissionValidator([missing]));

        await service.AuthenticateAsync("ghp_test", cancellationToken: TestContext.Current.CancellationToken);
        var permissions = await service.ValidatePermissionsAsync(
            "octocat",
            "repo",
            TestContext.Current.CancellationToken);

        Assert.Contains(permissions, permission => permission.Capability == "actions-write" && !permission.Satisfied);
        Assert.Equal(GitHubAuthenticationState.InsufficientPermissions, service.GetCurrentState().State);
    }

    [Fact]
    public void EvidenceValidatorDoesNotTreatUnverifiedCapabilitiesAsSatisfied()
    {
        var validator = new GitHubPermissionValidator();
        var result = validator.ValidateEvidence(
            GitHubCredentialType.FineGrainedPersonalAccessToken,
            new Dictionary<string, GitHubPermissionObservation>
            {
                [GitHubPermissionRequirements.RepositoryContentsWrite.Capability] =
                    GitHubPermissionObservation.Unverified("A write probe would mutate state.")
            });

        var contents = result.Single(permission =>
            permission.Capability == GitHubPermissionRequirements.RepositoryContentsWrite.Capability);

        Assert.False(contents.Satisfied);
        Assert.False(contents.Verified);
    }

    [Fact]
    public void OrdinaryAuthenticationModelsContainNoTokenProperty()
    {
        Assert.DoesNotContain(typeof(GitHubCredentialInfo).GetProperties(),
            property => property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(GitHubAccount).GetProperties(),
            property => property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeEffectivePermissionValidator(IReadOnlyList<GitHubPermissionResult>? result = null) : IGitHubEffectivePermissionValidator
    {
        public Task<IReadOnlyList<GitHubPermissionResult>> ValidateAsync(
            GitHubCredentialType credentialType,
            string? owner = null,
            string? repository = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result ?? (IReadOnlyList<GitHubPermissionResult>)[]);
    }

    private sealed class FakeCredentialProvider(GitHubCredentialSecret? credential) : IGitHubCredentialProvider
    {
        public Task<GitHubCredentialSecret?> GetActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(credential);
    }

    private sealed class FakeUserService(GitHubUserInfo? user = null, Exception? exception = null) : IGitHubUserService
    {
        public Task<GitHubUserInfo> GetAuthenticatedUserAsync(CancellationToken cancellationToken = default) =>
            exception is not null ? Task.FromException<GitHubUserInfo>(exception) :
            user is null ? Task.FromException<GitHubUserInfo>(new InvalidOperationException("No user configured.")) :
            Task.FromResult(user);
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
