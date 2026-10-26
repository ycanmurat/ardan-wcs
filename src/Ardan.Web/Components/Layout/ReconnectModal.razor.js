const modal = document.getElementById("components-reconnect-modal");
modal.addEventListener("components-reconnect-state-changed", onStateChanged);
document.getElementById("components-reconnect-button").addEventListener("click", retry);
document.getElementById("components-resume-button").addEventListener("click", resume);

function onStateChanged(event) {
    const state = event.detail.state;
    if (state === "show") {
        modal.showModal();
    } else if (state === "hide") {
        modal.close();
    } else if (state === "failed") {
        document.addEventListener("visibilitychange", retryWhenVisible);
    } else if (state === "rejected") {
        location.reload();
    }
}

async function retry() {
    document.removeEventListener("visibilitychange", retryWhenVisible);
    try {
        const reconnected = await Blazor.reconnect();
        if (reconnected) return;
        const resumed = await Blazor.resumeCircuit();
        if (resumed) {
            modal.close();
        } else {
            location.reload();
        }
    } catch {
        document.addEventListener("visibilitychange", retryWhenVisible);
    }
}

async function resume() {
    try {
        const resumed = await Blazor.resumeCircuit();
        if (!resumed) location.reload();
    } catch {
        modal.classList.replace("components-reconnect-paused", "components-reconnect-resume-failed");
    }
}

async function retryWhenVisible() {
    if (document.visibilityState === "visible") await retry();
}
