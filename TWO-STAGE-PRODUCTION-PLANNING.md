# CEO deadlines and machine production scheduling

## Implemented
- The existing `/production/planning` route now presents a CEO-only production-order deadline editor. This writes `ProductionOrder.PlannedEndDate`, keeping the due date separate from machine-operation slots.
- `/production/machine-schedule` presents a date-filtered, machine-grouped operational calendar/list. Production Managers can generate and review operations, add missing steps, and schedule them on active machines. Floor Managers can view the schedule.
- The Home page displays appropriate entry points based on CEO, ProductionManager and FloorManager roles; Administrator can access both.
- The role seeder includes the new role names (assign users to those roles separately).
- The existing Sales job specifications are used to propose operations: sheet/roll printing when supported by explicit print fields, selected services, die cutting when a die is specified, and gluing when selected. Existing operations are never overwritten. Missing/invalid specifications require manual review.
- Existing `ProductionOperation`, `ProductionSchedule`, `Machine`, and `ProductionWorkCenter` tables are reused, avoiding a new migration.
- New interface labels and messages are added to both English and Persian resource files. Dates display in Persian when that UI language is active.

## Limitations requiring further work before production release
- The machine model has no work-center or capability mapping. Operations therefore **cannot yet be reliably restricted to compatible machines**. Configure an explicit capability relationship and validate it server-side.
- Operation duration defaults to **one hour for generated steps**, because no authoritative per-step duration model exists in job specifications. Production Managers must review and adjust durations before relying on the schedule.
- Selected-service strings are used as the operation names; they may not have stable translation keys. Canonical service identifiers/routing rules should replace this provisional mapping.
- The machine view is a date-filtered list grouped by machine, not a drag-and-drop time-grid calendar. It does not support moving already scheduled operations.
- Machine overlap and predecessor completion are checked before inserting schedules; machine working calendars, shifts, maintenance, race-condition-safe locking, and full cross-operation dependency validation are not yet implemented.
- There is no automated notification when the CEO changes a deadline.
- This branch inherits the separate CSS refactor branch and does **not** include the separate security remediation PR.
- **No .NET build, automated tests, or browser tests were run.** Keep draft until validated.

## Manual test plan
1. Seed roles and assign CEO, ProductionManager, FloorManager users.
2. CEO signs in, opens the deadline calendar, changes an order deadline, and confirms it persists.
3. Production Manager opens machine schedule, verifies automatic operations from a real Sheet and Roll job, reviews missing operations, and adds a manual operation.
4. Schedule a step and confirm it appears under the correct machine on the selected day.
5. Attempt to overlap a machine booking, schedule before an earlier operation, and schedule past the CEO deadline.
6. Floor Manager sees machine schedules but cannot edit or generate operations.
7. Test English/Persian switching, RTL layout, date entry, and responsive screens.
