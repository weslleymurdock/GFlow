# GFlow Development Stages

## Architectural boundaries

GFlow's core workflow model remains independent of GitHub HTTP clients, MAUI, Blazor, and platform APIs.

### Stage 03 — GitHub REST integration

Stage 03 owns the GitHub REST boundary: the generated Kiota GitHubClient, GFlow-facing GitHub services, application-facing GitHub contracts, REST error mapping, and the authenticated IRequestAdapter boundary.

Generated Kiota files are never hand-edited. Stage 03 does not persist PATs or implement account management.

### Stage 04 — Authentication and secure credential storage

Stage 04 owns authentication and secure credential storage.

The boundary is:

    Secure credential storage
            |
    GitHub authentication/session abstraction
            |
    Kiota authentication provider/request adapter
            |
    Generated GitHubClient
            |
    Stage 03 GitHub services

The first credential type is a Personal Access Token. Raw PAT material is restricted to authentication infrastructure and is never exposed through ordinary account, session, workflow, or project models.

PATs are stored only through ISecureCredentialStore and the .NET MAUI SecureStorage implementation. They must never be persisted in JSON files, preferences, ordinary application data, workflow documents, logs, analytics, or source-controlled files.

Classic PATs and fine-grained PATs are distinct permission models. Classic PAT validation uses OAuth-style scopes. Fine-grained PAT validation uses named repository/account permissions with read/write access and is never interpreted as classic OAuth scopes.

GFlow permission requirements are explicitly modeled so later capabilities can extend them without coupling application code to PATs.

Authentication reaches Kiota through IAuthenticationProvider. The PAT provider obtains the active secret only at the request boundary and adds the authorization header there. The generated Kiota client remains the sole GitHub REST implementation.

Authentication failures remain distinguishable from authorization, not-found, conflict, validation, rate-limit, server, cancellation, and unknown failures through the Stage 03 error abstraction. Raw PATs and authorization headers must never be logged.

### Stage 05 — Repository and workflow management

Stage 05 consumes authenticated Stage 03 GitHub services for repository and workflow management:

    Authenticated account -> repositories -> repository -> branch -> .github/workflows -> workflow

Stage 05 owns repository/workflow application flows and UI. It must not move PAT storage into workflow/project persistence or bypass Stage 03 GitHub services.

## Stage 04 exit criteria

A credential can be securely stored and removed, validated through the Stage 03 authenticated-user service, associated with an authenticated account without exposing the PAT, and supplied to the Kiota request adapter. Classic scopes and fine-grained permissions remain explicitly separated.
