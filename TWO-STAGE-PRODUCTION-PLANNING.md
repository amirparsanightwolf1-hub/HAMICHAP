# CEO Planning and Date-First Multi-Machine Calendar

## Intended workflow

1. **CEO – `/production/planning`:** Uses the existing Sales-first Jalali/English production calendar. The CEO sets each order's planned start and end dates (plus estimated total duration and optional shift preferences). These dates are the only *hard* placement boundaries for machine bookings.
2. **Production Manager – `/production/machine-schedule`:** A machine-by-machine **date calendar** (Month / Week / Day). It always lists CEO production orders in its sidebar, whether or not routing has been generated. When the manager opens the page, missing machine operations are generated from the Sheet and Roll job specifications where possible. A Generate Operations button remains for retries.
3. The manager can **drag a CEO order** onto a compatible machine's date, which assigns the next unscheduled operation for that machine. Or choose/drag one **specific operation** from the unscheduled queue. **Click Place here** or choose a date and machine in the queue as a keyboard/touch fallback.
4. **Machine booking validation:** The selected **date** must lie within the CEO-approved inclusive window: `CEOStart.Date <= MachineDate <= CEOEnd.Date`. If either CEO boundary is absent, the operation cannot be booked. Date placement is intentionally independent of precise work hours, minute budgets, predecessor time intervals and same-day machine capacity; several operations may share a machine/day. These hour-level capacity constraints are **not** currently enforced.
5. **Existing bookings:** Drag a scheduled operation to a different machine/day to move its booking. Drag the scheduled operation back into the left unscheduled queue (or click Return to list) to cancel it and any dependent downstream **planned** bookings. Drag the order summary back to return the entire order. Released, running or completed bookings cannot be removed/moved this way. Cancellation does not change the CEO's original order-level dates.
6. **Visual feedback:** All CEO orders are in the sidebar (including those with no generated route); active bookings appear as labeled cards in each machine's date cells; selecting an operation highlights CEO-permitted dates. If no active machines exist, an empty calendar and warning remain visible.
7. **Routing:** Machine candidates are inferred from SheetJobEdit/RollJobEdit specifications, selected services, and stored CMYK/Pantone JSON. Route stages include Polar, Heidelberg, Nilpeter 1/2, Wenchun, Tops 102, Sakurai, SBL, and Folder-gluer. Some finishing names require manual review. Routes with missing/ambiguous specifications may still need manually added operations.
8. **Duration information:** CEO `EstimatedDurationMinutes` and individual step `PlannedHours` are still stored and can be proportionally reallocated so total estimates match; they are **informational** in this date-first calendar and do not block day assignment. The CEO's original planning-time/shift calculations remain unchanged.

## Persistence
The existing `ProductionSchedule` DateTime columns are retained. New date-level bookings use `PlannedStart = selectedDate at midnight` and `PlannedEnd = selectedDate + 1 day at midnight` as an all-day marker. These **must not be interpreted as actual work start/end times** in execution reporting. This avoids database migrations but should be replaced by an explicit `PlannedDate` field in a future data-model update if the ERP needs both daily planning and exact execution times.

Native HTML5 drag/drop initializes a small browser DataTransfer payload (`wwwroot/js/machine-schedule-drag.js`) to support Firefox/Chromium. The Blazor page itself handles the permitted operation and database write.

## Manual testing required before merging

1. CEO books an order from **November 12 to November 16**. Production Manager visits machine calendar and sees the CEO order in the left list and navigates to Nov 12.
2. Verify a Sheet job with saved front/back CMYK/Pantone and a finishing step generates more than one operation. A Roll job with flexo printing and finishing creates operations as well.
3. Drag the whole CEO order onto the Heidelberg date cell on Nov 13. Expect a printing step card to appear on that machine/date; drag it to Nov 14 and verify the old card disappears. Add another step to the Wenchun calendar within the CEO window.
4. Drag a card back to the left queue; it should disappear from the calendar and reappear in the list. Use the return button as a non-drag alternative.
5. Try dragging a step onto **November 11 or November 17**. Expect a rejected drop and no saved booking. Verify same-date assignment works regardless of work hour.
6. Check month/week/day navigation, empty machine fallback, CEO-only/Floor Manager read-only access, English/Persian/RTL and touch/button fallback.
7. Check that the program uses primary blue `#0c568d`.

**No .NET build, automated tests or browser drag/drop tests have been run in this tool environment.** Keep the PR a draft until manually verified. Do not merge into `main` without explicit approval.
