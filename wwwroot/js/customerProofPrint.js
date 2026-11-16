// Ensure that a complete roll or sheet customer proof fits the printable
// area of a single A4 landscape page (297 x 210 mm, 5 mm margins).
// Works for the Print button as well as the browser's Ctrl+P/print menu.
let registered = false;
let prepared = false;
let pdfModulePromise;

function pdfModule() {
    pdfModulePromise ??= import("./pdfPreview.js");
    return pdfModulePromise;
}

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
        String(Number.isFinite(factor) && factor > 0 ? factor : 1));
    prepared = true;
}

function finishProof() {
    if (!prepared) return;
    const page = document.querySelector(".proof-page");
    // Return to fast screen previews after the browser closes the print dialog.
    pdfModule().then(module => module.restorePdfAfterPrint(page))
        .catch(error => console.warn("Could not restore PDF previews:", error));
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

export async function printProof() {
    // The button path waits for decoded 3600-pixel source PNGs before
    // opening the dialog. Source PDF files are never rewritten.
    prepareProof();
    try {
        const module = await pdfModule();
        await module.preparePdfForPrint(document.querySelector(".proof-page"));
    } catch (error) {
        console.warn("Printing with available PDF previews:", error);
    }
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
