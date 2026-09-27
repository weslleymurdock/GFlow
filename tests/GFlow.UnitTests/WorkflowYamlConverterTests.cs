using GFlow.Core.Workflows;
using GFlow.Core.Workflows.Yaml;
using GFlow.Yaml;

namespace GFlow.UnitTests;

public sealed class WorkflowYamlConverterTests
{
    private readonly WorkflowYamlConverter _converter = new();

    [Fact]
    public void ParsesLiteralOnKeyAndConfiguredTriggers()
    {
        const string yaml = """
            name: Build
            on:
              push:
                branches:
                  - main
              pull_request:
                branches:
                  - main
            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - run: dotnet build
            """;

        var workflow = _converter.Parse(yaml);
        Assert.Equal("Build", workflow.Name);
        Assert.Equal(2, workflow.Triggers.Count);
        Assert.Equal("push", workflow.Triggers[0].EventName);
        Assert.Equal("pull_request", workflow.Triggers[1].EventName);
        Assert.IsType<YamlMapping>(workflow.Triggers[0].Configuration);
    }

    [Fact]
    public void ParsesAllSupportedTriggerForms()
    {
        var scalar = _converter.Parse("""
            name: Scalar
            on: push
            jobs:
              build: { steps: [{ run: echo hi }] }
            """);
        Assert.Single(scalar.Triggers);
        Assert.Equal("push", scalar.Triggers[0].EventName);

        var sequence = _converter.Parse("""
            name: Sequence
            on:
              - push
              - pull_request
            jobs:
              build: { steps: [{ run: echo hi }] }
            """);
        Assert.Equal(2, sequence.Triggers.Count);

        var empty = _converter.Parse("""
            name: Empty
            on:
              workflow_dispatch:
            jobs:
              build: { steps: [{ run: echo hi }] }
            """);
        Assert.Null(empty.Triggers[0].Configuration);
    }

    [Fact]
    public void ParsesEnvironmentPermissionsAndJobGraph()
    {
        var workflow = _converter.Parse("""
            name: Build
            on: push
            env:
              CI: true
            permissions:
              contents: read
              actions: write
            jobs:
              prepare:
                steps:
                  - run: echo prepare
              build:
                needs:
                  - prepare
                env:
                  RETRIES: 3
                permissions: read-all
                runs-on: ${{ matrix.os }}
                strategy:
                  fail-fast: false
                  max-parallel: 2
                  matrix:
                    os:
                      - ubuntu-latest
                      - windows-latest
                    dotnet:
                      - "9.0.x"
                      - "10.0.x"
                    include:
                      - os: ubuntu-latest
                        experimental: true
                    exclude:
                      - os: windows-latest
                        dotnet: "9.0.x"
                steps:
                  - name: Build
                    env:
                      ENABLED: false
                    run: echo build
            """);

        Assert.Equal(YamlScalarKind.Boolean, ((YamlScalar)workflow.Environment["CI"]).Kind);
        Assert.Equal(PermissionLevel.Read, workflow.Permissions.Values["contents"]);
        Assert.Equal(PermissionLevel.Write, workflow.Permissions.Values["actions"]);
        Assert.Single(workflow.Jobs[1].Needs);
        Assert.Equal("prepare", workflow.Jobs[1].Needs[0]);
        Assert.Equal(PermissionPolicy.ReadAll, workflow.Jobs[1].Permissions.Policy);
        Assert.Equal("${{ matrix.os }}", ((YamlScalar)workflow.Jobs[1].RunsOn!).GetValue<string>());
        Assert.False(workflow.Jobs[1].Strategy!.FailFast);
        Assert.Equal(2, workflow.Jobs[1].Strategy.MaxParallel);
        Assert.Equal(2, workflow.Jobs[1].Strategy.Dimensions.Count);
        Assert.Single(workflow.Jobs[1].Strategy.Include);
        Assert.Single(workflow.Jobs[1].Strategy.Exclude);
    }

    [Fact]
    public void ParsesNestedActionWithAndTypedValues()
    {
        var workflow = _converter.Parse("""
            name: Build
            on: push
            jobs:
              build:
                steps:
                  - uses: actions/setup-dotnet@v4
                    with:
                      configuration:
                        enabled: true
                        targets:
                          - linux
                          - windows
                      count: 2
                      ratio: 1.5
                      missing: null
            """);

        var with = ((ActionStep)workflow.Jobs[0].Steps[0]).With;
        Assert.IsType<YamlMapping>(with["configuration"]);
        Assert.Equal(YamlScalarKind.Integer, ((YamlScalar)with["count"]).Kind);
        Assert.Equal(YamlScalarKind.Number, ((YamlScalar)with["ratio"]).Kind);
        Assert.Equal(YamlScalarKind.Null, ((YamlScalar)with["missing"]).Kind);
    }

    [Fact]
    public void ParsesRunStepCommonPropertiesAndMultilineCommand()
    {
        var workflow = _converter.Parse("""
            name: Build
            on: push
            jobs:
              build:
                steps:
                  - name: Build
                    if: ${{ matrix.enabled }}
                    run: |
                      dotnet restore
                      dotnet build
                    shell: bash
                    continue-on-error: false
                    timeout-minutes: 10
            """);

        var step = Assert.IsType<RunStep>(workflow.Jobs[0].Steps[0]);
        Assert.Equal("Build", step.Name);
        Assert.Equal("${{ matrix.enabled }}", step.If);
        Assert.Equal("dotnet restore\ndotnet build\n", step.Run);
        Assert.Equal("bash", step.Shell);
        Assert.False(step.ContinueOnError);
        Assert.Equal(10, step.TimeoutMinutes);
    }

    [Fact]
    public void SerializesAndParsesRepresentativeWorkflow()
    {
        var workflow = _converter.Parse(RepresentativeWorkflow);
        var yaml = _converter.Serialize(workflow);

        Assert.Contains("on:", yaml);
        Assert.Contains("${{ matrix.os }}", yaml);

        var roundTrip = _converter.Parse(yaml);
        Assert.Equal(workflow.Name, roundTrip.Name);
        Assert.Equal(workflow.Triggers.Count, roundTrip.Triggers.Count);
        Assert.Equal(workflow.Jobs.Count, roundTrip.Jobs.Count);
        Assert.Equal(workflow.Jobs[1].Needs[0], roundTrip.Jobs[1].Needs[0]);
        Assert.Equal(workflow.Jobs[1].Strategy!.Dimensions.Count, roundTrip.Jobs[1].Strategy!.Dimensions.Count);
        Assert.Equal(((RunStep)workflow.Jobs[0].Steps[0]).Run, ((RunStep)roundTrip.Jobs[0].Steps[0]).Run);
        Assert.Equal(((ActionStep)workflow.Jobs[1].Steps[0]).Uses, ((ActionStep)roundTrip.Jobs[1].Steps[0]).Uses);
    }

    [Fact]
    public void InvalidYamlProducesActionableError()
    {
        var exception = Assert.Throws<WorkflowYamlException>(() => _converter.Parse("""
            name: [invalid
            """));
        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingRequiredWorkflowStructureFails()
    {
        var missingOn = Assert.Throws<WorkflowYamlException>(() => _converter.Parse("""
            name: Build
            jobs: {}
            """));
        Assert.Contains("'on'", missingOn.Message);

        var missingJobs = Assert.Throws<WorkflowYamlException>(() => _converter.Parse("""
            name: Build
            on: push
            """));
        Assert.Contains("'jobs'", missingJobs.Message);
    }

    [Fact]
    public void SerializedNullTriggerConfigurationCanBeParsedAgain()
    {
        var workflow = new Workflow("Dispatch");
        workflow.Triggers.Add(new Trigger("workflow_dispatch"));
        workflow.AddJob(new Job("build"));

        var yaml = _converter.Serialize(workflow);

        Assert.Contains("workflow_dispatch:", yaml);
        Assert.Null(_converter.Parse(yaml).Triggers[0].Configuration);
    }

    private const string RepresentativeWorkflow = """
        name: Build
        on:
          push:
            branches:
              - main
          pull_request:
            branches:
              - main
        env:
          CI: true
        permissions:
          contents: read
        jobs:
          prepare:
            name: Prepare
            runs-on: ubuntu-latest
            steps:
              - name: Prepare
                run: |
                  echo prepare
                  echo ready
          build:
            name: Build
            needs:
              - prepare
            strategy:
              fail-fast: false
              max-parallel: 2
              matrix:
                os:
                  - ubuntu-latest
                  - windows-latest
                dotnet:
                  - "9.0.x"
                  - "10.0.x"
                include:
                  - os: ubuntu-latest
                    experimental: true
                exclude:
                  - os: windows-latest
                    dotnet: "9.0.x"
            runs-on: ${{ matrix.os }}
            env:
              CONFIGURATION: Release
            permissions:
              contents: read
            steps:
              - name: Checkout
                uses: actions/checkout@v4
                with:
                  repository: owner/repository
                  ref: main
              - name: Setup .NET
                uses: actions/setup-dotnet@v4
                with:
                  dotnet-version: ${{ matrix.dotnet }}
              - name: Build
                if: ${{ matrix.experimental != true }}
                run: dotnet build --configuration $CONFIGURATION
                shell: bash
                continue-on-error: false
                timeout-minutes: 10
        """;
