import * as THREE from "https://cdn.jsdelivr.net/npm/three@0.170.0/build/three.module.min.js";

const MIN_VIEW = 6;
const CLICK_SLOP = 5;
const KEY_STEP = 70;
const FOLLOW = 14;
const TARGET_HEIGHT = 2.6;

// Fixed camera angles. The building is open to the front and the left, so none of them looks through a wall.
export const VIEWS = {
    overview: { azimuth: -0.66, elevation: 0.62 },
    rack: { azimuth: -0.12, elevation: 0.42 },
    lifts: { azimuth: -1.12, elevation: 0.6 },
};

export class Rig {
    constructor(camera, site) {
        this.camera = camera;
        this.site = site;
        this.probe = camera.clone();
        this.target = new THREE.Vector3(0, TARGET_HEIGHT, 0);
        this.azimuth = VIEWS.overview.azimuth;
        this.elevation = VIEWS.overview.elevation;
        this.size = 40;
        this.maxSize = 72;
        this.wanted = null;
        this.tween = null;
        this.moved = false;
    }

    // The camera that shows every point in the free part of the screen: inside the HUD panels, given in pixels.
    fit(view, points, width, height, insets) {
        const { azimuth, elevation } = VIEWS[view];
        const right = new THREE.Vector3(Math.cos(azimuth), 0, -Math.sin(azimuth));
        const up = new THREE.Vector3(-Math.sin(elevation) * Math.sin(azimuth), Math.cos(elevation), -Math.sin(elevation) * Math.cos(azimuth));
        const across = points.map(point => point.dot(right));
        const along = points.map(point => point.dot(up));
        const free = {
            w: Math.max(width * 0.3, width - insets.left - insets.right),
            h: Math.max(height * 0.3, height - insets.top - insets.bottom),
        };
        const spanX = Math.max(...across) - Math.min(...across);
        const spanY = Math.max(...along) - Math.min(...along);
        const size = THREE.MathUtils.clamp(Math.max(spanY * height / free.h, spanX * height / free.w) * 1.06, MIN_VIEW, 120);
        const unit = size / height;
        const x = (Math.max(...across) + Math.min(...across)) / 2 - (insets.left - insets.right) / 2 * unit;
        const y = (Math.max(...along) + Math.min(...along)) / 2 + (insets.top - insets.bottom) / 2 * unit;
        const target = right.multiplyScalar(x).addScaledVector(up, y);
        const view3 = this.direction(azimuth, elevation);
        target.addScaledVector(view3, (TARGET_HEIGHT - target.y) / view3.y);
        return { target, azimuth, elevation, size };
    }

    direction(azimuth, elevation) {
        return new THREE.Vector3(Math.sin(azimuth) * Math.cos(elevation), Math.sin(elevation), Math.cos(azimuth) * Math.cos(elevation));
    }

    place(camera, aspect, state) {
        const half = state.size / 2;
        camera.left = -half * aspect;
        camera.right = half * aspect;
        camera.top = half;
        camera.bottom = -half;
        camera.position.copy(state.target).addScaledVector(this.direction(state.azimuth, state.elevation), 120);
        camera.lookAt(state.target);
        camera.updateProjectionMatrix();
        camera.updateMatrixWorld();
    }

    apply(aspect) {
        this.place(this.camera, aspect, this);
    }

    // The camera a drag or a wheel turn is heading for, so pointer maths uses where the view is going, not where it is.
    aimed(aspect) {
        this.place(this.probe, aspect, { ...this, ...this.want() });
        return this.probe;
    }

    want() {
        if (!this.wanted) {
            this.tween = null;
            this.wanted = { target: this.target.clone(), size: this.size };
        }
        this.moved = true;
        return this.wanted;
    }

    goal() {
        if (this.tween) return this.tween.to;
        return { target: (this.wanted?.target ?? this.target).clone(), azimuth: this.azimuth, elevation: this.elevation, size: this.wanted?.size ?? this.size };
    }

    pan(dx, dy, pixels) {
        const want = this.want();
        const unit = want.size / pixels;
        const right = new THREE.Vector3(Math.cos(this.azimuth), 0, -Math.sin(this.azimuth));
        const ahead = new THREE.Vector3(-Math.sin(this.azimuth), 0, -Math.cos(this.azimuth));
        want.target.addScaledVector(right, -dx * unit);
        want.target.addScaledVector(ahead, dy * unit / Math.sin(this.elevation));
        this.clamp(want.target);
    }

    zoomTo(size) {
        this.want().size = this.limit(size);
    }

    limit(size) {
        return THREE.MathUtils.clamp(size, MIN_VIEW, this.maxSize);
    }

    clamp(target) {
        const s = this.site;
        target.x = THREE.MathUtils.clamp(target.x, s.left - 2, s.right + 2);
        target.z = THREE.MathUtils.clamp(target.z, s.back - 2, s.front + 8);
    }

    fly(to, ms = 650) {
        const goal = { ...this.goal(), ...to };
        goal.target = goal.target.clone();
        goal.size = this.limit(goal.size);
        if (matchMedia("(prefers-reduced-motion: reduce)").matches) ms = 1;
        this.wanted = null;
        this.tween = {
            from: { target: this.target.clone(), azimuth: this.azimuth, elevation: this.elevation, size: this.size },
            to: goal,
            start: performance.now(),
            ms,
        };
    }

    hold() {
        if (!this.tween) return;
        this.tween = null;
        this.wanted = { target: this.target.clone(), size: this.size };
    }

    step(time, seconds) {
        if (this.tween) {
            const { from, to, start, ms } = this.tween;
            const t = Math.min(1, (time - start) / ms);
            const k = t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
            this.target.lerpVectors(from.target, to.target, k);
            this.azimuth = from.azimuth + (to.azimuth - from.azimuth) * k;
            this.elevation = from.elevation + (to.elevation - from.elevation) * k;
            this.size = from.size + (to.size - from.size) * k;
            if (t === 1) this.tween = null;
            return true;
        }
        if (!this.wanted) return false;
        const k = 1 - Math.exp(-FOLLOW * seconds);
        this.target.lerp(this.wanted.target, k);
        this.size += (this.wanted.size - this.size) * k;
        if (this.target.distanceToSquared(this.wanted.target) < 1e-6 && Math.abs(this.wanted.size - this.size) < 1e-3) {
            this.target.copy(this.wanted.target);
            this.size = this.wanted.size;
            this.wanted = null;
        }
        return true;
    }
}

// Drag moves the camera and the wheel (or a pinch) zooms towards the pointer. The angle only changes with the view buttons,
// and a locked camera ignores the mouse altogether.
export class Controls {
    constructor(element, rig, handlers) {
        this.element = element;
        this.rig = rig;
        this.handlers = handlers;
        this.pointers = new Map();
        this.press = null;
        this.locked = false;
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
        if (this.pointers.size === 1) this.press = { x: e.clientX, y: e.clientY, button: e.button, moved: false };
        else if (this.press) this.press.moved = true;
        if (!this.locked) this.rig.hold();
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
            if (this.press.moved && !this.locked) {
                this.rig.pan(e.clientX - point.x, e.clientY - point.y, this.handlers.pixels());
                this.element.style.cursor = "grabbing";
            }
        } else if (this.pointers.size === 2 && !this.locked) {
            const [a, b] = [...this.pointers.values()];
            const span = Math.hypot(a.x - b.x, a.y - b.y);
            const middle = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
            point.x = e.clientX;
            point.y = e.clientY;
            const after = Math.hypot(a.x - b.x, a.y - b.y);
            if (span > 0 && after > 0) this.rig.zoomTo(this.rig.goal().size * span / after);
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
        if (this.locked) return;
        const rate = e.deltaMode === 1 ? 0.05 : e.ctrlKey ? 0.012 : 0.0016;
        this.handlers.zoom(e.clientX, e.clientY, Math.exp(THREE.MathUtils.clamp(e.deltaY, -120, 120) * rate));
    }

    key(e) {
        if (e.defaultPrevented || e.metaKey || e.ctrlKey || e.altKey || typing(e.target) || document.querySelector(".modal")) return;
        if (!this.element.isConnected || this.element.offsetParent === null) return;
        const pixels = this.handlers.pixels();
        const pan = (dx, dy) => () => {
            if (!this.locked) this.rig.pan(dx, dy, pixels);
        };
        const actions = {
            "1": () => this.handlers.command("overview"),
            "2": () => this.handlers.command("rack"),
            "3": () => this.handlers.command("lifts"),
            "0": () => this.handlers.command("overview"),
            "+": () => this.handlers.command("zoom-in"),
            "=": () => this.handlers.command("zoom-in"),
            "-": () => this.handlers.command("zoom-out"),
            f: () => this.handlers.command("fullscreen"),
            ArrowLeft: pan(KEY_STEP, 0),
            ArrowRight: pan(-KEY_STEP, 0),
            ArrowUp: pan(0, KEY_STEP),
            ArrowDown: pan(0, -KEY_STEP),
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
