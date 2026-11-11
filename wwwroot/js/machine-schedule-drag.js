/*
 * Native HTML5 drag data is required by Firefox (and improves compatibility
 * elsewhere). Blazor keeps operation/order IDs and persists changes on drop;
 * this script only enables browser drag/drop, never updates the database.
 */
(function () {
    "use strict";
    document.addEventListener("dragstart", function (event) {
        if (!(event.target instanceof Element)) return;
        const source = event.target.closest(".machine-schedule [draggable='true']");
        if (!source || !event.dataTransfer) return;
        event.dataTransfer.effectAllowed = "move";
        event.dataTransfer.setData("text/plain", "hamichap-machine-schedule");
    });
    document.addEventListener("dragover", function (event) {
        if (!(event.target instanceof Element)) return;
        if (!event.target.closest(".machine-schedule .machine-day-cell, .machine-schedule .planning-order-panel"))
            return;
        event.preventDefault();
        if (event.dataTransfer) event.dataTransfer.dropEffect = "move";
    });
}());
