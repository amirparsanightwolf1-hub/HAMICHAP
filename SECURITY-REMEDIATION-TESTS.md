# Security remediation verification

This branch changes administrator bootstrap and inventory document authorization.

## Administrator bootstrap
- [ ] With no `BootstrapAdmin:Email` and `BootstrapAdmin:Password`, startup creates no default administrator.
- [ ] With both values provided securely through environment variables (`BootstrapAdmin__Email`, `BootstrapAdmin__Password`), a fresh database receives one administrator.
- [ ] An invalid bootstrap password fails provisioning rather than silently leaving an unprivileged account.
- [ ] Restart does not reset an existing administrator password.
- [ ] Rotate credentials for any administrator previously created with the old public password; review access logs.

## Inventory permissions
- [ ] Anonymous users cannot access inventory document list, create, or edit routes.
- [ ] Authenticated users without inventory document permissions are denied.
- [ ] Users with View can list/open documents but cannot save edits or post.
- [ ] Users with Create can create documents.
- [ ] Users with Edit can save drafts; users without Edit cannot.
- [ ] Users with Post can post documents; users without Post cannot.
- [ ] Administrator role retains access.
- [ ] Permissions changed during an active session are rechecked at mutation time.

## Follow-up hardening
- [ ] Audit all call sites of InventoryPostingService and other inventory write services for permission enforcement at service boundaries.
- [ ] Add automated integration tests for policy evaluation and authorization of interactive component actions.
- [ ] Run `dotnet restore`, `dotnet build`, and `dotnet test` in a .NET 10 environment.
