# CEO Calendar → automatic machine-operation schedule

## What each role controls

**CEO — `/production/planning`**: The original Sales-first Jalali Month/Week/Day calendar and shift coloring. The CEO determines the production order's earliest start, final deadline, allowable shifts and overall estimated productive duration. The Production Manager cannot change these order-level CEO fields here.

**Production Manager — `/production/machine-schedule`**: The **same CEO-style calendar**, showing order cards and automatically generated *operation/sub-order* cards with order number, stage number, assigned machine, operation description and duration. The manager edits stage estimates and order sequence, rather than dragging or clicking time slots. A machine workload grid below the calendar visualizes the bookings per machine and highlights historical conflicts. Floor Manager can view but not change the plan.

## Sales-derived stages

On opening Machine Schedule, orders with no route imported yet are read from their source `SalesOrder → Quotation → SalesRequest` and the `JobSpecificationJson` saved by `SheetJobEdit.razor` or `RollJobEdit.razor`. The `ProductionJobRouting` service generates initial operation stages:

- **Sheet**: Polar cutting when cutting dimensions are specified; Heidelberg offset printing as the default print stage; Wenchun lamination; Tops 102 / Heidelberg spot UV; Heidelberg varnish/cylinder coating; Sakurai screen printing where specified; SBL die-cutting; Folder-gluer for box assembly.
- **Roll**: Nilpeter 1 or 2 offset/flexo printing; Wenchun film/lamination; Folder-gluer when machine gluing is requested. Roll die-cutting, foil stamping and other finishing operations without a verified machine capability are flagged **REVIEW** for explicit manager machine selection.

Each initial stage gets an estimated share of the CEO's production duration, giving the manager editable starting values. Initial suggestions are created **only once** per production order, protected by `OperationsRouteInitialized`: later changes from the manager to machine assignments, stage names, durations and order sequence are preserved, and removed stages do not reappear on refresh. A **Restore suggested stages from Sales** button is available only if the order has no stages. A manual **Add operation** section remains collapsed unless needed.

For multi-machine capabilities (e.g. Nilpeter 1 or 2, Tops 102 or Heidelberg for spot UV), the scheduler chooses the **earliest-available compatible machine**. If a manager explicitly saves an alternative machine, that choice takes precedence. Unmapped stages cannot be scheduled until the manager chooses an active machine.

## Manager-driven automatic scheduling

1. Select a CEO production order. Review the prefilled stages, suggested equipment, and allocated and remaining duration totals.
2. Adjust each operation's estimated duration or machine as needed using **Edit operation → Save operation**. Use **Move up/down** to determine exact step sequence. Add or delete operations only when the suggested route needs correction.
3. The estimated stage durations must sum to the CEO total. The manager selects **Confirm sequence and generate schedule**. The confirmation captures their account, UTC confirmation time and a fingerprint of the current stage list.
4. The scheduler considers the earliest permitted CEO start and books the **entire route in sequence**, skipping lunch and CEO-excluded shifts. It searches each eligible machine's occupied intervals, advances past conflicts, and chooses the earliest feasible placement. It checks **the whole route** fits by the CEO deadline. Stage N+1 never starts until stage N ends, even if they run on different machines. One machine cannot run two tasks simultaneously.
5. Only if *every* stage fits does a serializable SQL transaction cancel old **Planned** bookings, save all the new bookings and confirm the ordered route **atomically**. On failure no partial automatic schedule or confirmation is committed; the UI explains the stage that cannot fit.
6. After confirmation, operation/sub-order cards automatically appear on the **shared CEO calendar** (Month/Week/Day) and in the machine workload rows. No drag/drop, manual click-to-place, or individual-slot buttons exist.
7. Editing, adding, removing or reordering stages invalidates confirmation and clears that order's **Planned** bookings. The manager confirms again to regenerate the schedule. Released, Running or Completed operations/bookings block such changes to avoid invalidating execution records.

The minute-level values are estimates, not measured actual throughput. Capacity is enforced through half-open booking intervals `[start,end)`: back-to-back bookings on one machine are allowed when the second starts exactly as the first finishes. During lunch and other excluded shifts the CEO clock skips production minutes, but an operation spanning a break still reserves its machine through that span. Different orders can run simultaneously on different free machines.

## Database migration

PR #4 contains two sequential nullable/additive migrations:

1. `20261111090100_AddOperationSequenceConfirmation` — manager confirmation metadata.
2. `20261111091500_AddAutomaticProductionRouteInitialization` — one-time Sales route import flag.

The second migration is **separate** because the first may already have been applied in development. Apply migrations before running the revised application:

```powershell
Update-Database
# or from the project directory:
dotnet ef database update
```

Do not merge the draft PR into main without explicit permission.

## Acceptance checklist (must run in a .NET/SQL Server environment)

- Create one Sheet specification with CMYK printing, lamination, spot UV, die-cutting. Open Machine Schedule. Verify stages and machines were suggested without pressing Add or clicking any calendar slot.
- Create a Roll order with printing and lamination; verify the Nilpeter 1/2 choices and automated earliest-available assignment.
- For a six-hour CEO order, enter sequential operation durations totaling six hours, confirm once, and verify the cards appear automatically on the CEO-style Month/Week/Day calendars and the machine workload matrix.
- Place another order on an occupied machine during the first order's slot; confirm that the automatic scheduler shifts it to a later available machine time, or rejects the entire plan if it cannot finish before its CEO deadline. Ensure it never double-books.
- Change an estimated duration or move a stage up/down. Verify old Planned bookings disappear and confirmation is invalidated; confirm again to regenerate the full route, with no same-order parallel steps.
- Remove one suggested operation, reload the page and verify it stays removed. If all stages are deleted, use Restore suggestions explicitly to rebuild the Sales-derived route.
- An unsupported finishing machine shows a REVIEW stage instead of guessing; the manager can assign a machine to that stage manually.
- Try to change a stage that has a Released/Running/Completed booking. The system must block modification.
- Test permissions, English/Persian localization, RTL, Jalali calendar, minute-level input and simultaneous scheduling sessions.

**Validation status:** Source-level edits only. A .NET SDK, running SQL Server and an interactive browser were not available to run the build, migrations, concurrency tests or UI tests from this environment. Keep the PR in draft until verified.
