window.ardan = (() => {
    const storageKey = "ardan-theme";
    const tipHosts = ".tip, .lane, .taxi, .carriage, .parcel, .belt, .gate";
    let keyListener = null;

    function stored() {
        try {
            return localStorage.getItem(storageKey);
        } catch {
            return null;
        }
    }

    function preferred() {
        return matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
    }

    function theme() {
        return document.documentElement.dataset.theme || stored() || preferred();
    }

    function setTheme(value) {
        document.documentElement.dataset.theme = value;
        try {
            localStorage.setItem(storageKey, value);
        } catch {
        }
        return value;
    }

    function isTyping(target) {
        return target instanceof HTMLElement
            && (target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName));
    }

    function listen(dotnet) {
        unlisten();
        keyListener = event => {
            if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
                event.preventDefault();
                dotnet.invokeMethodAsync("OpenSearch");
            } else if (event.key === "Escape") {
                dotnet.invokeMethodAsync("CloseOverlays");
            } else if (event.key === "?" && !isTyping(event.target)) {
                event.preventDefault();
                dotnet.invokeMethodAsync("ToggleHelp");
            }
        };
        document.addEventListener("keydown", keyListener);
    }

    function unlisten() {
        if (keyListener) document.removeEventListener("keydown", keyListener);
        keyListener = null;
    }

    function visibleArea(element) {
        let left = 8;
        let right = window.innerWidth - 8;
        for (let node = element.parentElement; node && node !== document.body; node = node.parentElement) {
            if (getComputedStyle(node).overflowX === "visible") continue;
            const box = node.getBoundingClientRect();
            left = Math.max(left, box.left + 4);
            right = Math.min(right, box.right - 4);
        }
        return { left, right };
    }

    function keepInView(bubble) {
        bubble.style.removeProperty("--shift");
        const box = bubble.getBoundingClientRect();
        const area = visibleArea(bubble);
        let shift = 0;
        if (box.right > area.right) shift = area.right - box.right;
        if (box.left + shift < area.left) shift = area.left - box.left;
        if (shift !== 0) bubble.style.setProperty("--shift", `${Math.round(shift)}px`);
    }

    document.addEventListener("pointerover", event => {
        const host = event.target instanceof Element ? event.target.closest(tipHosts) : null;
        const bubble = host?.querySelector(":scope > .tip-bubble, :scope > .marker-tip");
        if (bubble) keepInView(bubble);
    });

    document.addEventListener("focusin", event => {
        const host = event.target instanceof Element ? event.target.closest(tipHosts) : null;
        const bubble = host?.querySelector(":scope > .tip-bubble, :scope > .marker-tip");
        if (bubble) keepInView(bubble);
    });

    document.documentElement.dataset.theme = theme();

    return { theme, setTheme, listen, unlisten };
})();
