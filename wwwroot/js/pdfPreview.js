// PDF pages are rendered to PNG by HAMICHAP itself, using Docnet.Core.
// No externally hosted JavaScript, worker or PDF conversion API is required.
export async function renderPdf(container, url) {
    container.__proofPdfCleanup?.();
    container.innerHTML = "";

    const controller = new AbortController();
    let disposed = false;
    let resizeObserver;

    let markReady;
    const ready = new Promise(resolve => { markReady = resolve; });
    const state = {
        printing: false,
        disposed: false,
        generation: 0,
        refresh: null,
        printPromise: null,
        ready,
        markReady
    };
    container.__proofPdfState = state;
    container.__proofPdfCleanup = () => {
        disposed = true;
        state.disposed = true;
        state.generation++;
        state.markReady();
        controller.abort();
        resizeObserver?.disconnect();
        delete container.__proofPdfState;
    };

    try {
        const source = new URL(url, window.location.href);
        if (source.origin !== window.location.origin ||
            !source.pathname.startsWith("/uploads/") ||
            !source.pathname.toLowerCase().endsWith(".pdf")) {
            throw new Error("PDF previews only support locally uploaded PDF files.");
        }

        const file = encodeURIComponent(source.pathname);
        const response = await fetch(`/api/pdf-preview/info?file=${file}`, {
            credentials: "same-origin",
            signal: controller.signal
        });

        if (!response.ok) {
            throw new Error(`Could not open local PDF (HTTP ${response.status}).`);
        }

        const metadata = await response.json();
        if (!Array.isArray(metadata.pages) || metadata.pages.length === 0) {
            throw new Error("The PDF has no previewable pages.");
        }

        const pages = metadata.pages.map((page, index) => ({
            width: page.width,
            height: page.height,
            pageNumber: index + 1,
            image: null
        }));

        // Limit simultaneous PDFium page renders so large documents do not
        // overload the local application server.
        let nextPage = 0;
        await Promise.all(Array.from(
            { length: Math.min(3, pages.length) },
            async () => {
                while (nextPage < pages.length && !disposed) {
                    const item = pages[nextPage++];
                    const image = new Image();
                    image.decoding = "async";
                    image.src = `/api/pdf-preview/page?file=${file}&page=${item.pageNumber}`;
                    await image.decode();
                    item.image = image;
                }
            }
        ));

        if (disposed) return;

        const renderAllPages = () => {
            if (disposed || state.printing) return;
            const width = container.clientWidth;
            const height = container.clientHeight;
            if (!width || !height) return;

            const count = pages.length;
            const gap = 7;
            const labelHeight = 14;
            const padding = 8;

            let best = { columns: 1, rows: count, scale: 0 };

            for (let columns = 1; columns <= count; columns++) {
                const rows = Math.ceil(count / columns);
                const cellWidth = (width - gap * (columns - 1)) / columns - padding;
                const cellHeight = (height - gap * (rows - 1)) / rows -
                    padding - labelHeight;

                if (cellWidth <= 0 || cellHeight <= 0) continue;

                const commonScale = Math.min(
                    ...pages.map(({ width: pageWidth, height: pageHeight }) =>
                        Math.min(cellWidth / pageWidth, cellHeight / pageHeight))
                );

                if (commonScale > best.scale) {
                    best = { columns, rows, scale: commonScale };
                }
            }

            if (best.scale <= 0) return;

            container.innerHTML = "";
            container.style.display = "grid";
            container.style.gridTemplateColumns =
                `repeat(${best.columns}, minmax(0, 1fr))`;
            container.style.gridTemplateRows =
                `repeat(${best.rows}, minmax(0, 1fr))`;
            container.style.gap = `${gap}px`;
            container.style.overflow = "hidden";

            // Render the PNG directly as an image. Redrawing onto a small
            // screen-resolution canvas permanently discarded pixels before print.
            for (const item of pages) {
                const wrapper = document.createElement("div");
                wrapper.className = "proof-pdf-page";

                const image = document.createElement("img");
                image.className = "proof-pdf-image";
                image.alt = `PDF artwork page ${item.pageNumber} of ${count}`;
                image.decoding = "async";
                image.src = item.image.src;
                image.dataset.previewSrc = item.image.src;
                image.dataset.printSrc = `${item.image.src}&quality=print`;
                image.style.width = `${item.width * best.scale}px`;
                image.style.height = `${item.height * best.scale}px`;
                wrapper.appendChild(image);

                const pageLabel = document.createElement("div");
                pageLabel.className = "proof-pdf-page-number";
                pageLabel.textContent = `Page ${item.pageNumber} of ${count}`;
                wrapper.appendChild(pageLabel);
                container.appendChild(wrapper);
            }
        };

        state.refresh = renderAllPages;
        renderAllPages();
        state.markReady();

        if (typeof ResizeObserver !== "undefined") {
            resizeObserver = new ResizeObserver(renderAllPages);
            resizeObserver.observe(container);
        }
    } catch (error) {
        state.markReady();
        if (disposed) return;
        console.error("PDF preview error:", error);
        container.innerHTML = "";
        const message = document.createElement("div");
        message.className = "text-danger small text-center py-3";
        message.append("Unable to display PDF preview. ");
        const link = document.createElement("a");
        link.textContent = "Open PDF";
        link.href = url;
        link.target = "_blank";
        link.rel = "noopener";
        message.appendChild(link);
        container.appendChild(message);
    }
}


// Wait for print-quality artwork to decode before opening the Print / Save as PDF
// dialog. All source PDFs are kept intact; this only changes proof preview images.
export async function preparePdfForPrint(root) {
    if (!root) return;
    const containers = [...root.querySelectorAll(".proof-pdf-pages")];
    await Promise.all(containers.map(async (container) => {
        const state = container.__proofPdfState;
        if (!state || state.disposed) return;
        await state.ready;
        if (state.disposed) return;
        if (state.printPromise) return state.printPromise;

        const generation = ++state.generation;
        state.refresh?.();
        state.printing = true;

        const run = (async () => {
            const images = [...container.querySelectorAll("img.proof-pdf-image")];
            // One high-resolution page at a time to bound memory and PDFium load.
            for (const image of images) {
                if (state.disposed || generation !== state.generation) return;
                const printSrc = image.dataset.printSrc;
                if (!printSrc) continue;
                const highResolution = new Image();
                highResolution.decoding = "async";
                highResolution.src = printSrc;
                try {
                    await highResolution.decode();
                    if (state.disposed || generation !== state.generation) return;
                    image.src = printSrc;
                    if (!image.complete) await image.decode();
                } catch (error) {
                    // Never silently substitute the low-resolution preview in the
                    // Print button path: let the UI explain that print art failed.
                    console.error("Print-quality PDF artwork unavailable:", error);
                    throw new Error("High-resolution PDF artwork could not be prepared.");
                }
            }
        })();

        state.printPromise = run;
        try {
            await run;
        } finally {
            if (state.printPromise === run) state.printPromise = null;
        }
    }));
}

export function restorePdfAfterPrint(root) {
    if (!root) return;
    for (const container of root.querySelectorAll(".proof-pdf-pages")) {
        const state = container.__proofPdfState;
        if (!state) continue;
        state.generation++;
        state.printing = false;
        for (const image of container.querySelectorAll("img.proof-pdf-image")) {
            if (image.dataset.previewSrc) image.src = image.dataset.previewSrc;
        }
        state.refresh?.();
    }
}

export function disposePdf(container) {
    container.__proofPdfCleanup?.();
    delete container.__proofPdfCleanup;
}
