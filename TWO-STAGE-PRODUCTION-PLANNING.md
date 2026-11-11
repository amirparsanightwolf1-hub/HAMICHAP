# CEO Calendar and Production Manager Operation Planning

## The two calendars

- **CEO:** `/production/planning` remains the Sales-first original Month / Week / Day Jalali calendar with regular, lunch, overtime and night shift colors. CEO sets each order's planned start/end and the total estimated productive duration.
- **Production Manager:** `/production/machine-schedule` now renders **the same calendar grid and shift legend**—one calendar showing CEO production orders, with additional operation cards overlaid. This replaces the previous machine-by-machine date grids.
- **Floor Manager:** sees the same machine-planning calendar in read-only mode.

## Manual operation splitting (primary workflow)

1. Select a CEO production order from the sidebar. The CEO schedule remains visible in the manager's calendar and cannot be edited from this page.
2. Under **Split order into operations**, enter an operation name, the machine that performs it, and its estimated duration in hours. Add as many sequential operations as needed (e.g. Polar cutting → Heidelberg printing → Wenchun lamination → SBL die-cutting).
3. Each operation is saved separately as `ProductionOperation`, including `Name`, `Sequence` and `PlannedHours`. The chosen machine is persisted through a machine-specific `ProductionWorkCenter` entry using the existing `WorkCenterId` property; code prefix `MC-` avoids colliding with other work centers. There is no new database migration.
4. The sidebar always shows the **CEO duration**, **sum of the operation estimates**, and **remaining unallocated time**. Adding or editing a stage cannot exceed the CEO total; managers can fill the remaining time with further operations. The sum matches the CEO total when all time is allocated, without silently redistributing any explicitly entered stage duration.
5. Select an operation and click a calendar day/hour, or drag it onto a slot. Month view schedules from the first permitted time on the chosen date; Week/Day use the selected hour. The shared `ProductionPlanningTime` methods skip lunch and any shifts prohibited by the CEO. A stage is rejected if its start/end would lie outside the CEO's exact planned window, or if another operation occupies the same machine during productive time.
6. The resulting operation card shows the order, assigned machine and operation name on the **same CEO calendar**. Moving a planned card to another slot changes its booking; dragging it back to the left operation list or selecting **Return to list** cancels it and any downstream planned steps. Released/running/completed bookings cannot be moved or cancelled here.
7. Edit any unscheduled operation's name, machine or duration; delete unscheduled operations. Scheduled operations must be unscheduled before their details can be changed. The page never automatically generates operations on load: **splitting is explicitly controlled by the manager**.

## Equipment

Available machines come from the seeded `Machines` rows: Polar, Heidelberg, Nilpeter 1, Nilpeter 2, Wenchun, Tops 102, Sakurai, SBL, and Folder-gluer. The manager chooses an actual machine for each step. This does not yet validate an operation-to-machine capability matrix.

## Limits and verification

- Durations are *estimates*. The matching-total indicator is a planning budget, not a guarantee of machine throughput or actual production time.
- Hour-based calendar placement, CEO shift restrictions, and machine overlap are validated at save time. Multiple machine operations may share the order timeframe, but a single machine cannot have overlapping productive slots. Downstream stage completion order is a production review responsibility; strict predecessor constraints are not automatically enforced for all combinations.
- Machine-specific `WorkCenter` rows are added on demand. This is an interim reuse of existing schema, not a full machine capability database.
- Concurrent edits are not serialized through a machine-booking lock. Production use needs concurrency/load tests.
- **No .NET build, automated tests, or real-browser tests were possible in this environment**. Keep PR #4 in draft pending validation. Do not merge into main without explicit approval.

### Manual acceptance checks

1. Verify manager and CEO pages show identical 42-day Jalali Month grids, 24-hour Week/Day timelines, original order chips and shift colors; the manager page adds machine-operation chips.
2. CEO plans a 6-hour order; manager creates three stages (2.5 hours Heidelberg, 1 hour Wenchun, 2.5 hours SBL). The sum is 6 hours with 0 remaining. Try a fourth stage; it must reject exceeding the CEO total.
3. Select and drag the first stage into a valid calendar slot. It appears on the calendar with machine name. Repeat with a different machine.
4. Drag a planned stage to a new time; old booking disappears. Drag a planned stage into the sidebar; the assignment is removed.
5. Reject a booking extending beyond the CEO's precise end time and reject an overlap on the same machine.
6. Reload and confirm the machine selection, operation name, duration and calendar bookings persist. Test English/Persian, RTL, Floor Manager read-only.
