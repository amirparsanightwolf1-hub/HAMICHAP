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

## Machine workload matrix

Below the shared calendar is a scrollable utilization table with **one row per active machine** (Polar, Heidelberg, Nilpeter 1/2, Wenchun, Tops 102, Sakurai, SBL and Folder-gluer). Its columns correspond to the currently visible period: individual days for Month/Week and individual hours for Day. Each occupied cell shows the order and operation. Empty cells indicate availability. Historical overlapping assignments, if present, display a visible red warning.

**No double-booking:** the application rejects any new or moved booking on a machine if its time interval intersects another active booking on that machine. Endpoints are half-open: if one job ends at 14:00, another may start at 14:00, but not at 13:59. The existing conflicting order, operation and time range are shown when an assignment is rejected. Conflict checks and writes are enclosed in a SQL Server Serializable transaction to protect concurrent manager scheduling attempts. Other applications writing ProductionSchedule directly must also enforce this invariant; SQL Server does not yet have a table-wide overlap constraint.

## Manual acceptance tests

1. Select a CEO order planned for a specific week. Confirm Month/Week/Day calendars look the same as ProductionPlanning, with its original colors.
2. Split a 6-hour order into 1-hour Polar, 3-hour Heidelberg, 1-hour Wenchun and 1-hour SBL operations; confirm their total matches the CEO budget.
3. Select the Heidelberg stage and click a permitted calendar slot. The card appears in the CEO-style calendar **and** in the Heidelberg machine workload row.
4. Schedule another stage on a different machine at the same time; permit it. Schedule a second stage overlapping Heidelberg at the same time; **reject** it with a message identifying the conflicting booking.
5. Schedule an operation immediately after a prior operation ends on the same machine; permit it. Move a planned operation by selecting it and clicking a different free slot.
6. Attempt to place an operation outside the CEO start/end, or into a CEO-prohibited shift; reject it. Ensure lunch is excluded from productive-time duration.
7. Check red warnings for pre-existing overlapping data, English/Persian and RTL layout, Floor Manager read-only permissions, and two nearly simultaneous scheduling requests.
8. Confirm the manager page has no draggable cards, drop zones or required drag-and-drop interactions.

No .NET SDK, automated integration suite or browser was available during these changes. Keep draft PR #4 unmerged until build, database concurrency and UI testing pass.
