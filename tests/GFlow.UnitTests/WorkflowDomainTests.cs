using GFlow.Core.Workflows;
using GFlow.Core.Workflows.Yaml;

namespace GFlow.UnitTests;

public sealed class WorkflowDomainTests
{
    [Fact]
    public void WorkflowCreation_ShouldExposeNameAndIdentity()
    {
        var workflow = new Workflow("Build");

        Assert.Equal("Build", workflow.Name);
        Assert.NotEqual(Guid.Empty, workflow.Id);
    }

    [Fact]
    public void TriggerCreation_ShouldPreserveEventAndConfiguration()
    {
        var branches = new YamlSequence([new YamlScalar("main")]);
        var configuration = new YamlMapping();
        configuration.Set("branches", branches);
        var trigger = new Trigger("push", configuration);

        Assert.Equal("push", trigger.EventName);
        Assert.Same(configuration, trigger.Configuration);
    }

    [Fact]
    public void JobCreation_ShouldRepresentRunnerAndName()
    {
        var job = new Job("build")
        {
            Name = "Build",
            RunsOn = new YamlScalar("ubuntu-latest")
        };

        Assert.Equal("build", job.Id);
        Assert.Equal("Build", job.Name);
        Assert.Equal("ubuntu-latest", Assert.IsType<string>(((YamlScalar)job.RunsOn).Value));
    }

    [Fact]
    public void ActionStepCreation_ShouldSupportGenericWithValues()
    {
        var step = new ActionStep("actions/checkout@v4") { Name = "Checkout" };
        step.With.Set("repository", new YamlScalar("owner/repository"));
        step.With.Set("ref", new YamlScalar("main"));

        Assert.Equal("actions/checkout@v4", step.Uses);
        Assert.Equal("owner/repository", ((YamlScalar)step.With["repository"]).GetValue<string>());
        Assert.Equal("main", ((YamlScalar)step.With["ref"]).GetValue<string>());
    }

    [Fact]
    public void RunStepCreation_ShouldRepresentCommandAndShell()
    {
        var step = new RunStep("dotnet build") { Name = "Build", Shell = "bash" };

        Assert.Equal("dotnet build", step.Run);
        Assert.Equal("bash", step.Shell);
    }

    [Fact]
    public void ScalarYamlValues_ShouldPreserveTypes()
    {
        var text = new YamlScalar("value");
        var boolean = new YamlScalar(true);
        var integer = new YamlScalar(10L);
        var number = new YamlScalar(1.5m);
        var nullValue = YamlScalar.Null;

        Assert.Equal(YamlScalarKind.String, text.Kind);
        Assert.Equal("value", text.GetValue<string>());
        Assert.Equal(YamlScalarKind.Boolean, boolean.Kind);
        Assert.True(boolean.GetValue<bool>());
        Assert.Equal(YamlScalarKind.Integer, integer.Kind);
        Assert.Equal(10L, integer.GetValue<long>());
        Assert.Equal(YamlScalarKind.Number, number.Kind);
        Assert.Equal(1.5m, number.GetValue<decimal>());
        Assert.Equal(YamlScalarKind.Null, nullValue.Kind);
        Assert.Null(nullValue.Value);
    }

    [Fact]
    public void MappingYamlValues_ShouldStoreTypedNestedValues()
    {
        var mapping = new YamlMapping();
        mapping.Set("enabled", new YamlScalar(true));
        mapping.Set("values", new YamlSequence([new YamlScalar("one"), new YamlScalar("two")]));

        Assert.True(((YamlScalar)mapping["enabled"]).GetValue<bool>());
        Assert.Equal(2, ((YamlSequence)mapping["values"]).Count);
    }

    [Fact]
    public void SequenceYamlValues_ShouldPreserveOrder()
    {
        var sequence = new YamlSequence();
        sequence.Add(new YamlScalar("one"));
        sequence.Add(new YamlScalar("two"));

        Assert.Equal("one", ((YamlScalar)sequence[0]).GetValue<string>());
        Assert.Equal("two", ((YamlScalar)sequence[1]).GetValue<string>());
    }

    [Fact]
    public void ArbitraryNestedYamlValues_ShouldSupportMappingSequenceMapping()
    {
        var root = new YamlMapping();
        var bar = new YamlMapping();
        var values = new YamlSequence();
        var first = new YamlMapping();
        first.Set("enabled", new YamlScalar(true));
        first.Set("values", new YamlSequence([new YamlScalar("one"), new YamlScalar("two")]));
        values.Add(first);
        bar.Set("items", values);
        root.Set("bar", bar);

        var result = (YamlMapping)((YamlMapping)root["bar"])["items"];
        var item = (YamlMapping)((YamlSequence)result)[0];

        Assert.True(((YamlScalar)item["enabled"]).GetValue<bool>());
        Assert.Equal("two", ((YamlScalar)((YamlSequence)item["values"])[1]).GetValue<string>());
    }

    [Fact]
    public void JobDependencies_ShouldPreventSelfDependencyAndDuplicateEntries()
    {
        var job = new Job("build");
        job.AddNeed("prepare");
        job.AddNeed("prepare");

        Assert.Single(job.Needs);
        Assert.Equal("prepare", job.Needs[0]);
        Assert.Throws<ArgumentException>(() => job.AddNeed("build"));
    }

    [Fact]
    public void MatrixStrategy_ShouldRepresentDimensionsIncludeAndExclude()
    {
        var strategy = new MatrixStrategy { FailFast = false, MaxParallel = 2 };
        strategy.Dimensions.Set("os", new YamlSequence([new YamlScalar("ubuntu-latest"), new YamlScalar("windows-latest")]));
        strategy.Dimensions.Set("dotnet", new YamlSequence([new YamlScalar("9.0.x"), new YamlScalar("10.0.x")]));
        strategy.Include.Add(new YamlMapping([new("os", new YamlScalar("ubuntu-latest")), new("experimental", new YamlScalar(true))]));
        strategy.Exclude.Add(new YamlMapping([new("os", new YamlScalar("windows-latest")), new("dotnet", new YamlScalar("9.0.x"))]));

        Assert.False(strategy.FailFast!.Value);
        Assert.Equal(2, strategy.MaxParallel);
        Assert.Equal(2, strategy.Dimensions.Count);
        Assert.Equal(2, ((YamlSequence)strategy.Dimensions["os"]).Count);
        Assert.Single(strategy.Include);
        Assert.Single(strategy.Exclude);
    }

    [Fact]
    public void RepresentativeWorkflow_ShouldComposeTriggersJobsActionsRunAndMatrix()
    {
        var workflow = new Workflow("Build");
        var push = new Trigger("push");
        var pushConfig = new YamlMapping();
        pushConfig.Set("branches", new YamlSequence([new YamlScalar("main")]));
        push.Configuration = pushConfig;
        workflow.Triggers.Add(push);

        workflow.Environment.Set("CI", new YamlScalar(true));
        workflow.Permissions.Set("contents", PermissionLevel.Read);

        var prepare = new Job("prepare") { RunsOn = new YamlScalar("ubuntu-latest") };
        prepare.AddStep(new RunStep("echo prepare") { Name = "Prepare" });

        var build = new Job("build") { RunsOn = new YamlScalar("${{ matrix.os }}") };
        build.AddNeed("prepare");
        var strategy = new MatrixStrategy();
        strategy.Dimensions.Set("os", new YamlSequence([new YamlScalar("ubuntu-latest"), new YamlScalar("windows-latest")]));
        strategy.Dimensions.Set("dotnet", new YamlSequence([new YamlScalar("9.0.x"), new YamlScalar("10.0.x")]));
        build.Strategy = strategy;

        var checkout = new ActionStep("actions/checkout@v4") { Name = "Checkout" };
        checkout.With.Set("repository", new YamlScalar("owner/repository"));
        checkout.With.Set("ref", new YamlScalar("main"));
        build.AddStep(checkout);

        var setup = new ActionStep("actions/setup-dotnet@v4") { Name = "Build" };
        setup.With.Set("dotnet-version", new YamlScalar("${{ matrix.dotnet }}"));
        build.AddStep(setup);

        workflow.AddJob(prepare);
        workflow.AddJob(build);

        Assert.Equal(2, workflow.Jobs.Count);
        Assert.Single(workflow.Triggers);
        Assert.Equal("prepare", workflow.Jobs[1].Needs[0]);
        Assert.Equal(2, workflow.Jobs[1].Strategy!.Dimensions.Count);
        Assert.Equal(2, workflow.Jobs[1].Steps.Count);
        Assert.Equal(PermissionLevel.Read, workflow.Permissions.Values["contents"]);
    }
}
