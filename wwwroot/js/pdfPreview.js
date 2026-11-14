// PDF pages are rendered to PNG by HAMICHAP itself, using Docnet.Core.
// No externally hosted JavaScript, worker or PDF conversion API is required.
export async function renderPdf(container, url) {
    container.__proofPdfCleanup?.();
    container.innerHTML = "";

    const controller = new AbortController();
    let disposed = false;
    let resizeObserver;

    container.__proofPdfCleanup = () => {
        disposed = true;
        controller.abort();
        resizeObserver?.disconnect();
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
            if (disposed) return;
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

            const pixelRatio = Math.min(window.devicePixelRatio || 1, 2);

            for (const item of pages) {
                const wrapper = document.createElement("div");
                wrapper.className = "proof-pdf-page";

                const canvas = document.createElement("canvas");
                const context = canvas.getContext("2d");
                const cssWidth = item.width * best.scale;
                const cssHeight = item.height * best.scale;

                canvas.width = Math.ceil(cssWidth * pixelRatio);
                canvas.height = Math.ceil(cssHeight * pixelRatio);
                canvas.style.width = `${cssWidth}px`;
                canvas.style.height = `${cssHeight}px`;

                if (context && item.image) {
                    context.drawImage(item.image, 0, 0, canvas.width, canvas.height);
                }

                wrapper.appendChild(canvas);

                const pageLabel = document.createElement("div");
                pageLabel.className = "proof-pdf-page-number";
                pageLabel.textContent = `Page ${item.pageNumber} of ${count}`;
                wrapper.appendChild(pageLabel);
                container.appendChild(wrapper);
            }
        };

        renderAllPages();

        if (typeof ResizeObserver !== "undefined") {
            resizeObserver = new ResizeObserver(renderAllPages);
            resizeObserver.observe(container);
        }
    } catch (error) {
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

export function disposePdf(container) {
    container.__proofPdfCleanup?.();
    delete container.__proofPdfCleanup;
}
