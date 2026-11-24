# Employee access hardening

This application is an internal employee ERP. Public registration, external sign-in and email-based recovery/change are unavailable until a verified email/provider flow is implemented. Existing administrator-created, confirmed employee accounts remain supported. Configure a secure administrator-assisted recovery procedure before rollout; never reset passwords by editing hashes directly.

Permissions are checked using current database membership and active status. Named route policies use existing permission keys; check employee grants before rollout. Production read permissions accept the existing Production.View and legacy Production.Orders.View names. Mutation permissions are separate from view permissions. The account cookie uses HTTPS and HttpOnly; validate IIS HTTPS configuration. Five failed password attempts lock eligible accounts for 15 minutes. Review existing LockoutEnabled values; the Identity option only controls new accounts.

## Staging checklist

- Signed-out requests to sales, inventory, customers, production and admin routes must be rejected; login, error and not-found pages must remain usable.
- Sign in as an active administrator and a typical employee. Test navigation and direct links with and without the required permissions. Test read-only users against save/post actions; test revoking permissions while a circuit is open.
- Verify customer create versus edit, new customer inside job creation, quotation create versus approval, inventory draft versus posting, sales order production creation and production/quality writes.
- Try five incorrect passwords for a test user with LockoutEnabled=true. Confirm subsequent correct password attempts remain locked until timeout; test a disabled account, including a previously authenticated circuit and passkey/2FA flow.
- Verify registration and confirmation pages do not create accounts or expose tokens; email recovery is explicitly unavailable. Test admin employee creation, login, password change, passkeys and authenticator MFA.
- Validate direct endpoints, including passkey creation, with and without a valid session.
- Run a full Release build and exercise the workflows in staging before merging. Static source changes do not establish complete security.

## Remaining scope

Hardcoded administrator seed credentials and public artwork storage are separate critical findings and remain unresolved by this patch. Review remaining business service entry points and resource-level authorization. This patch does not add email infrastructure, automated end-to-end tests, or a new administrator reset workflow.
