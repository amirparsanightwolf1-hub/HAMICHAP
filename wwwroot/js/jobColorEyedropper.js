// Run directly in the browser click handler: EyeDropper.open requires user activation.
document.addEventListener("click", async (event) => {
    const button = event.target instanceof Element
        ? event.target.closest("[data-job-eyedropper]") : null;
    if (!button || button.disabled) return;
    const input = document.getElementById(button.dataset.jobEyedropper);
    if (!input) return;
    const publish = value => {
        if (!input.isConnected) return;
        input.value = value;
        input.dispatchEvent(new Event("change", { bubbles: true }));
    };
    if (!window.isSecureContext || typeof window.EyeDropper !== "function") {
        publish("unsupported");
        return;
    }
    button.disabled = true;
    const modal = button.closest(".modal");
    const previousVisibility = modal?.style.visibility;
    // Hide the colour dialog while sampling so the underlying artwork is visible.
    if (modal) modal.style.visibility = "hidden";
    try {
        const result = await new window.EyeDropper().open();
        publish(result.sRGBHex);
    } catch (error) {
        if (error?.name !== "AbortError") publish("failed");
    } finally {
        if (modal) modal.style.visibility = previousVisibility ?? "";
        button.disabled = false;
    }
});
