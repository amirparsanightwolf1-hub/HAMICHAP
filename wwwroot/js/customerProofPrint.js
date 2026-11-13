// Ensure that a complete roll or sheet customer proof fits the printable
// area of a single A4 landscape page (297 x 210 mm, 5 mm margins).
// Works for the Print button as well as the browser's Ctrl+P/print menu.
let registered = false;
let prepared = false;

function prepareProof() {
    const page = document.querySelector(".proof-page");
    const sheet = page?.querySelector(".proof-sheet");
    if (!page || !sheet) return;

    const root = document.documentElement;
    root.classList.add("proof-print-layout");
    page.style.setProperty("--proof-print-scale", "1");

    // CSS and browser printing use the same 287 mm sheet width. The
    // initial, unscaled height includes every field and arbitrary notes.
    const unit = document.createElement("div");
    unit.style.cssText =
        "position:absolute;visibility:hidden;pointer-events:none;width:100mm;height:100mm;";
    document.body.appendChild(unit);
    const pxPerMm = unit.getBoundingClientRect().width / 100;
    unit.remove();

    const height = Math.max(sheet.scrollHeight, sheet.getBoundingClientRect().height);
    const width = Math.max(sheet.scrollWidth, sheet.getBoundingClientRect().width);

    // 2 mm safety allowance for different Chromium/Firefox rounding.
    const usableHeight = 198 * pxPerMm;
    const usableWidth = 285 * pxPerMm;
    const factor = Math.min(1,
        usableHeight / Math.max(height, 1),
        usableWidth / Math.max(width, 1));

    page.style.setProperty("--proof-print-scale",
        Math.max(0.05, factor).toFixed(5));
    prepared = true;
}

function finishProof() {
    if (!prepared) return;
    const page = document.querySelector(".proof-page");
    page?.style.removeProperty("--proof-print-scale");
    document.documentElement.classList.remove("proof-print-layout");
    prepared = false;
}

export function registerProofPrint() {
    if (registered) return;
    window.addEventListener("beforeprint", prepareProof);
    window.addEventListener("afterprint", finishProof);
    registered = true;
}

export function printProof() {
    // Prepare synchronously before opening the dialog; the beforeprint
    // handler also covers browser-initiated printing.
    prepareProof();
    window.print();
}

export function unregisterProofPrint() {
    if (registered) {
        window.removeEventListener("beforeprint", prepareProof);
        window.removeEventListener("afterprint", finishProof);
        registered = false;
    }
    finishProof();
}
