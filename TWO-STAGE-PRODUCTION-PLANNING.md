# CEO production calendar and multi-machine scheduling

## Workflow
1. **CEO:** `/production/planning` is the original Sales-first calendar (Month / Week / Day, Jalali dates, shift colors and drag-and-drop). The order's `EstimatedDurationMinutes`, `PlannedStartDate`, `PlannedEndDate`, `AvoidOvertime` and `AvoidNightShift` define the production window and permitted productive minutes. Lunch is always excluded.
2. **Production Manager:** `/production/machine-schedule` shows separate Month / Week / Day calendars for every active machine. **Generate Operations** builds an ordered multi-stage route using the Sheet/Roll job specifications; an order can move through multiple different machines.
3. **Duration agreement:** All operation `PlannedHours` are apportioned in whole productive minutes. Their **total equals** the CEO's `EstimatedDurationMinutes`. The machine page shows both totals and scheduled/total step counts per order. Managers can edit one unscheduled step's hours; the remaining steps rebalance to keep the CEO total unchanged. Adding a manual step likewise redistributes durations.
4. **Assign and move:** Select or drag an unscheduled step to a compatible machine/calendar slot. Steps may be moved to another slot/machine while still `Planned`; this **updates** its existing assignment instead of creating a second active assignment. Machine conflicts, the CEO start/end window, and required order sequence are checked.
5. **Return from calendar to list:** Drag a scheduled step back to the left-side unscheduled queue, or use the return button on a card. The step **and any downstream planned dependent steps** are cancelled as machine assignments and reappear in the list. Drag the order summary card to the queue (or click **Return all order steps**) to return its whole route. The CEO's order-level calendar remains unchanged. Released/running/completed schedules cannot be removed or moved this way.
6. **CEO changes:** Changing an order's total duration/shift options reallocates its unscheduled steps and recalculates its CEO end date. Moving or resizing an order with active machine bookings is blocked until the Production Manager returns those steps to the list; no silent invalidation occurs.

The shared `Services/ProductionPlanningTime.cs` performs shift-aware productive-time and duration calculations for both pages. Machine calendar day/hour occupancy and conflict checks exclude lunch and disallowed shifts.

## Equipment
Machines are seeded by their unique codes if missing: Polar, Heidelberg, Nilpeter 1, Nilpeter 2, Wenchun, Tops 102, Sakurai, SBL and Folder-gluer. Selected Sheet/Roll services map to suggested compatible machinery. Nilpeter 1 vs 2 remains a manager choice. Unrecognized services are flagged for review.

## Important limitations before production release
- Routing is derived from service names and machine name/code matching, **not** a normalized database-backed machine capability table. Many service-to-machine assignments require manual confirmation.
- Operation durations are **initial estimates**, initially weighted equally on generation; exact total matching does not imply physically accurate individual runtimes. Review and adjust stage durations.
- Machine schedules use the CEO's shift restrictions, but there is no per-machine maintenance/downtime calendar, staffing capacity model, or race-proof database transaction preventing two simultaneous writers from booking an overlapping slot.
- Historical scheduled steps that already have assignments with a mismatched total are not automatically modified. Return them to the queue, then use **Generate Operations** to synchronize.
- No .NET build, automated tests, or browser tests have been run in this environment. Keep PR in draft until validated.

## Manual verification scenarios
1. Start with an order requiring printing, lamination, and die-cutting, and set CEO productive duration to **180 minutes**. Generate operations: multiple machine steps appear and their duration total equals 180 minutes.
2. On the CEO calendar, schedule **120 productive minutes starting at 12:00**, with overtime and night work disabled. Expected end is **15:00**, because 13:00–14:00 lunch is skipped.
3. On the manager page schedule the first step on a compatible machine, then the next one on another compatible machine. Verify predecessor order, the exact CEO window, status, visible calendar cards, and no display during lunch.
4. Drag a scheduled step from its machine calendar into the left queue: it reappears unscheduled; any downstream planned steps return too. Drag an order summary into the queue to return all planned steps. Try the return button as a non-drag alternative.
5. Drag a planned step to a different valid machine/time: the original active machine booking disappears and exactly one active schedule remains for that step.
6. Try double-booking a machine or scheduling the next stage before its predecessor completes: save is rejected. Try scheduling past the exact CEO end, not just past the same date: rejected.
7. Edit one unscheduled step's hours and verify that the sum of all other steps is automatically redistributed to the CEO total. Verify that changing the CEO duration or stage duration while there are active machine bookings is blocked with an explanation.
8. Confirm CEO/manager/floor role permissions, EN/FA labels, RTL and responsive presentation, and the global primary color `#0c568d`.
