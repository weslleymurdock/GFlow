using System.Globalization;
using System.Text;
using GFlow.Core.Workflows;
using GFlow.Core.Workflows.Yaml;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace GFlow.Yaml;

/// <summary>Parses and serializes GitHub Actions workflows using YamlDotNet's representation model.</summary>
public sealed class WorkflowYamlConverter : IWorkflowYamlParser, IWorkflowYamlSerializer
{
    private const string On = "on";

    /// <inheritdoc />
    public Workflow Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        try
        {
            var stream = new YamlStream();
            using var reader = new StringReader(yaml);
            stream.Load(reader);
            if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode root)
                throw new WorkflowYamlException("The workflow document must contain exactly one YAML mapping at its root.");
            if (!TryGet(root, "name", out var nameNode) || nameNode is not YamlScalarNode nameScalar || string.IsNullOrWhiteSpace(nameScalar.Value))
                throw new WorkflowYamlException("The workflow must define a non-empty 'name' property.");
            if (!TryGet(root, On, out var triggerNode))
                throw new WorkflowYamlException("The workflow must define an 'on' trigger.");
            if (!TryGet(root, "jobs", out var jobsNode) || jobsNode is not YamlMappingNode jobsMapping)
                throw new WorkflowYamlException("The workflow must define a 'jobs' mapping.");

            var workflow = new Workflow(nameScalar.Value!);
            ParseTriggers(workflow.Triggers, triggerNode);
            if (TryGet(root, "env", out var env))
                CopyMapping(ParseMapping(env, "workflow env"), workflow.Environment);
            if (TryGet(root, "permissions", out var permissions))
                ParsePermissions(permissions, workflow.Permissions, "workflow permissions");

            foreach (var pair in jobsMapping.Children)
                workflow.AddJob(ParseJob(RequireScalarText(pair.Key, "job identifier"), pair.Value));
            return workflow;
        }
        catch (WorkflowYamlException) { throw; }
        catch (YamlException ex)
        {
            throw new WorkflowYamlException($"The workflow YAML is invalid at line {ex.Start.Line}, column {ex.Start.Column}: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidCastException or FormatException or OverflowException)
        {
            throw new WorkflowYamlException($"The workflow YAML could not be represented by the GFlow workflow model: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public string Serialize(Workflow workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        try
        {
            var root = new YamlMappingNode
            {
                { "name", StringScalar(workflow.Name) },
                { On, BuildTriggers(workflow.Triggers) }
            };
            if (workflow.Environment.Count > 0) root.Add("env", BuildMapping(workflow.Environment));
            if (workflow.Permissions.Policy != PermissionPolicy.Explicit || workflow.Permissions.Values.Count > 0)
                root.Add("permissions", BuildPermissions(workflow.Permissions));

            var jobs = new YamlMappingNode();
            foreach (var job in workflow.Jobs) jobs.Add(job.Id, BuildJob(job));
            root.Add("jobs", jobs);

            var stream = new YamlStream(new YamlDocument(root));
            var output = new StringBuilder();
            using var writer = new StringWriter(output, CultureInfo.InvariantCulture);
            stream.Save(writer, false);
            return output.ToString();
        }
        catch (WorkflowYamlException) { throw; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            throw new WorkflowYamlException($"The workflow could not be serialized: {ex.Message}", ex);
        }
    }

    private static void ParseTriggers(TriggerCollection target, YamlNode node)
    {
        switch (node)
        {
            case YamlScalarNode scalar when scalar.Value is not null:
                target.Add(new Trigger(scalar.Value)); return;
            case YamlSequenceNode sequence:
                foreach (var item in sequence.Children) target.Add(new Trigger(RequireScalarText(item, "trigger event")));
                return;
            case YamlMappingNode mapping:
                foreach (var pair in mapping.Children)
                {
                    var name = RequireScalarText(pair.Key, "trigger event name");
                    target.Add(new Trigger(name, IsNullNode(pair.Value) ? null : ToDomain(pair.Value)));
                }
                return;
            default: throw new WorkflowYamlException("The 'on' trigger must be a scalar, sequence, or mapping.");
        }
    }

    private static Job ParseJob(string id, YamlNode node)
    {
        var mapping = RequireMapping(node, $"job '{id}'");
        var job = new Job(id);
        if (TryGet(mapping, "name", out var name)) job.Name = RequireScalarText(name, $"job '{id}' name");
        if (TryGet(mapping, "runs-on", out var runsOn)) job.RunsOn = ToDomain(runsOn);
        if (TryGet(mapping, "needs", out var needs)) ParseNeeds(job, needs);
        if (TryGet(mapping, "env", out var env)) CopyMapping(ParseMapping(env, $"job '{id}' env"), job.Environment);
        if (TryGet(mapping, "permissions", out var permissions)) ParsePermissions(permissions, job.Permissions, $"job '{id}' permissions");
        if (TryGet(mapping, "strategy", out var strategy)) job.Strategy = ParseStrategy(strategy);
        if (TryGet(mapping, "if", out var condition)) job.If = RequireScalarText(condition, $"job '{id}' if");
        if (TryGet(mapping, "steps", out var steps)) ParseSteps(job, steps);
        return job;
    }

    private static void ParseNeeds(Job job, YamlNode node)
    {
        if (node is YamlScalarNode) { job.AddNeed(RequireScalarText(node, $"job '{job.Id}' needs")); return; }
        if (node is YamlSequenceNode sequence)
        {
            foreach (var item in sequence.Children) job.AddNeed(RequireScalarText(item, $"job '{job.Id}' needs item"));
            return;
        }
        throw new WorkflowYamlException($"The needs property of job '{job.Id}' must be a scalar or sequence.");
    }

    private static MatrixStrategy ParseStrategy(YamlNode node)
    {
        var mapping = RequireMapping(node, "strategy");
        var strategy = new MatrixStrategy();
        if (TryGet(mapping, "fail-fast", out var failFast)) strategy.FailFast = RequireBoolean(failFast, "strategy.fail-fast");
        if (TryGet(mapping, "max-parallel", out var maxParallel)) strategy.MaxParallel = checked((int)RequireInteger(maxParallel, "strategy.max-parallel"));
        if (!TryGet(mapping, "matrix", out var matrixNode)) return strategy;

        var matrix = RequireMapping(matrixNode, "strategy.matrix");
        foreach (var pair in matrix.Children)
        {
            var name = RequireScalarText(pair.Key, "matrix dimension name");
            if (name is not "include" and not "exclude") strategy.Dimensions.Set(name, ToDomain(pair.Value));
        }
        if (TryGet(matrix, "include", out var include)) CopySequence(ParseSequence(include, "strategy.matrix.include"), strategy.Include);
        if (TryGet(matrix, "exclude", out var exclude)) CopySequence(ParseSequence(exclude, "strategy.matrix.exclude"), strategy.Exclude);
        return strategy;
    }

    private static void ParseSteps(Job job, YamlNode node)
    {
        foreach (var item in RequireSequence(node, $"job '{job.Id}' steps").Children)
        {
            var mapping = RequireMapping(item, "workflow step");
            var hasUses = TryGet(mapping, "uses", out var uses);
            var hasRun = TryGet(mapping, "run", out var run);
            if (hasUses == hasRun) throw new WorkflowYamlException("Each workflow step must define exactly one of 'uses' or 'run'.");

            Step step = hasUses
                ? new ActionStep(RequireScalarText(uses!, "step uses"))
                : new RunStep(RequireScalarText(run!, "step run"));

            if (TryGet(mapping, "name", out var name)) step.Name = RequireScalarText(name, "step name");
            if (TryGet(mapping, "if", out var condition)) step.If = RequireScalarText(condition, "step if");
            if (TryGet(mapping, "continue-on-error", out var continueOnError)) step.ContinueOnError = RequireBoolean(continueOnError, "step continue-on-error");
            if (TryGet(mapping, "timeout-minutes", out var timeout)) step.TimeoutMinutes = checked((int)RequireInteger(timeout, "step timeout-minutes"));
            if (TryGet(mapping, "env", out var env)) CopyMapping(ParseMapping(env, "step env"), step.Environment);

            if (step is ActionStep action && TryGet(mapping, "with", out var with)) CopyMapping(ParseMapping(with, "step with"), action.With);
            if (step is RunStep runStep && TryGet(mapping, "shell", out var shell)) runStep.Shell = RequireScalarText(shell, "step shell");
            job.AddStep(step);
        }
    }

    private static void ParsePermissions(YamlNode node, PermissionSet target, string context)
    {
        if (node is YamlScalarNode scalar)
        {
            var value = RequireScalarText(scalar, context);
            if (value == "read-all") target.SetReadAll();
            else if (value == "write-all") target.SetWriteAll();
            else throw new WorkflowYamlException($"{context} must be 'read-all', 'write-all', or a mapping.");
            return;
        }

        target.SetExplicit();
        foreach (var pair in RequireMapping(node, context).Children)
        {
            var name = RequireScalarText(pair.Key, $"{context} name");
            var level = RequireScalarText(pair.Value, $"{context}.{name}") switch
            {
                "none" => PermissionLevel.None,
                "read" => PermissionLevel.Read,
                "write" => PermissionLevel.Write,
                _ => throw new WorkflowYamlException($"{context}.{name} must be 'none', 'read', or 'write'.")
            };
            target.Set(name, level);
        }
    }

    private static YamlMapping ParseMapping(YamlNode node, string context)
    {
        var result = new YamlMapping();
        foreach (var pair in RequireMapping(node, context).Children)
            result.Set(RequireScalarText(pair.Key, $"{context} key"), ToDomain(pair.Value));
        return result;
    }

    private static YamlSequence ParseSequence(YamlNode node, string context)
    {
        var result = new YamlSequence();
        foreach (var item in RequireSequence(node, context).Children) result.Add(ToDomain(item));
        return result;
    }

    private static YamlValue ToDomain(YamlNode node) => node switch
    {
        YamlMappingNode mapping => ParseMapping(mapping, "mapping"),
        YamlSequenceNode sequence => ParseSequence(sequence, "sequence"),
        YamlScalarNode scalar => ToScalar(scalar),
        _ => throw new WorkflowYamlException($"Unsupported YAML node type '{node.GetType().Name}'.")
    };

    private static YamlScalar ToScalar(YamlScalarNode scalar)
    {
        if (scalar.Value is null || scalar.Style == ScalarStyle.Plain && IsNullText(scalar.Value)) return YamlScalar.Null;
        if (scalar.Style != ScalarStyle.Plain) return new YamlScalar(scalar.Value!);
        if (bool.TryParse(scalar.Value, out var boolean)) return new YamlScalar(boolean);
        if (long.TryParse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return new YamlScalar(integer);
        if (decimal.TryParse(scalar.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return new YamlScalar(number);
        return new YamlScalar(scalar.Value!);
    }

    private static YamlNode BuildTriggers(TriggerCollection triggers)
    {
        var mapping = new YamlMappingNode();
        foreach (var trigger in triggers) mapping.Add(trigger.EventName, trigger.Configuration is null ? NullScalar() : BuildNode(trigger.Configuration));
        return mapping;
    }

    private static YamlMappingNode BuildJob(Job job)
    {
        var mapping = new YamlMappingNode();
        if (job.Name is not null) mapping.Add("name", StringScalar(job.Name));
        if (job.RunsOn is not null) mapping.Add("runs-on", BuildNode(job.RunsOn));
        if (job.Needs.Count == 1) mapping.Add("needs", StringScalar(job.Needs[0]));
        else if (job.Needs.Count > 1) mapping.Add("needs", BuildStringSequence(job.Needs));
        if (job.Environment.Count > 0) mapping.Add("env", BuildMapping(job.Environment));
        if (job.Permissions.Policy != PermissionPolicy.Explicit || job.Permissions.Values.Count > 0) mapping.Add("permissions", BuildPermissions(job.Permissions));
        if (job.Strategy is not null) mapping.Add("strategy", BuildStrategy(job.Strategy));
        if (job.If is not null) mapping.Add("if", StringScalar(job.If));
        if (job.Steps.Count > 0) mapping.Add("steps", BuildSteps(job.Steps));
        return mapping;
    }

    private static YamlSequenceNode BuildSteps(IReadOnlyList<Step> steps)
    {
        var result = new YamlSequenceNode();
        foreach (var step in steps)
        {
            var mapping = new YamlMappingNode();
            if (step.Name is not null) mapping.Add("name", StringScalar(step.Name));
            if (step.If is not null) mapping.Add("if", StringScalar(step.If));
            if (step.ContinueOnError.HasValue) mapping.Add("continue-on-error", BooleanScalar(step.ContinueOnError.Value));
            if (step.TimeoutMinutes.HasValue) mapping.Add("timeout-minutes", IntegerScalar(step.TimeoutMinutes.Value));
            if (step.Environment.Count > 0) mapping.Add("env", BuildMapping(step.Environment));

            switch (step)
            {
                case ActionStep action:
                    mapping.Add("uses", StringScalar(action.Uses));
                    if (action.With.Count > 0) mapping.Add("with", BuildMapping(action.With));
                    break;
                case RunStep run:
                    mapping.Add("run", StringScalar(run.Run, run.Run.Contains('\n') || run.Run.Contains('\r')));
                    if (run.Shell is not null) mapping.Add("shell", StringScalar(run.Shell));
                    break;
                default:
                    throw new WorkflowYamlException($"Unsupported workflow step type '{step.GetType().Name}'.");
            }
            result.Add(mapping);
        }
        return result;
    }

    private static YamlMappingNode BuildStrategy(MatrixStrategy strategy)
    {
        var mapping = new YamlMappingNode();
        if (strategy.FailFast.HasValue) mapping.Add("fail-fast", BooleanScalar(strategy.FailFast.Value));
        if (strategy.MaxParallel.HasValue) mapping.Add("max-parallel", IntegerScalar(strategy.MaxParallel.Value));
        if (strategy.Dimensions.Count > 0 || strategy.Include.Count > 0 || strategy.Exclude.Count > 0)
        {
            var matrix = BuildMapping(strategy.Dimensions);
            if (strategy.Include.Count > 0) matrix.Add("include", BuildSequence(strategy.Include));
            if (strategy.Exclude.Count > 0) matrix.Add("exclude", BuildSequence(strategy.Exclude));
            mapping.Add("matrix", matrix);
        }
        return mapping;
    }

    private static YamlNode BuildPermissions(PermissionSet permissions) => permissions.Policy switch
    {
        PermissionPolicy.ReadAll => StringScalar("read-all"),
        PermissionPolicy.WriteAll => StringScalar("write-all"),
        PermissionPolicy.Explicit => BuildMapping((YamlMapping)permissions.ToYaml()),
        _ => throw new WorkflowYamlException($"Unsupported permission policy '{permissions.Policy}'.")
    };

    private static YamlMappingNode BuildMapping(YamlMapping mapping)
    {
        var result = new YamlMappingNode();
        foreach (var pair in mapping) result.Add(StringScalar(pair.Key), BuildNode(pair.Value));
        return result;
    }

    private static YamlSequenceNode BuildSequence(YamlSequence sequence)
    {
        var result = new YamlSequenceNode();
        foreach (var value in sequence) result.Add(BuildNode(value));
        return result;
    }

    private static YamlSequenceNode BuildStringSequence(IEnumerable<string> values)
    {
        var result = new YamlSequenceNode();
        foreach (var value in values) result.Add(StringScalar(value));
        return result;
    }

    private static YamlNode BuildNode(YamlValue value) => value switch
    {
        YamlMapping mapping => BuildMapping(mapping),
        YamlSequence sequence => BuildSequence(sequence),
        YamlScalar scalar => BuildScalar(scalar),
        _ => throw new WorkflowYamlException($"Unsupported YAML value type '{value.GetType().Name}'.")
    };

    private static YamlScalarNode BuildScalar(YamlScalar scalar) => scalar.Kind switch
    {
        YamlScalarKind.Null => NullScalar(),
        YamlScalarKind.Boolean => BooleanScalar(scalar.GetValue<bool>()),
        YamlScalarKind.Integer => IntegerScalar(scalar.GetValue<long>()),
        YamlScalarKind.Number => NumberScalar(scalar.GetValue<decimal>()),
        YamlScalarKind.String => StringScalar(scalar.GetValue<string>()),
        _ => throw new WorkflowYamlException($"Unsupported scalar kind '{scalar.Kind}'.")
    };

    private static YamlScalarNode StringScalar(string value, bool multiline = false) => new(value)
    {
        Style = multiline ? ScalarStyle.Literal : ScalarStyle.DoubleQuoted
    };

    private static YamlScalarNode BooleanScalar(bool value) => new(value ? "true" : "false");
    private static YamlScalarNode IntegerScalar(long value) => new(value.ToString(CultureInfo.InvariantCulture));
    private static YamlScalarNode NumberScalar(decimal value) => new(value.ToString(CultureInfo.InvariantCulture));
    private static YamlScalarNode NullScalar() => new() { Value = null };

    private static bool TryGet(YamlMappingNode mapping, string key, out YamlNode value)
    {
        foreach (var pair in mapping.Children)
            if (pair.Key is YamlScalarNode scalar && scalar.Value == key) { value = pair.Value; return true; }
        value = null!;
        return false;
    }

    private static YamlMappingNode RequireMapping(YamlNode node, string context) =>
        node as YamlMappingNode ?? throw new WorkflowYamlException($"{context} must be a YAML mapping.");

    private static YamlSequenceNode RequireSequence(YamlNode node, string context) =>
        node as YamlSequenceNode ?? throw new WorkflowYamlException($"{context} must be a YAML sequence.");

    private static string RequireScalarText(YamlNode node, string context) =>
        node is YamlScalarNode scalar && scalar.Value is not null ? scalar.Value :
        throw new WorkflowYamlException($"{context} must be a scalar string.");

    private static bool RequireBoolean(YamlNode node, string context) =>
        node is YamlScalarNode scalar && bool.TryParse(scalar.Value, out var value) ? value :
        throw new WorkflowYamlException($"{context} must be a Boolean.");

    private static long RequireInteger(YamlNode node, string context) =>
        node is YamlScalarNode scalar && long.TryParse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value :
        throw new WorkflowYamlException($"{context} must be an integer.");

    private static bool IsNullText(string value) =>
        value.Length == 0 || value.Equals("null", StringComparison.OrdinalIgnoreCase) || value == "~";

    private static bool IsNullNode(YamlNode node) =>
        node is YamlScalarNode scalar && (scalar.Value is null || scalar.Style == ScalarStyle.Plain && IsNullText(scalar.Value));

    private static void CopyMapping(YamlMapping source, YamlMapping target)
    {
        foreach (var pair in source) target.Set(pair.Key, pair.Value);
    }

    private static void CopySequence(YamlSequence source, YamlSequence target)
    {
        foreach (var value in source) target.Add(value);
    }
}
