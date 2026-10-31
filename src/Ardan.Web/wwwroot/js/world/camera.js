import * as THREE from "https://cdn.jsdelivr.net/npm/three@0.170.0/build/three.module.min.js";

export const MIN_VIEW = 6;
export const MAX_VIEW = 72;
const MIN_ELEVATION = 0.32;
const MAX_ELEVATION = 1.36;
const CLICK_SLOP = 5;
const KEY_STEP = 70;

export class Rig {
    constructor(camera, site) {
        this.camera = camera;
        this.site = site;
        this.tween = null;
        Object.assign(this, this.homeFor(1));
    }

    homeFor(aspect) {
        const s = this.site;
        const azimuth = -0.66;
        const elevation = 0.62;
        const right = new THREE.Vector3(Math.cos(azimuth), 0, -Math.sin(azimuth));
        const up = new THREE.Vector3(-Math.sin(elevation) * Math.sin(azimuth), Math.cos(elevation), -Math.sin(elevation) * Math.cos(azimuth));
        const corners = [[s.left, 0, s.front], [s.rackEnd, 0, s.aisleFront], [s.rackEnd, s.top, -s.laneLength], [s.left, s.top, s.aisleZ]]
            .map(([x, y, z]) => new THREE.Vector3(x, y, z));
        const across = corners.map(point => point.dot(right));
        const along = corners.map(point => point.dot(up));
        const hud = aspect > 1 ? { left: 0.27, right: 0.09, top: 0.1, bottom: 0.05 } : { left: 0.03, right: 0.14, top: 0.2, bottom: 0.38 };
        const size = THREE.MathUtils.clamp(Math.max(
            (Math.max(...along) - Math.min(...along)) / (1 - hud.top - hud.bottom),
            (Math.max(...across) - Math.min(...across)) / ((1 - hud.left - hud.right) * aspect)), MIN_VIEW, MAX_VIEW);
        const x = (Math.max(...across) + Math.min(...across)) / 2 - size * aspect * (hud.left - hud.right) / 2;
        const y = (Math.max(...along) + Math.min(...along)) / 2 - size * (hud.bottom - hud.top) / 2;
        const target = right.multiplyScalar(x).addScaledVector(up, y);
        const view = new THREE.Vector3(Math.sin(azimuth) * Math.cos(elevation), Math.sin(elevation), Math.cos(azimuth) * Math.cos(elevation));
        target.addScaledVector(view, (2.6 - target.y) / view.y);
        return { target, azimuth, elevation, size };
    }

    apply(aspect) {
        const half = this.size / 2;
        const camera = this.camera;
        camera.left = -half * aspect;
        camera.right = half * aspect;
        camera.top = half;
        camera.bottom = -half;
        const flat = Math.cos(this.elevation) * 120;
        camera.position.set(
            this.target.x + Math.sin(this.azimuth) * flat,
            this.target.y + Math.sin(this.elevation) * 120,
            this.target.z + Math.cos(this.azimuth) * flat);
        camera.lookAt(this.target);
        camera.updateProjectionMatrix();
    }

    pan(dx, dy, pixels) {
        const unit = this.size / pixels;
        const right = new THREE.Vector3(Math.cos(this.azimuth), 0, -Math.sin(this.azimuth));
        const ahead = new THREE.Vector3(-Math.sin(this.azimuth), 0, -Math.cos(this.azimuth));
        this.target.addScaledVector(right, -dx * unit);
        this.target.addScaledVector(ahead, dy * unit / Math.max(0.3, Math.sin(this.elevation)));
        this.clamp();
    }

    orbit(dx, dy) {
        this.azimuth -= dx * 0.008;
        this.elevation = THREE.MathUtils.clamp(this.elevation + dy * 0.006, MIN_ELEVATION, MAX_ELEVATION);
    }

    zoomTo(size) {
        this.size = THREE.MathUtils.clamp(size, MIN_VIEW, MAX_VIEW);
    }

    clamp() {
        const s = this.site;
        this.target.x = THREE.MathUtils.clamp(this.target.x, s.left - 8, s.right + 8);
        this.target.z = THREE.MathUtils.clamp(this.target.z, s.back - 6, s.front + 12);
    }

    goal() {
        return this.tween?.to ?? { target: this.target.clone(), azimuth: this.azimuth, elevation: this.elevation, size: this.size };
    }

    fly(to, ms = 650) {
        const goal = { ...this.goal(), ...to };
        goal.size = THREE.MathUtils.clamp(goal.size, MIN_VIEW, MAX_VIEW);
        goal.elevation = THREE.MathUtils.clamp(goal.elevation, MIN_ELEVATION, MAX_ELEVATION);
        if (matchMedia("(prefers-reduced-motion: reduce)").matches) ms = 1;
        this.tween = {
            from: { target: this.target.clone(), azimuth: this.azimuth, elevation: this.elevation, size: this.size },
            to: goal,
            start: performance.now(),
            ms,
        };
    }

    stop() {
        this.tween = null;
    }

    step(time) {
        if (!this.tween) return;
        const { from, to, start, ms } = this.tween;
        const t = Math.min(1, (time - start) / ms);
        const k = t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
        this.target.lerpVectors(from.target, to.target, k);
        this.azimuth = from.azimuth + (to.azimuth - from.azimuth) * k;
        this.elevation = from.elevation + (to.elevation - from.elevation) * k;
        this.size = from.size + (to.size - from.size) * k;
        if (t === 1) this.tween = null;
    }
}

// Left drag turns the camera, right or middle drag (or Shift + drag) moves it, the wheel zooms towards the cursor.
// One finger turns; two fingers move, pinch and twist.
export class Controls {
    constructor(element, rig, handlers) {
        this.element = element;
        this.rig = rig;
        this.handlers = handlers;
        this.pointers = new Map();
        this.press = null;
        this.listeners = [
            [element, "pointerdown", e => this.down(e)],
            [element, "pointermove", e => this.move(e)],
            [element, "pointerup", e => this.up(e)],
            [element, "pointercancel", e => this.up(e)],
            [element, "pointerleave", () => this.handlers.leave()],
            [element, "wheel", e => this.wheel(e), { passive: false }],
            [element, "contextmenu", e => e.preventDefault()],
            [window, "keydown", e => this.key(e)],
        ];
        for (const [target, type, listener, options] of this.listeners) target.addEventListener(type, listener, options);
    }

    dispose() {
        for (const [target, type, listener, options] of this.listeners) target.removeEventListener(type, listener, options);
    }

    down(e) {
        if (e.button > 2) return;
        this.element.setPointerCapture?.(e.pointerId);
        this.pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
        if (this.pointers.size === 1) {
            this.press = { x: e.clientX, y: e.clientY, button: e.button, moved: false, move: e.button !== 0 || e.shiftKey };
        } else if (this.press) {
            this.press.moved = true;
        }
        this.rig.stop();
        this.handlers.leave();
    }

    move(e) {
        const point = this.pointers.get(e.pointerId);
        if (!point) {
            this.handlers.hover(e.clientX, e.clientY);
            return;
        }
        if (this.pointers.size === 1 && this.press) {
            if (!this.press.moved && Math.hypot(e.clientX - this.press.x, e.clientY - this.press.y) > CLICK_SLOP) this.press.moved = true;
            if (this.press.moved) {
                const dx = e.clientX - point.x;
                const dy = e.clientY - point.y;
                if (this.press.move) this.rig.pan(dx, dy, this.handlers.pixels());
                else this.rig.orbit(dx, dy);
                this.element.style.cursor = this.press.move ? "move" : "grabbing";
            }
        } else if (this.pointers.size === 2) {
            const [a, b] = [...this.pointers.values()];
            const span = Math.hypot(a.x - b.x, a.y - b.y);
            const angle = Math.atan2(b.y - a.y, b.x - a.x);
            const middle = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
            point.x = e.clientX;
            point.y = e.clientY;
            const after = Math.hypot(a.x - b.x, a.y - b.y);
            const twist = Math.atan2(b.y - a.y, b.x - a.x) - angle;
            if (span > 0 && after > 0) this.rig.zoomTo(this.rig.size * span / after);
            this.rig.azimuth -= Math.atan2(Math.sin(twist), Math.cos(twist));
            this.rig.pan((a.x + b.x) / 2 - middle.x, (a.y + b.y) / 2 - middle.y, this.handlers.pixels());
        }
        point.x = e.clientX;
        point.y = e.clientY;
    }

    up(e) {
        if (!this.pointers.delete(e.pointerId) || this.pointers.size > 0) return;
        if (e.type === "pointerup" && this.press && !this.press.moved && this.press.button === 0) this.handlers.pick(e.clientX, e.clientY);
        this.press = null;
        this.element.style.cursor = "";
    }

    wheel(e) {
        e.preventDefault();
        const rate = e.deltaMode === 1 ? 0.05 : e.ctrlKey ? 0.012 : 0.0016;
        this.rig.stop();
        this.handlers.zoom(e.clientX, e.clientY, Math.exp(e.deltaY * rate));
    }

    key(e) {
        if (e.defaultPrevented || e.metaKey || e.ctrlKey || e.altKey || typing(e.target) || document.querySelector(".modal")) return;
        if (!this.element.isConnected || this.element.offsetParent === null) return;
        const pixels = this.handlers.pixels();
        const actions = {
            q: () => this.handlers.command("rotate-left"),
            e: () => this.handlers.command("rotate-right"),
            "+": () => this.handlers.command("zoom-in"),
            "=": () => this.handlers.command("zoom-in"),
            "-": () => this.handlers.command("zoom-out"),
            "0": () => this.handlers.command("home"),
            f: () => this.handlers.command("fullscreen"),
            ArrowLeft: () => this.rig.pan(KEY_STEP, 0, pixels),
            ArrowRight: () => this.rig.pan(-KEY_STEP, 0, pixels),
            ArrowUp: () => this.rig.pan(0, KEY_STEP, pixels),
            ArrowDown: () => this.rig.pan(0, -KEY_STEP, pixels),
        };
        const action = actions[e.key.length === 1 ? e.key.toLowerCase() : e.key];
        if (!action) return;
        e.preventDefault();
        action();
    }
}

function typing(target) {
    return target instanceof HTMLElement
        && (target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName));
}
