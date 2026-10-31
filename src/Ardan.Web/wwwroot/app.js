window.ardan = (() => {
    const storageKey = "ardan-theme";
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

    // Every tooltip is drawn in one layer above the page, so panels, scroll areas and the 3D view never hide it.
    const tip = (() => {
        const hosts = ".tip, .lane, .taxi, .carriage, .parcel, .belt, .gate, [data-tip]";
        const margin = 8;
        const gap = 10;
        let layer = null;
        let host = null;
        let timer = 0;
        let watcher = null;
        let following = false;

        function element() {
            if (!layer) {
                layer = document.createElement("div");
                layer.className = "tip-layer";
                layer.setAttribute("aria-hidden", "true");
                document.body.appendChild(layer);
            }
            return layer;
        }

        function text(tag, className, value) {
            const node = document.createElement(tag);
            node.className = className;
            node.textContent = value;
            return node;
        }

        function fill(content) {
            if (content instanceof Node) {
                element().replaceChildren(content);
                return;
            }
            const parts = [];
            if (content.title) parts.push(text("strong", "tip-title", content.title));
            for (const line of content.lines ?? []) parts.push(text("span", "tip-text", line));
            if (content.meter) {
                const bar = document.createElement("span");
                bar.className = "tip-meter";
                const value = document.createElement("i");
                value.style.width = `${Math.round(Math.min(1, content.meter.value / Math.max(1, content.meter.max)) * 100)}%`;
                bar.appendChild(value);
                parts.push(bar);
            }
            element().replaceChildren(...parts);
        }

        function move(x, y) {
            const el = element();
            const left = Math.min(Math.max(x, margin), innerWidth - el.offsetWidth - margin);
            const top = Math.min(Math.max(y, margin), innerHeight - el.offsetHeight - margin);
            el.style.transform = `translate(${Math.round(left)}px, ${Math.round(top)}px)`;
        }

        function place(anchor, side) {
            const el = element();
            const w = el.offsetWidth;
            const h = el.offsetHeight;
            const spots = {
                top: [anchor.left + anchor.width / 2 - w / 2, anchor.top - h - gap],
                bottom: [anchor.left + anchor.width / 2 - w / 2, anchor.bottom + gap],
                left: [anchor.left - w - gap, anchor.top + anchor.height / 2 - h / 2],
                right: [anchor.right + gap, anchor.top + anchor.height / 2 - h / 2],
            };
            const order = { top: ["top", "bottom"], bottom: ["bottom", "top"], left: ["left", "right", "top"] }[side] ?? ["top", "bottom"];
            const fits = ([x, y]) => x >= margin && y >= margin && x + w <= innerWidth - margin && y + h <= innerHeight - margin;
            const [x, y] = spots[order.find(name => fits(spots[name])) ?? order[0]];
            move(x, y);
        }

        function reveal(content, anchor, side) {
            fill(content);
            element().classList.add("is-shown");
            place(anchor, side);
        }

        function hide() {
            clearTimeout(timer);
            watcher?.disconnect();
            watcher = null;
            host = null;
            following = false;
            layer?.classList.remove("is-shown");
        }

        function unfollow() {
            if (following) hide();
        }

        function follow(content, x, y) {
            hide();
            following = true;
            fill(content);
            element().classList.add("is-shown");
            const el = element();
            const below = y + 20 + el.offsetHeight <= innerHeight - margin;
            move(x + 16, below ? y + 20 : y - el.offsetHeight - 12);
        }

        function source(target) {
            if (target.matches("[data-tip]")) {
                return { content: { title: target.dataset.tip, lines: target.dataset.tipText ? [target.dataset.tipText] : [] }, side: "top" };
            }
            const bubble = target.querySelector(":scope > .tip-bubble, :scope > .marker-tip");
            if (!bubble || !bubble.textContent.trim()) return null;
            const below = bubble.classList.contains("tip-bottom") || bubble.classList.contains("tip-below") || target.classList.contains("tip-below");
            return { bubble, side: bubble.classList.contains("tip-left") ? "left" : below ? "bottom" : "top" };
        }

        function open(target, delay) {
            if (target === host) return;
            hide();
            host = target;
            timer = setTimeout(() => {
                if (host !== target || !target.isConnected) return;
                const found = source(target);
                if (!found) return;
                if (!found.bubble) {
                    reveal(found.content, target.getBoundingClientRect(), found.side);
                    return;
                }
                const render = () => {
                    const copy = document.createDocumentFragment();
                    for (const child of found.bubble.childNodes) copy.appendChild(child.cloneNode(true));
                    reveal(copy, target.getBoundingClientRect(), found.side);
                };
                render();
                watcher = new MutationObserver(render);
                watcher.observe(found.bubble, { childList: true, subtree: true, characterData: true });
            }, delay);
        }

        const leaving = event => host && !(event.relatedTarget instanceof Node && host.contains(event.relatedTarget));

        document.addEventListener("pointerover", event => {
            const target = event.target instanceof Element ? event.target.closest(hosts) : null;
            if (target) open(target, target.matches(".tip, [data-tip]") ? 260 : 80);
            else if (host && !host.contains(event.target)) hide();
        });
        document.addEventListener("pointerout", event => {
            if (leaving(event)) hide();
        });
        document.addEventListener("focusin", event => {
            const target = event.target instanceof Element ? event.target.closest(hosts) : null;
            if (target) open(target, 0);
        });
        document.addEventListener("focusout", event => {
            if (leaving(event)) hide();
        });
        document.addEventListener("pointerdown", hide, true);
        document.addEventListener("keydown", event => {
            if (event.key === "Escape") hide();
        });
        window.addEventListener("scroll", hide, true);
        window.addEventListener("blur", hide);

        return { follow, unfollow };
    })();

    document.documentElement.dataset.theme = theme();

    return { theme, setTheme, listen, unlisten, tip };
})();
