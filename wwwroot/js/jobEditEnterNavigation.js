// Enter advances through enabled job-edit fields without submitting or saving.
// Delegate at document level so enhanced navigation and conditional fields need no reinitialization.
(() => {
    const pageSelector = ".job-edit-page.job-edit-roll, .job-edit-page.job-edit-sheet";
    const fieldSelector = "input, select, textarea, [data-job-enter-target]";
    const excludedInputTypes = new Set(["hidden", "button", "submit", "reset", "image"]);

    function eligible(element) {
        if (!(element instanceof HTMLElement) || element.matches(":disabled") ||
            element.getAttribute("aria-disabled") === "true" ||
            element.closest("[hidden], [inert], [aria-hidden='true']") ||
            element.tabIndex < 0 || element.getClientRects().length === 0 ||
            getComputedStyle(element).visibility !== "visible") return false;
        if (element instanceof HTMLInputElement &&
            (excludedInputTypes.has(element.type) || element.readOnly)) return false;
        if (element instanceof HTMLTextAreaElement && element.readOnly) return false;
        return true;
    }

    document.addEventListener("keydown", event => {
        if (event.key !== "Enter" || event.defaultPrevented || event.isComposing ||
            event.repeat || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey)
            return;
        const current = event.target;
        if (!(current instanceof HTMLInputElement || current instanceof HTMLSelectElement)) return;
        const page = current.closest(pageSelector);
        if (!page || !eligible(current)) return;
        // Preserve any separate dialog's keyboard handling.
        if (current.closest("[role='dialog'], dialog, .modal")) return;

        event.preventDefault();
        event.stopPropagation();
        const fields = Array.from(page.querySelectorAll(fieldSelector));
        const index = fields.indexOf(current);
        if (index < 0) return;
        // Compute after the current event; focus commits the current field's change
        // event naturally, including Blazor's InputNumber/InputText bindings.
        const next = fields.slice(index + 1).find(eligible);
        if (next) next.focus();
        else {
            const save = page.querySelector(".job-edit-save-button");
            if (save && eligible(save)) save.focus();
        }
    }, true);
})();
