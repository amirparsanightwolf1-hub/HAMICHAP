
import * as pdfjsLib from
    "https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.10.38/pdf.min.mjs";

pdfjsLib.GlobalWorkerOptions.workerSrc =
    "https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.10.38/pdf.worker.min.mjs";

export async function renderPdf(container, url) {
    container.__proofPdfCleanup?.();
    container.innerHTML = "";

    try {
        const pdf = await pdfjsLib.getDocument(url).promise;
        const pages = [];

        for (let pageNumber = 1; pageNumber <= pdf.numPages; pageNumber++) {
            const page = await pdf.getPage(pageNumber);

            pages.push({
                page,
                pageNumber,
                viewport: page.getViewport({ scale: 1 })
            });
        }

        let renderVersion = 0;
        let resizeObserver;

        const renderAllPages = async () => {
            const version = ++renderVersion;

            const width = container.clientWidth;
            const height = container.clientHeight;

            if (!width || !height) return;

            const count = pages.length;
            const gap = 7;
            const labelHeight = 14;
            const padding = 8;

            let best = {
                columns: 1,
                rows: count,
                scale: 0
            };

            // Find the grid arrangement that gives all pages
            // the largest common scale.
            for (let columns = 1; columns <= count; columns++) {
                const rows = Math.ceil(count / columns);

                const cellWidth =
                    (width - gap * (columns - 1)) / columns - padding;

                const cellHeight =
                    (height - gap * (rows - 1)) / rows -
                    padding -
                    labelHeight;

                if (cellWidth <= 0 || cellHeight <= 0) continue;

                const commonScale = Math.min(
                    ...pages.map(({ viewport }) =>
                        Math.min(
                            cellWidth / viewport.width,
                            cellHeight / viewport.height
                        )
                    )
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

            const pixelRatio = Math.min(window.devicePixelRatio || 1, 2);

            for (const item of pages) {
                if (version !== renderVersion) return;

                const wrapper = document.createElement("div");
                wrapper.className = "proof-pdf-page";

                const canvas = document.createElement("canvas");
                const context = canvas.getContext("2d");

                const cssWidth = item.viewport.width * best.scale;
                const cssHeight = item.viewport.height * best.scale;

                const renderViewport = item.page.getViewport({
                    scale: best.scale * pixelRatio
                });

                canvas.width = Math.ceil(renderViewport.width);
                canvas.height = Math.ceil(renderViewport.height);

                canvas.style.width = `${cssWidth}px`;
                canvas.style.height = `${cssHeight}px`;

                wrapper.appendChild(canvas);

                const pageLabel = document.createElement("div");
                pageLabel.className = "proof-pdf-page-number";
                pageLabel.textContent =
                    `Page ${item.pageNumber} of ${pdf.numPages}`;

                wrapper.appendChild(pageLabel);
                container.appendChild(wrapper);

                await item.page.render({
                    canvasContext: context,
                    viewport: renderViewport
                }).promise;
            }
        };

        await renderAllPages();

        if (typeof ResizeObserver !== "undefined") {
            resizeObserver = new ResizeObserver(() => {
                renderAllPages();
            });

            resizeObserver.observe(container);
        }

        container.__proofPdfCleanup = () => {
            renderVersion++;
            resizeObserver?.disconnect();
        };
    }
    catch (error) {
        console.error("PDF preview error:", error);

        container.innerHTML =
            '<div class="text-danger small text-center py-3">' +
            'Unable to display PDF preview. ' +
            '<a href="' + encodeURI(url) +
            '" target="_blank" rel="noopener">Open PDF</a></div>';
    }
}

export function disposePdf(container) {
    container.__proofPdfCleanup?.();
    delete container.__proofPdfCleanup;
}
