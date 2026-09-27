using System.Net;
using System.Text;
using GFlow.GitHub;
using GFlow.GitHub.Contracts;
using GFlow.GitHub.Services;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace GFlow.UnitTests;

public sealed class GitHubServicesTests
{
    [Fact]
    public async Task RepositoryService_MapsGeneratedRepository()
    {
        var handler = new StubHandler(_ => Json(
            """
            {
              "id": 42,
              "name": "gflow",
              "full_name": "weslleymurdock/gflow",
              "private": true,
              "default_branch": "main",
              "html_url": "https://github.com/weslleymurdock/gflow"
            }
            """));

        var service = CreateRepositoryService(handler);

        var repository = await service.GetAsync("weslleymurdock", "gflow");

        Assert.Equal(42, repository.Id);
        Assert.Equal("gflow", repository.Name);
        Assert.Equal("weslleymurdock/gflow", repository.FullName);
        Assert.True(repository.IsPrivate);
        Assert.Equal("main", repository.DefaultBranch);
    }

    [Fact]
    public async Task FileService_UsesCurrentShaWhenUpdating()
    {
        var handler = new StubHandler(request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Json(
                    """
                    {
                      "type": "file",
                      "path": ".github/workflows/build.yml",
                      "sha": "old-sha",
                      "encoding": "base64",
                      "content": "bmFtZTogYnVpbGQK"
                    }
                    """);
            }

            return Json(
                """
                {
                  "content": {
                    "path": ".github/workflows/build.yml",
                    "sha": "new-sha",
                    "html_url": "https://github.com/weslleymurdock/gflow/blob/main/.github/workflows/build.yml"
                  },
                  "commit": {
                    "sha": "commit-sha"
                  }
                }
                """);
        });

        var service = CreateFileService(handler);
        var current = await service.GetAsync("weslleymurdock", "gflow", ".github/workflows/build.yml", "main");

        await service.WriteAsync(
            "weslleymurdock",
            "gflow",
            new GitHubFileWriteRequest(
                ".github/workflows/build.yml",
                "name: build\n",
                "Update workflow",
                "main",
                current.Sha));

        Assert.Equal("old-sha", current.Sha);
        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains(""sha":"old-sha"", handler.LastRequestBody);
        Assert.Contains(""branch":"main"", handler.LastRequestBody);
        Assert.Contains("bmFtZTogYnVpbGQK", handler.LastRequestBody);
    }

    [Fact]
    public async Task WorkflowService_DiscoversYamlFilesUnderWorkflowDirectory()
    {
        var handler = new StubHandler(_ => Json(
            """
            [
              {
                "type": "file",
                "name": "build.yml",
                "path": ".github/workflows/build.yml",
                "sha": "sha-build",
                "html_url": "https://github.com/example/repo/blob/main/.github/workflows/build.yml"
              },
              {
                "type": "file",
                "name": "release.yaml",
                "path": ".github/workflows/release.yaml",
                "sha": "sha-release",
                "html_url": "https://github.com/example/repo/blob/main/.github/workflows/release.yaml"
              },
              {
                "type": "file",
                "name": "README.md",
                "path": ".github/workflows/README.md",
                "sha": "sha-readme"
              }
            ]
            """));

        var service = CreateWorkflowService(handler);

        var files = await service.DiscoverFilesAsync("weslleymurdock", "gflow", "main");

        Assert.Equal(2, files.Count);
        Assert.Contains(files, file => file.Path.EndsWith("build.yml"));
        Assert.Contains(files, file => file.Path.EndsWith("release.yaml"));
        Assert.DoesNotContain(files, file => file.Path.EndsWith("README.md"));
    }

    [Fact]
    public async Task WorkflowService_Dispatch_BuildsRefAndInputs()
    {
        var handler = new StubHandler(request =>
            Json(
                """
                {
                  "workflow_run_id": 123,
                  "run_url": "https://api.github.com/repos/example/repo/actions/runs/123",
                  "html_url": "https://github.com/example/repo/actions/runs/123"
                }
                """));

        var service = CreateWorkflowService(handler);

        var run = await service.DispatchAsync(
            "example",
            "repo",
            new GitHubWorkflowDispatchRequest(
                "build.yml",
                "feature/test",
                new Dictionary<string, string> { ["configuration"] = "Release" }));

        Assert.Equal(123, run?.Id);
        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains(""ref":"feature/test"", handler.LastRequestBody);
        Assert.Contains(""configuration":"Release"", handler.LastRequestBody);
    }

    [Fact]
    public async Task WorkflowRunService_MapsStatusAndConclusion()
    {
        var handler = new StubHandler(_ => Json(
            """
            {
              "id": 123,
              "name": "Build",
              "status": "completed",
              "conclusion": "success",
              "head_branch": "main",
              "head_sha": "abc123",
              "html_url": "https://github.com/example/repo/actions/runs/123",
              "created_at": "2026-09-27T12:00:00Z",
              "updated_at": "2026-09-27T12:05:00Z"
            }
            """));

        var service = CreateRunService(handler);
        var run = await service.GetAsync("example", "repo", 123);

        Assert.Equal("completed", run.Status);
        Assert.Equal("success", run.Conclusion);
        Assert.Equal("main", run.Branch);
        Assert.Equal("abc123", run.Sha);
    }

    [Fact]
    public async Task WorkflowRunService_Cancel_UsesCancelEndpoint()
    {
        var handler = new StubHandler(request =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });

        var service = CreateRunService(handler);
        await service.CancelAsync("example", "repo", 123);

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.EndsWith("/repos/example/repo/actions/runs/123/cancel", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task Service_MapsUnauthorizedResponse()
    {
        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"message\":\"Bad credentials\"}", Encoding.UTF8, "application/json")
            });

        var service = CreateRepositoryService(handler);

        var exception = await Assert.ThrowsAsync<GitHubServiceException>(
            () => service.GetAsync("example", "repo"));

        Assert.Equal(GitHubErrorCategory.Authentication, exception.Category);
        Assert.Equal(401, exception.StatusCode);
    }

    private static IGitHubRepositoryService CreateRepositoryService(StubHandler handler)
    {
        var client = CreateClient(handler);
        return new GitHubRepositoryService(client);
    }

    private static IGitHubFileService CreateFileService(StubHandler handler) =>
        new GitHubFileService(CreateClient(handler));

    private static IGitHubWorkflowService CreateWorkflowService(StubHandler handler) =>
        new GitHubWorkflowService(CreateClient(handler), CreateFileService(handler));

    private static IGitHubWorkflowRunService CreateRunService(StubHandler handler) =>
        new GitHubWorkflowRunService(CreateClient(handler));

    private static GitHubClient CreateClient(StubHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com") };
        var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient)
        {
            BaseUrl = "https://api.github.com"
        };
        return new GitHubClient(adapter);
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }
}
