# CEO production calendar and machine workload planning

## Design

The CEO continues to plan whole orders in /production/planning. The Production Manager sees the same Jalali Month/Week/Day calendar and original shift colors at /production/machine-schedule, with the CEO orders and extra operation cards on that shared calendar.

The Production Manager **manually splits** each order into operations by entering a name, machine and estimated duration. The original total estimated order minutes, allocated stage minutes and remaining minutes are displayed beside the calendar. New operations cannot exceed the CEO estimate. Each machine choice is stored in a machine-specific ProductionWorkCenter code with the existing WorkCenterId property; no schema migration is needed. The manager can edit or delete unscheduled operations.

## Click-based scheduling — no drag and drop

1. Select a production order, then a particular operation from the left panel.
2. Click a day in Month view to start from its first permitted working time; click an hour slot in Week/Day view to choose the start time.
3. The shared ProductionPlanningTime calculation skips lunch and any CEO-disallowed overtime/night shifts. The entire estimated operation must fit **within the CEO's exact planned start/end window**.
4. On success, the scheduled operation appears as a card on the same CEO calendar, labeled with order, machine and operation name. To move the stage, select it and click another slot. To remove it, click **Return to list**; the stage and dependent downstream planned bookings return to the unscheduled list. Released/running/completed bookings cannot be edited from this page.
5. The manager's calendar contains no drag/drop event handlers. No JavaScript drag helper is loaded.

## Manager confirmation and no simultaneous operations on the same order

**Sequence approval is mandatory before booking or moving any operation.**

1. The Production Manager selects an order, adds its operations and specifies an active machine and estimated hours for every stage.
2. The manager uses **Move up** / **Move down** in the operation list to arrange the intended production sequence. Reordering is blocked if any of that order's operations already has an active machine booking; first use **Return to list** to clear the bookings.
3. The manager clicks **Confirm operation sequence**. Confirmation is allowed only when step numbers are sequential, every step has a valid machine, the stage durations sum to the CEO's estimated order duration, and existing bookings (if any) already respect that sequence.
4. The approval is **persisted with the confirming user's ID, UTC timestamp and a SHA-256 fingerprint of the ordered operations**. Any add/edit/delete/reorder invalidates it; the fingerprint also detects changes made through other code paths. Changes to the CEO duration likewise require re-confirmation.
5. On every calendar placement or move, the application re-reads and verifies the approval **inside the serializable transaction**. Each earlier step of the order must already be scheduled and finish no later than the new step starts. Later scheduled steps must start no earlier than the new step ends.
6. **An order cannot be assigned to two operations at the same time even when the machines are different.** Before insertion, the application checks *every other active operation for the same order* for any interval overlap. The manager receives the conflicting operation and time range when a proposed booking is rejected. The existing separate one-task-per-machine rule also remains in force.
7. Historical order overlaps appear as warnings in the order list; invalid legacy bookings must be removed or corrected before confirmation.

### Required database migration

This PR adds nullable columns on `ProductionOrders`: `OperationsSequenceConfirmedAt`, `OperationsSequenceConfirmedBy` and `OperationsSequenceSignature`, using migration `20261111090100_AddOperationSequenceConfirmation`. Apply the EF Core database migration to the target SQL Server database **before opening the revised scheduler**, for example from the solution folder: `dotnet ef database update`. Existing orders start **unconfirmed** and require an explicit manager review; they are not auto-approved.

## Machine workload matrix

Below the shared calendar is a scrollable utilization table with **one row per active machine** (Polar, Heidelberg, Nilpeter 1/2, Wenchun, Tops 102, Sakurai, SBL and Folder-gluer). Its columns correspond to the currently visible period: individual days for Month/Week and individual hours for Day. Each occupied cell shows the order and operation. Empty cells indicate availability. Historical overlapping assignments, if present, display a visible red warning.

**No double-booking:** the application rejects any new or moved booking on a machine if its time interval intersects another active booking on that machine. Endpoints are half-open: if one job ends at 14:00, another may start at 14:00, but not at 13:59. The existing conflicting order, operation and time range are shown when an assignment is rejected. Conflict checks and writes are enclosed in a SQL Server Serializable transaction to protect concurrent manager scheduling attempts. Other applications writing ProductionSchedule directly must also enforce this invariant; SQL Server does not yet have a table-wide overlap constraint.

## Manual acceptance tests

1. Select a CEO order planned for a specific week. Confirm Month/Week/Day calendars look the same as ProductionPlanning, with its original colors.
2. Split a 6-hour order into 1-hour Polar, 3-hour Heidelberg, 1-hour Wenchun and 1-hour SBL operations; confirm their total matches the CEO budget.
3. Use Move up/down to establish the expected stage sequence, confirm it as a Production Manager, then select the Heidelberg stage and click a permitted calendar slot. The card appears in the CEO-style calendar **and** in the Heidelberg machine workload row.
4. Try placing a *different stage of the same order* on another machine while Heidelberg is active; **reject** it. Try assigning another order to an occupied Heidelberg slot; also **reject** it. Two different orders may run simultaneously on different free machines.
5. Schedule the second stage only after the first stage ends, and require all prior stages to be booked; adjacent end/start time boundaries are permitted. Move a planned operation by selecting it and clicking a different free slot, while maintaining stage order.
6. Attempt to place an operation outside the CEO start/end, or into a CEO-prohibited shift; reject it. Ensure lunch is excluded from productive-time duration.
7. Check red warnings for pre-existing overlapping data, English/Persian and RTL layout, Floor Manager read-only permissions, and two nearly simultaneous scheduling requests.
8. Verify that edits or reordering revoke confirmation and scheduling is blocked until the manager reconfirms. Verify approval persists across page reload and different manager sessions. Confirm no drag-and-drop remains.

No .NET SDK, automated integration suite or browser was available during these changes. Keep draft PR #4 unmerged until build, database concurrency and UI testing pass.
