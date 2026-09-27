using GFlow.Application.RepositoryWorkflow;
using GFlow.Core.Workflows;
using GFlow.Core.Workflows.Yaml;
using GFlow.GitHub.Authentication;
using GFlow.GitHub.Contracts;
using GFlow.GitHub.Services;

namespace GFlow.UnitTests;

public sealed class RepositoryWorkflowServiceTests
{
    [Fact]
    public async Task ListsRepositoriesThroughAuthenticatedInfrastructure()
    {
        var fakes = Create();
        var result = await fakes.Service.ListRepositoriesAsync(TestContext.Current.CancellationToken);
        Assert.Single(result);
        Assert.Equal("owner/repo", result[0].FullName);
    }

    [Fact]
    public async Task CreatesAndSelectsRepository()
    {
        var fakes = Create();
        var result = await fakes.Service.CreateRepositoryAsync(new("created"));
        Assert.Equal("owner/created", result.FullName);
        Assert.Same(result, fakes.Service.Context.Repository);
    }

    [Fact]
    public async Task ResolvesDefaultBranchWithoutHardCoding()
    {
        var fakes = Create();
        await fakes.Service.SelectRepositoryAsync("owner", "repo");
        var branch = await fakes.Service.SelectDefaultBranchAsync();
        Assert.Equal("trunk", branch.Name);
    }

    [Fact]
    public async Task SelectsBranchAndPreservesWorkflowContext()
    {
        var fakes = Create();
        await fakes.Service.SelectRepositoryAsync("owner", "repo");
        var branch = await fakes.Service.SelectBranchAsync("feature/test");
        Assert.Equal("feature/test", branch.Name);
        Assert.Same(branch, fakes.Service.Context.Branch);
    }

    [Fact]
    public async Task DiscoversBothYamlExtensions()
    {
        var fakes = Create();
        await fakes.Service.SelectRepositoryAsync("owner", "repo");
        await fakes.Service.SelectBranchAsync("trunk");
        var files = await fakes.Service.DiscoverWorkflowsAsync();
        Assert.Equal(2, files.Count);
        Assert.Contains(files, file => file.Path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, file => file.Path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LoadsWorkflowAndPreservesBlobSha()
    {
        var fakes = Create();
        await fakes.Service.SelectRepositoryAsync("owner", "repo");
        await fakes.Service.SelectBranchAsync("trunk");
        var document = await fakes.Service.LoadWorkflowAsync(".github/workflows/build.yaml");
        Assert.False(document.IsNew);
        Assert.Equal("blob-123", document.BlobSha);
        Assert.Equal("build", document.Model.Name);
        Assert.Equal("trunk", document.Branch);
    }

    [Fact]
    public async Task CreatesNewWorkflowWithSafeYamlPath()
    {
        var fakes = Create();
        await fakes.Service.SelectRepositoryAsync("owner", "repo");
        await fakes.Service.SelectBranchAsync("trunk");
        var document = fakes.Service.CreateWorkflow("release");
        Assert.True(document.IsNew);
        Assert.Equal(".github/workflows/release.yml", document.Path);
        Assert.Null(document.BlobSha);
    }

    [Fact]
    public async Task RejectsUnsafeWorkflowPath()
    {
        var fakes = Create();
        await fakes.Service.SelectRepositoryAsync("owner", "repo");
        await fakes.Service.SelectBranchAsync("trunk");
        Assert.Throws<GitHubServiceException>(() => fakes.Service.CreateWorkflow("../secrets"));
    }

    [Fact]
    public async Task NewWorkflowUsesCreateSemantics()
    {
        var fakes = Create();
        await fakes.Service.SelectRepositoryAsync("owner", "repo");
        await fakes.Service.SelectBranchAsync("trunk");
        var document = fakes.Service.CreateWorkflow("new");
        await fakes.Service.SaveWorkflowAsync(document);
        Assert.Null(fakes.Files.LastWrite.Sha);
        Assert.Equal("name: build", fakes.Files.LastWrite.Content);
    }

    [Fact]
    public async Task ExistingWorkflowUsesCurrentBlobSha()
    {
        var fakes = Create();
        await fakes.Service.SelectRepositoryAsync("owner", "repo");
        await fakes.Service.SelectBranchAsync("trunk");
        var document = await fakes.Service.LoadWorkflowAsync(".github/workflows/build.yml");
        await fakes.Service.SaveWorkflowAsync(document);
        Assert.Equal("blob-123", fakes.Files.LastWrite.Sha);
    }

    [Fact]
    public async Task SavingExistingWorkflowUpdatesEditingSha()
    {
        var fakes = Create();
        await fakes.Service.SelectRepositoryAsync("owner", "repo");
        await fakes.Service.SelectBranchAsync("trunk");
        var document = await fakes.Service.LoadWorkflowAsync(".github/workflows/build.yml");
        var saved = await fakes.Service.SaveWorkflowAsync(document);
        Assert.False(saved.IsNew);
        Assert.Equal("blob-new", saved.BlobSha);
    }

    [Fact]
    public async Task PropagatesConflictWithoutOverwriting()
    {
        var fakes = Create();
        fakes.Files.WriteException = new GitHubServiceException(GitHubErrorCategory.Conflict, "conflict", 409);
        await fakes.Service.SelectRepositoryAsync("owner", "repo");
        await fakes.Service.SelectBranchAsync("trunk");
        var document = await fakes.Service.LoadWorkflowAsync(".github/workflows/build.yml");
        var exception = await Assert.ThrowsAsync<GitHubServiceException>(() => fakes.Service.SaveWorkflowAsync(document));
        Assert.Equal(GitHubErrorCategory.Conflict, exception.Category);
        Assert.Equal("blob-123", fakes.Files.LastWrite.Sha);
    }

    [Fact]
    public async Task MissingWorkflowIsHandledByExistingErrorAbstraction()
    {
        var fakes = Create();
        fakes.Files.GetException = new GitHubServiceException(GitHubErrorCategory.NotFound, "missing", 404);
        await fakes.Service.SelectRepositoryAsync("owner", "repo");
        await fakes.Service.SelectBranchAsync("trunk");
        var exception = await Assert.ThrowsAsync<GitHubServiceException>(() => fakes.Service.LoadWorkflowAsync(".github/workflows/missing.yml"));
        Assert.Equal(GitHubErrorCategory.NotFound, exception.Category);
    }

    [Fact]
    public async Task MalformedYamlIsRejectedByStage02Parser()
    {
        var fakes = Create();
        fakes.Files.GetContent = "not: [valid";
        await fakes.Service.SelectRepositoryAsync("owner", "repo");
        await fakes.Service.SelectBranchAsync("trunk");
        await Assert.ThrowsAsync<WorkflowYamlException>(() => fakes.Service.LoadWorkflowAsync(".github/workflows/build.yml"));
    }

    [Fact]
    public async Task AuthenticationRequiredStatePreventsRepositoryAccess()
    {
        var fakes = Create(GitHubAuthenticationState.MissingCredential);
        var exception = await Assert.ThrowsAsync<GitHubServiceException>(() => fakes.Service.ListRepositoriesAsync());
        Assert.Equal(GitHubErrorCategory.Authorization, exception.Category);
        Assert.Equal(0, fakes.Repositories.ListCalls);
    }

    [Fact]
    public async Task CancellationIsPropagated()
    {
        var fakes = Create();
        fakes.Repositories.ListCancellation = TestContext.Current.CancellationToken;
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fakes.Service.ListRepositoriesAsync(cts.Token));
        Assert.NotNull(exception);
    }

    [Fact]
    public async Task WorkflowRoundTripUsesStage02Contracts()
    {
        var fakes = Create();
        var workflow = new Workflow("roundtrip");
        workflow.Triggers.Add(new GFlow.Core.Workflows.Trigger("push"));
        workflow.AddJob(new Job("build") { RunsOn = new GFlow.Core.Workflows.Yaml.YamlScalar("ubuntu-latest") });
        var yaml = fakes.Serializer.Serialize(workflow);
        var parsed = fakes.Parser.Parse(yaml);
        Assert.Equal("roundtrip", parsed.Name);
        Assert.Single(parsed.Jobs);
    }

    private static Fakes Create(GitHubAuthenticationState state = GitHubAuthenticationState.Authenticated)
    {
        var repositories = new FakeRepositories();
        var files = new FakeFiles();
        var service = new RepositoryWorkflowService(
            new FakeAuthentication(state),
            repositories,
            new FakeBranches(),
            files,
            new FakeWorkflows(files),
            new FakeParser(files),
            new FakeSerializer(),
            new RepositoryWorkflowContext());
        return new Fakes(service, repositories, files, new FakeParser(files), new FakeSerializer());
    }

    private sealed record Fakes(IRepositoryWorkflowService Service, FakeRepositories Repositories, FakeFiles Files, FakeParser Parser, FakeSerializer Serializer);

    private sealed class FakeAuthentication(GitHubAuthenticationState state) : IGitHubAuthenticationService
    {
        public Task<GitHubAuthenticationResult> AuthenticateAsync(string token, string? label = null, GitHubCredentialType? credentialType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result());
        public Task<GitHubAuthenticationResult> ValidateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result());
        }
        public GitHubAuthenticationResult GetCurrentState() => Result();
        public IReadOnlyList<GitHubPermissionResult> ValidatePermissions(GitHubCredentialType credentialType, IReadOnlySet<string> grantedPermissions, IEnumerable<GitHubPermissionRequirement> requirements) => [];
        public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        private GitHubAuthenticationResult Result() => new(state, null, state == GitHubAuthenticationState.Authenticated ? new(1, "user", "User", "url") : null, []);
    }

    private sealed class FakeRepositories : IGitHubRepositoryService
    {
        public int ListCalls { get; private set; }
        public CancellationToken ListCancellation { private get; set; }
        public Task<IReadOnlyList<GitHubRepositoryInfo>> ListAsync(int page = 1, int perPage = 100, CancellationToken cancellationToken = default)
        {
            ListCalls++;
            if (ListCancellation.CanBeCanceled) cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GitHubRepositoryInfo>>([new(1, "repo", "owner/repo", true, "trunk", "url")]);
        }
        public Task<GitHubRepositoryInfo> GetAsync(string owner, string repository, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GitHubRepositoryInfo(1, repository, $"{owner}/{repository}", false, "trunk", "url"));
        public Task<GitHubRepositoryInfo> CreateAsync(GitHubRepositoryCreateRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GitHubRepositoryInfo(2, request.Name, $"owner/{request.Name}", request.IsPrivate, "trunk", "url"));
    }

    private sealed class FakeBranches : IGitHubBranchService
    {
        public Task<IReadOnlyList<GitHubBranchInfo>> ListAsync(string owner, string repository, int page = 1, int perPage = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GitHubBranchInfo>>([new("trunk", "sha"), new("feature/test", "sha2")]);
    }

    private sealed class FakeFiles : IGitHubFileService
    {
        public GitHubFileWriteRequest LastWrite { get; private set; } = new("", "", "", "");
        public Exception? GetException { get; set; }
        public Exception? WriteException { get; set; }
        public string GetContent { get; set; } = "name: build\non:\n  push: {}\njobs:\n  build:\n    runs-on: ubuntu-latest\n    steps: []\n";
        public Task<GitHubFileInfo> GetAsync(string owner, string repository, string path, string? reference = null, CancellationToken cancellationToken = default)
        {
            if (GetException is not null) return Task.FromException<GitHubFileInfo>(GetException);
            return Task.FromResult(new GitHubFileInfo(path, GetContent, "blob-123", "url"));
        }
        public Task<GitHubFileInfo> WriteAsync(string owner, string repository, GitHubFileWriteRequest request, CancellationToken cancellationToken = default)
        {
            LastWrite = request;
            if (WriteException is not null) return Task.FromException<GitHubFileInfo>(WriteException);
            return Task.FromResult(new GitHubFileInfo(request.Path, request.Content, "blob-new", "url"));
        }
    }

    private sealed class FakeWorkflows(FakeFiles files) : IGitHubWorkflowService
    {
        public Task<IReadOnlyList<GitHubWorkflowInfo>> ListAsync(string owner, string repository, int page = 1, int perPage = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GitHubWorkflowInfo>>([]);
        public Task<GitHubWorkflowInfo> GetAsync(string owner, string repository, string workflow, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<GitHubFileInfo>> DiscoverFilesAsync(string owner, string repository, string? reference = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GitHubFileInfo>>([
                new(".github/workflows/build.yml", "", "sha1", "url"),
                new(".github/workflows/build.yaml", "", "sha2", "url")
            ]);
        public Task<GitHubWorkflowRunInfo?> DispatchAsync(string owner, string repository, GitHubWorkflowDispatchRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeParser(FakeFiles files) : IWorkflowYamlParser
    {
        public Workflow Parse(string yaml)
        {
            if (yaml.Contains("[valid", StringComparison.Ordinal)) throw new WorkflowYamlException("malformed");
            return new Workflow("build");
        }
    }

    private sealed class FakeSerializer : IWorkflowYamlSerializer
    {
        public string Serialize(Workflow workflow) => "name: build";
    }
}
