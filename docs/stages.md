# GFlow Development Stages

## Product Goal

GFlow is a .NET MAUI Blazor application for visually creating, editing, validating, storing, and executing GitHub Actions workflows.

The application supports two execution targets:

1. GitHub Actions: publish the workflow to a GitHub repository and execute it through GitHub Actions.
2. Local Docker / act: execute Linux-compatible workflows locally through nektos/act, using Docker.

The local executor is a development/testing target, not a claim of full GitHub-hosted-runner equivalence. act executes Actions in Docker containers and its images/environment can differ from GitHub-hosted runners.

On Windows, the initial local executor may install act through Chocolatey:

~~~powershell
choco install act-cli
~~~

Docker is required by the normal act execution model.

---

# Architectural Principles

## 1. Workflow YAML is the source of truth

The visual editor operates on a model that can faithfully represent GitHub Actions YAML.

Visual Editor -> GFlow Workflow Model -> YAML -> .github/workflows/*.yml

Existing workflow files follow the reverse path:

.github/workflows/*.yml -> YAML Parser -> GFlow Workflow Model -> Visual Editor

GFlow must not introduce a proprietary workflow format that is only approximately translated into GitHub Actions.

## 2. GitHub integration is infrastructure

The core workflow model must not depend on GitHub HTTP clients, MAUI, Blazor, or platform APIs. GitHub REST operations belong behind application abstractions.

## 3. Execution is provider-based

Use a provider abstraction conceptually equivalent to:

~~~text
IWorkflowExecutor
├── GitHubActionsWorkflowExecutor
└── ActWorkflowExecutor
~~~

Both providers consume the same workflow model.

## 4. Actions are metadata-driven

A GitHub Action must not require a custom UI component for every Action. The application maintains an Action index based primarily on action.yml/action.yaml metadata, and that metadata drives the configuration UI.

## 5. Secrets are not ordinary workflow values

PATs, credentials, repository secrets, and local execution secrets must have explicit secure-storage semantics and must never be silently persisted as ordinary workflow data.

---

# Stage 01 — Core Workflow Domain Model

## Objective

Create the framework-independent domain model for GitHub Actions workflows.

Support at least:

- Workflow
- Trigger
- Job
- Step
- ActionStep
- RunStep
- job dependencies
- matrix strategy
- environment variables
- permissions
- generic YAML values

The YAML value model must distinguish:

- Scalar
- Mapping/Object
- Sequence/Array

and allow arbitrary nesting.

Example:

~~~yaml
with:
  configuration:
    enabled: true
    targets:
      - linux
      - windows
~~~

The model must support job dependencies:

~~~yaml
jobs:
  build:
    needs:
      - prepare
~~~

and matrix strategies:

~~~yaml
strategy:
  matrix:
    os:
      - ubuntu-latest
      - windows-latest
    dotnet:
      - "10.0.x"
      - "9.0.x"
~~~

Do not implement GitHub HTTP integration, authentication, YAML serialization/parsing, visual editing, Action discovery, or act in this stage.

## Exit Criteria

The core can construct representative workflows containing triggers, jobs, steps, Actions, shell commands, with, env, if, needs, matrix strategy, and permissions. Unit tests cover the model.

---

# Stage 02 — YAML Parsing and Serialization

## Objective

Implement bidirectional conversion:

~~~text
YAML -> Workflow Model
Workflow Model -> YAML
~~~

Support nested mappings/sequences, scalar types, multiline values, and the supported GitHub Actions structure.

Existing workflow YAML must be loadable even if it was not created by GFlow.

## Exit Criteria

Representative real-world workflows can be loaded, edited in memory, and serialized as valid GitHub Actions YAML.

---

# Stage 03 — Application Contracts and GitHub REST Integration

## Objective

Build the GFlow-facing GitHub contracts, adapters, and services on top of the Kiota-generated GitHub REST client already present in `GFlow.GitHub`.

The REST boundary is generated from the GitHub OpenAPI description:

```text
GitHub OpenAPI
      ↓
Kiota
      ↓
GFlow.GitHub generated client
      ↓
GFlow GitHub services/adapters
      ↓
Application-facing contracts
```

Kiota owns the complete GitHub REST request builders and generated request/response models. GFlow must not recreate that infrastructure with handwritten HTTP clients, endpoint DTOs, or duplicated request bodies.

The local Kiota tool is defined by `dotnet-tools.json`. The authoritative generated-client regeneration script is:

```powershell
dotnet tool restore
.\scripts\generate-gflow-github.ps1
```

Generated files under `src/GFlow.GitHub` must not be hand-edited. Changes to the GitHub OpenAPI surface are made by updating the source description and regenerating the client.

## Application-facing services

Group operations by GitHub capability rather than by individual endpoint. Stage 03 provides focused services such as:

- `IGitHubUserService`;
- `IGitHubRepositoryService`;
- `IGitHubBranchService`;
- `IGitHubFileService`;
- `IGitHubWorkflowService`;
- `IGitHubWorkflowRunService`.

The services expose application-oriented contracts and map the generated Kiota models at the boundary. Required capabilities include:

- authenticated user;
- repository listing, lookup, and creation;
- branch/ref discovery;
- workflow file retrieval;
- workflow file creation/update;
- workflow discovery under `.github/workflows`;
- workflow dispatch;
- workflow run retrieval and status/conclusion;
- workflow run cancellation.

File updates preserve GitHub blob SHA semantics for optimistic concurrency. Asynchronous operations propagate cancellation tokens, and paginated GitHub operations expose page controls.

## Authentication boundary

Stage 03 owns the GitHub REST boundary and only requires an authenticated Kiota `IRequestAdapter` to be supplied to the generated `GitHubClient`.

Stage 03 does not persist credentials or implement PAT account management. Authentication and secure credential storage belong to Stage 04.

## Error handling

GitHub/Kiota failures are mapped at the service boundary into a small application-facing error abstraction covering authentication, authorization, not found, conflict, validation, rate limit, server, cancellation, and unknown failures. The original HTTP status is retained when available.

## Testing

Service tests use local HTTP/Kiota infrastructure and do not require a GitHub token or live repository. They verify mapping, file SHA update semantics, workflow discovery and dispatch request construction, run status/conclusion mapping, cancellation, and representative error mapping.

## Exit Criteria

The application layer can express all Stage 03 GitHub operations through GFlow services while the core remains independent from GitHub and Kiota. The generated Kiota client is the sole REST implementation, authentication storage remains deferred to Stage 04, and the service/adaptor tests pass without live GitHub credentials.

---

# Stage 04 — Authentication and Secure Credential Storage

## Objective

Implement GitHub authentication and secure credential persistence.

The first credential type may be a Personal Access Token, but the abstraction must allow future authentication mechanisms.

Support:

- adding a credential;
- validating the token;
- obtaining authenticated-user information;
- secure storage;
- invalidation/removal;
- active-account selection.

## PAT permission validation

For classic PATs, validate required OAuth scopes.

For fine-grained PATs, validate required repository/account permissions rather than treating them as classic scopes.

The application must explicitly model the permissions required by GFlow features and explain missing permissions to the user.

The permission set must account for:

- repository metadata/read;
- repository contents read/write;
- workflow-related access where required;
- repository creation;
- Actions workflow/run operations.

Requirements must be verified against the specific GitHub REST endpoints used by the application.

## Security

Stage 04 owns the authentication/session abstraction and platform secure credential storage. PATs are never ordinary workflow or project data.

Never store PATs in JSON files, ordinary preferences, ordinary application data, workflow documents, logs, analytics, or source-controlled files. The raw token remains behind the secure credential abstraction and Kiota authentication provider.

Classic PATs and fine-grained PATs are distinct permission models. Classic tokens use OAuth-style scopes; fine-grained tokens use repository/account permissions and read/write access. GFlow models required capabilities explicitly rather than treating fine-grained permissions as OAuth scopes.

Stage 03 continues to own all GitHub REST operations and error mapping. Stage 04 supplies the authenticated Kiota adapter and validates authentication/permission state for those capabilities.

## Exit Criteria

A user can authenticate, the authenticated GitHub user can be resolved, required capabilities are explicitly modeled and validated, the credential is stored only through platform secure storage, and the raw PAT is never exposed through ordinary workflow/project models.

---

# Stage 05 — Repository and Workflow Management

Stage 05 consumes the authenticated GitHub services provided by Stage 03. It owns repository/workflow management and UI, not credential storage or the GitHub REST implementation.

## Objective

Implement:

GitHub account -> repositories -> repository -> branch -> .github/workflows -> workflow

Support:

- repository creation;
- repository selection;
- branch selection;
- workflow creation;
- existing workflow loading;
- workflow editing;
- workflow saving;
- workflow file creation/update through REST.

Handle optimistic concurrency using the current file/blob SHA where required.

## Exit Criteria

A user can create/select a repository and persist a valid GFlow workflow into .github/workflows/<name>.yml.

---

# Stage 06 — Action Index and Action Metadata

## Objective

Create an index of GitHub Actions usable as workflow commands/steps.

Support metadata such as:

- owner;
- repository;
- version/reference;
- display name;
- description;
- action type;
- inputs;
- outputs;
- required inputs;
- defaults;
- input descriptions;
- documentation URL;
- branding metadata where available.

The primary metadata source should be action.yml/action.yaml.

The index must distinguish the Action reference from the configuration instance placed into a workflow.

Example:

~~~text
actions/checkout@v4
~~~

is an Action reference, while its with configuration belongs to the workflow step instance.

## Exit Criteria

The application can resolve an Action reference into metadata consumable by the visual editor.

---

# Stage 07 — Dynamic Action Configuration Editor

## Objective

Create the visual configuration editor for Action steps.

Action metadata generates the configurable inputs; Actions must not require individual hard-coded Blazor components.

## Recursive key/value editor

The editor must support a progressive key/value experience.

A scalar can become an object or sequence:

~~~text
key = value

key
  └── child = value

key
  ├── item 0
  └── item 1
~~~

The editor must distinguish scalar, mapping, and sequence values and allow arbitrary nesting and dynamic sibling insertion.

## Exit Criteria

Users can configure Action inputs through a generic metadata-driven UI and the result maps directly to the workflow model.

---

# Stage 08 — Visual Workflow Editor

## Objective

Implement the main visual workflow editor.

Users can:

- create workflows;
- configure workflow name;
- configure triggers;
- create jobs;
- configure job properties;
- add Action steps;
- add shell/run steps;
- reorder steps;
- remove steps;
- configure env;
- configure conditions;
- configure permissions;
- configure job dependencies;
- configure matrix strategies.

The editor may reuse block-based concepts from NAutomate, but GitHub Actions semantics remain authoritative.

## Exit Criteria

A complete workflow can be assembled visually and previewed as valid YAML.

---

# Stage 09 — Matrix and Job Dependency Authoring

## Objective

Provide first-class UI support for GitHub Actions orchestration.

## Matrix

Support matrix dimensions, values, include, exclude, fail-fast, max-parallel, and matrix expressions where supported.

Example:

~~~yaml
strategy:
  matrix:
    os:
      - ubuntu-latest
      - windows-latest
    version:
      - "9.0.x"
      - "10.0.x"
~~~

## Dependencies

Support:

~~~yaml
jobs:
  build:
    needs:
      - prepare

  deploy:
    needs:
      - build
~~~

The editor must make dependencies understandable and the model must prevent invalid references such as a job depending on itself.

## Exit Criteria

Matrix workflows and multi-job dependency graphs can be created and serialized correctly.

---

# Stage 10 — GitHub Actions Execution Provider

## Objective

Implement execution against GitHub Actions.

Support:

- workflow dispatch;
- branch/ref selection;
- workflow inputs;
- run tracking;
- status/conclusion;
- run information;
- cancellation where supported.

The provider is independent from the local act provider.

## Exit Criteria

A saved workflow can be dispatched through GitHub Actions and tracked in GFlow.

---

# Stage 11 — Local Docker / act Execution Provider

## Objective

Add optional local execution for Linux-compatible workflows through nektos/act.

The first implementation targets Windows. The application detects:

- act availability;
- act version;
- Docker availability;
- Docker version where practical.

The application should offer explicit setup rather than silently installing software.

## Execution

The provider invokes act and Docker and exposes:

- command invocation;
- process output;
- exit code;
- cancellation;
- execution state;
- diagnostics.

The application must not assume GitHub and act executions are identical.

## Linux-only scope

Initially support Linux workflows.

Workflows targeting Windows or macOS GitHub-hosted runners are reported as unsupported locally unless a future provider adds support.

The UI should distinguish supported, unsupported, and unknown local compatibility.

## Secrets

Local execution secrets must not be written into workflow YAML. They must be supplied through secure process/environment mechanisms appropriate to act.

## Exit Criteria

A Linux-compatible workflow can execute locally through Docker/act and its output/status is visible in GFlow.

---

# Stage 12 — Unified Execution Experience

## Objective

Expose execution target selection:

~~~text
Execution target

( ) GitHub Actions
( ) Local Docker / act
~~~

The application clearly communicates the semantic differences.

GitHub:

~~~text
GFlow -> GitHub API -> GitHub Actions
~~~

Local:

~~~text
GFlow -> act -> Docker -> Linux container
~~~

The workflow definition remains shared.

## Exit Criteria

A compatible workflow can be selected for either execution provider.

---

# Stage 13 — Validation and Compatibility Analysis

## Objective

Validate workflows before saving or executing.

Validate:

- YAML validity;
- workflow structure;
- required properties;
- invalid job references;
- invalid needs relationships;
- matrix consistency;
- Action input requirements;
- missing required inputs;
- unsupported local targets;
- unsupported runner configurations;
- authentication permission problems.

Diagnostics are classified as errors, warnings, or informational messages.

## Exit Criteria

The editor explains actionable problems before publishing or execution.

---

# Stage 14 — Workflow Persistence and Local Projects

## Objective

Persist local workflow drafts and non-sensitive project metadata.

Persist:

- workflow drafts;
- repository/branch metadata;
- Action index cache;
- editor preferences;
- non-sensitive configuration.

Credentials remain exclusively in secure storage.

The local project format must be versioned for migration.

## Exit Criteria

A workflow draft can be closed and reopened without exposing credentials.

---

# Stage 15 — Action Index Synchronization and Caching

## Objective

Make the Action index practical.

Support:

- initial indexing;
- incremental updates;
- version/reference caching;
- invalidation;
- offline cached metadata;
- Action search;
- owner/repository filtering;
- version selection.

## Exit Criteria

Previously indexed Actions can be used without unnecessary repeated metadata downloads.

---

# Stage 16 — GitHub Repository Workflow UX

## Objective

Complete the repository-oriented workflow experience.

Support:

- repository creation;
- branch selection;
- workflow listing;
- workflow creation;
- workflow editing;
- workflow deletion;
- workflow update/versioning;
- workflow run history;
- run details;
- local-vs-GitHub execution selection.

Handle authentication failures, permission errors, conflicts, missing files, and API rate limits clearly.

## Exit Criteria

GFlow functions as a complete visual GitHub Actions workflow client for the supported feature set.

---

# Goal 1 — Functional GitHub Actions Workflow Client

Goal 1 is reached when all of the following are functional:

1. Authenticate to GitHub.
2. Validate PAT permissions before enabling dependent features.
3. Store credentials securely.
4. Create a GitHub repository.
5. Select a repository and branch.
6. Create a workflow visually.
7. Create multiple jobs.
8. Create job dependencies through needs.
9. Create matrix strategies.
10. Add shell commands and GitHub Actions as steps.
11. Generate Action configuration from Action metadata.
12. Edit nested YAML values through the recursive configuration editor.
13. Load and save .github/workflows/*.yml.
14. Dispatch the workflow to GitHub Actions.
15. Track workflow execution.
16. Optionally execute Linux-compatible workflows locally through Docker/act.
17. Report local execution incompatibilities instead of claiming GitHub-runner equivalence.
18. Keep the visual model and generated YAML synchronized.

The first complete product loop is:

~~~text
Authenticate
    ↓
Create/select repository
    ↓
Create workflow
    ↓
Configure jobs/actions/matrix/dependencies
    ↓
Generate YAML
    ↓
Save to GitHub
    ↓
Execute on GitHub
       OR
Execute locally through act/Docker
    ↓
Observe result
~~~

---

# Future Goals

Future planning may cover:

- GitHub App authentication;
- organization administration;
- repository secrets and variables;
- reusable workflows;
- composite Actions;
- reusable workflow calls;
- expressions editor;
- artifacts;
- logs and annotations;
- workflow run comparison;
- advanced Action version management;
- local runner customization;
- pull-request based workflow publishing;
- workflow templates;
- collaborative editing;
- GFlow project import/export.
