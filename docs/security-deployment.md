# Security hardening and deployment

Before deploying:
- Restrict repository visibility if possible. Customer artwork and the old default administrator password remain in Git history and may have been copied. Review access, rotate the exposed administrator password and invalidate existing sessions. Removing current files is not a history purge.
- Back up the live SQL Server database and all deployed `wwwroot/uploads` files. Restore/copy required uploads to the deployment's existing uploads directory if deploying to a new location; do not overwrite or delete customer data. Uploads are intentionally omitted from source/publish artifacts.
- On first installation only, configure `BootstrapAdmin__Email` and `BootstrapAdmin__Password` (at least 16 characters, Identity complexity requirements) via server environment/secret store. Remove these configuration values after provisioning. Existing active administrators are preserved and not automatically reset.
- Accounts are provisioned by administrators. Public registration and its development email-confirmation page are disabled. Real email delivery and self-service recovery are not configured.
- Assign Sales.Requests.View and Sales.Requests.Create explicitly to appropriate employee roles. Customers.Create is needed for inline customer creation; Sales.Requests.CreateQuotation is needed when attaching a new job to a quotation.
- Verify anonymous business routes redirect to login, signed-in users without sales permissions cannot view/create jobs, inactive users cannot sign in, failed password attempts trigger lockout, and authenticated artwork previews continue working. Test role-specific actions and existing accounts before production rollout.
- Anonymous access to legacy /uploads URLs is blocked. Active authenticated users can still access files by URL; per-job permissions and moving artwork outside the web root are separate follow-up changes.
