import * as THREE from "https://cdn.jsdelivr.net/npm/three@0.170.0/build/three.module.min.js";
import { BOX, HIDDEN, UPRIGHT, cargo, flows, tones, palettes, themeName, parseLane, smooth, Site, Glide, Path } from "./world/site.js";
import { SHARED, createMaterials, recolour, buildSite, paintFloor, paintStripes, paintBelt, dispatchRoute, makePallet, makeTaxi, makeLift } from "./world/scene.js";
import { Rig, Controls } from "./world/camera.js";
import { Routes, CHEVRON } from "./world/flow.js";

const MAX_TAXIS = 12;
const BASE_SIZE = new THREE.Vector3(1.0, 0.14, 1.05);
const LOAD_SIZE = new THREE.Vector3(0.92, 0.95, 0.98);
const WHITE = new THREE.Color(0xffffff);

export function supported() {
    try {
        return !!document.createElement("canvas").getContext("webgl2");
    } catch {
        return false;
    }
}

export function create(host, dotnet, setup) {
    return new World(host, dotnet, setup);
}

class World {
    constructor(host, dotnet, setup) {
        this.host = host;
        this.dotnet = dotnet;
        this.site = new Site(setup);
        this.theme = palettes[themeName()];
        this.taxis = new Map();
        this.lifts = new Map();
        this.parcels = new Map();
        this.lanes = new Array(this.site.lanes).fill(null);
        this.hiddenFronts = new Set();
        this.hiddenParcels = new Set();
        this.tags = new Set();
        this.alerts = new Map();
        this.speed = 1;
        this.interval = 250;
        this.paced = { frames: 0, ms: 0 };
        this.lastUpdate = 0;
        this.lastEvent = null;
        this.focusSpot = null;
        this.hovered = null;
        this.hoverLane = null;
        this.width = 1;
        this.height = 1;
        this.ray = new THREE.Raycaster();
        this.pointer = new THREE.Vector2();
        this.projected = new THREE.Vector3();
        this.matrix = new THREE.Matrix4();
        this.dimColor = new THREE.Color(this.theme.dim);

        this.createRenderer();
        this.createScene();
        this.createOverlay();
        this.controls = new Controls(this.canvas, this.rig, {
            pick: (x, y) => this.click(x, y),
            hover: (x, y) => this.hoverAt(x, y),
            leave: () => this.hover(null),
            zoom: (x, y, factor) => this.zoomAt(x, y, factor),
            command: name => this.command(name),
            pixels: () => this.height,
        });
        this.listen();
        this.resize();
        this.intro();
        this.loop = this.loop.bind(this);
        this.raf = requestAnimationFrame(this.loop);
    }

    createRenderer() {
        this.renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: "high-performance" });
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
        this.renderer.shadowMap.enabled = true;
        this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
        this.canvas = this.renderer.domElement;
        this.canvas.className = "world-canvas";
        this.canvas.setAttribute("aria-hidden", "true");
        this.host.appendChild(this.canvas);
        this.camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 400);
        this.rig = new Rig(this.camera, this.site);
    }

    createScene() {
        const s = this.site;
        this.mats = createMaterials(this.theme);
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(this.theme.sky);

        this.hemi = new THREE.HemisphereLight(...this.theme.hemi);
        this.sun = new THREE.DirectionalLight(0xffffff, this.theme.sun);
        this.sun.position.copy(s.centre).add(new THREE.Vector3(-18, 36, 26));
        this.sun.target.position.copy(s.centre);
        this.sun.castShadow = true;
        this.sun.shadow.mapSize.set(2048, 2048);
        Object.assign(this.sun.shadow.camera, { left: -36, right: 36, top: 32, bottom: -32, near: 1, far: 120 });
        this.sun.shadow.camera.updateProjectionMatrix();
        this.sun.shadow.bias = -0.0004;
        this.sun.shadow.normalBias = 0.03;
        this.scene.add(this.hemi, this.sun, this.sun.target);

        Object.assign(this, buildSite({ scene: this.scene, mats: this.mats, site: s, theme: this.theme, renderer: this.renderer }));
        this.routes = new Routes(this.scene, MAX_TAXIS);

        this.hoverFill = new THREE.Mesh(BOX, this.mats.hoverFill);
        this.hoverLine = new THREE.LineSegments(new THREE.EdgesGeometry(BOX), this.mats.hoverLine);
        this.selectLine = new THREE.LineSegments(new THREE.EdgesGeometry(BOX), this.mats.selectLine);
        for (const marker of [this.hoverFill, this.hoverLine, this.selectLine]) {
            marker.renderOrder = 10;
            marker.visible = false;
            this.scene.add(marker);
        }
    }

    createOverlay() {
        this.overlay = document.createElement("div");
        this.overlay.className = "world-overlay";
        this.host.appendChild(this.overlay);
        for (let level = 0; level < this.site.levels; level++) {
            const anchor = new THREE.Vector3(this.site.rackEnd + 0.9, this.site.y(level) + 0.75, 0.6);
            this.addTag(`L${level}`, "world-tag is-level", () => anchor);
        }
    }

    addTag(text, className, at) {
        const el = document.createElement("span");
        el.className = className;
        el.textContent = text;
        this.overlay.appendChild(el);
        const tag = { el, at };
        this.tags.add(tag);
        return tag;
    }

    removeTag(tag) {
        tag.el.remove();
        this.tags.delete(tag);
    }

    listen() {
        this.onFullscreen = () => {
            if (!document.fullscreenElement && this.immersive) this.setImmersive(false);
        };
        document.addEventListener("fullscreenchange", this.onFullscreen);
        this.resizer = new ResizeObserver(() => this.resize());
        this.resizer.observe(this.host);
        this.themer = new MutationObserver(() => this.applyTheme());
        this.themer.observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });
    }

    resize() {
        const box = this.host.getBoundingClientRect();
        this.width = Math.max(1, Math.round(box.width));
        this.height = Math.max(1, Math.round(box.height));
        this.renderer.setSize(this.width, this.height, false);
    }

    get aspect() {
        return this.width / this.height;
    }

    intro() {
        const home = this.rig.homeFor(this.aspect);
        Object.assign(this.rig, { azimuth: home.azimuth - 0.5, elevation: home.elevation + 0.18, size: home.size * 1.3 });
        this.rig.target.copy(home.target);
        this.rig.fly(home, 1400);
    }

    update(frame) {
        const now = performance.now();
        if (this.lastUpdate) this.interval = this.interval * 0.7 + THREE.MathUtils.clamp(now - this.lastUpdate, 120, 700) * 0.3;
        const first = !this.lastUpdate;
        this.lastUpdate = now;
        this.speed = frame.speed;
        this.rejectTip = frame.rejectTip;
        this.applyLanes(frame.lanes);
        this.applyLifts(frame.lifts, now);
        this.applyTaxis(frame.taxis, frame.lifts, now);
        const departed = this.applyBelts(frame.belts, now, first);
        this.applyRejects(frame.rejects);
        this.applyAlerts(frame.markers);
        this.applyEvents(frame.events, departed, now);
        this.focusSpot = frame.highlight ?? null;
    }

    applyLanes(lanes) {
        for (const lane of lanes) {
            const index = this.site.laneIndex(lane.level, lane.bay);
            this.lanes[index] = lane;
            this.drawLane(index);
        }
        if (lanes.length) this.markStock();
    }

    drawLane(index) {
        const lane = this.lanes[index];
        if (!lane) return;
        const s = this.site;
        const shown = lane.count - (this.hiddenFronts.has(index) ? 1 : 0);
        const tint = this.tint(lane, index === this.hoverLane);
        for (let slot = 0; slot < s.depth; slot++) {
            const i = index * s.depth + slot;
            if (slot >= shown) {
                this.matrix.compose(HIDDEN, UPRIGHT, HIDDEN);
                this.bases.setMatrixAt(i, this.matrix);
                this.loads.setMatrixAt(i, this.matrix);
                continue;
            }
            const place = s.slotAt(lane.level, lane.bay, slot);
            this.bases.setMatrixAt(i, this.matrix.compose(place.clone().setY(place.y + 0.07), UPRIGHT, BASE_SIZE));
            this.loads.setMatrixAt(i, this.matrix.compose(place.setY(place.y + 0.615), UPRIGHT, LOAD_SIZE));
            this.loads.setColorAt(i, tint);
        }
    }

    markStock() {
        this.bases.instanceMatrix.needsUpdate = true;
        this.loads.instanceMatrix.needsUpdate = true;
        this.loads.instanceColor.needsUpdate = true;
    }

    tint(lane, hovered) {
        const color = new THREE.Color(cargo[lane.tone] ?? this.theme.cardboard);
        if (hovered) return color.lerp(WHITE, 0.3);
        if (lane.dim) return color.lerp(this.dimColor, 0.72);
        return lane.lit ? color.lerp(WHITE, 0.12) : color;
    }

    applyLifts(lifts, now) {
        for (const lift of lifts) {
            const entry = this.lifts.get(lift.id) ?? this.addLift(lift);
            entry.data = lift;
            entry.glide.set(this.site.carriage(lift.shaft, lift.position), now, this.interval);
            entry.led.material.color.setHex(tones[lift.tone] ?? tones.neutral);
        }
    }

    addLift(lift) {
        const model = makeLift(this.mats);
        const start = this.site.carriage(lift.shaft, lift.position);
        model.group.position.copy(start);
        model.group.userData.pick = { kind: "Lift", id: lift.id };
        const entry = { ...model, glide: new Glide(start) };
        entry.tag = this.addTag(lift.id, "world-tag is-lift", () => model.group.position.clone().setY(model.group.position.y + 0.95));
        this.lifts.set(lift.id, entry);
        this.pickables.push(model.group);
        this.scene.add(model.group);
        return entry;
    }

    applyTaxis(taxis, lifts, now) {
        const s = this.site;
        for (const taxi of taxis) {
            const entry = this.taxis.get(taxi.id) ?? this.addTaxi(taxi);
            entry.data = taxi;
            const ride = taxi.lift ? lifts.find(lift => lift.id === taxi.lift) : null;
            const position = ride ? s.carriage(ride.shaft, ride.position) : s.aisle(taxi.x, taxi.level);
            if (!ride && taxi.level === 0 && taxi.x < 0.05) position.x = s.handoffX(taxi.loaded ? taxi.to : taxi.from);
            entry.ride = ride?.id ?? null;
            entry.glide.set(position, now, this.interval);
            this.track(entry, taxi, now);
            entry.led.material.color.setHex(tones[taxi.tone] ?? tones.neutral);
            const look = `world-tag is-taxi ${taxi.flow ? `flow-${taxi.flow}` : `tone-${taxi.tone}`}`;
            if (entry.tag.el.className !== look) entry.tag.el.className = look;
            if (entry.tag.el.textContent !== taxi.label) entry.tag.el.textContent = taxi.label;
        }
    }

    addTaxi(taxi) {
        const model = makeTaxi(this.mats);
        const ghost = makePallet(this.mats);
        ghost.visible = false;
        const start = this.site.aisle(taxi.x, taxi.level);
        model.group.position.copy(start);
        model.group.userData.pick = { kind: "Taxi", id: taxi.id };
        const entry = { ...model, ghost, glide: new Glide(start), handling: null };
        entry.tag = this.addTag(taxi.id, "world-tag is-taxi", () =>
            model.group.position.clone().setY(model.group.position.y + (model.carried.visible ? 1.5 : 0.72)));
        this.taxis.set(taxi.id, entry);
        this.pickables.push(model.group);
        this.scene.add(model.group, ghost);
        return entry;
    }

    track(entry, taxi, now) {
        if (!taxi.phase || taxi.handling === null || taxi.handling === undefined) {
            entry.handling = null;
            return;
        }
        const spot = taxi.phase === "pick" ? taxi.from : taxi.to;
        const was = entry.handling;
        const same = was && was.phase === taxi.phase && was.spot === spot;
        let rate = same ? was.rate : this.speed / 6000;
        if (same && taxi.handling > was.p && now > was.at) rate = (taxi.handling - was.p) / (now - was.at);
        entry.handling = { phase: taxi.phase, spot, lane: parseLane(spot), p: taxi.handling, rate, at: now };
    }

    applyBelts(belts, now, first) {
        const s = this.site;
        const seen = new Set();
        for (const belt of belts) {
            const entry = this.belts.get(belt.id);
            if (!entry) continue;
            entry.data = belt;
            entry.running = belt.running;
            entry.top.material.color.setHex(belt.running ? 0xffffff : 0xffb4a8);
            for (const item of belt.items) {
                seen.add(item.palletId);
                const position = Math.min(item.position, belt.length);
                const target = s.beltPoint(entry.inbound, position);
                const parcel = this.parcels.get(item.palletId) ?? this.addParcel(item.palletId, entry.inbound, target, position, now, first);
                parcel.position = position;
                parcel.target = target;
                if (!parcel.path) parcel.glide.set(target, now, this.interval);
            }
        }
        const departed = [];
        for (const [id, parcel] of this.parcels) {
            if (seen.has(id) || parcel.leaving) continue;
            parcel.leaving = true;
            departed.push(parcel);
        }
        return departed;
    }

    addParcel(id, inbound, target, position, now, first) {
        const s = this.site;
        const group = makePallet(this.mats);
        group.position.copy(target);
        this.scene.add(group);
        const parcel = { id, group, inbound, position, target, glide: new Glide(target), path: null, leaving: false };
        if (inbound && !first && position < 0.6) {
            parcel.path = new Path([s.goodsIn(), new THREE.Vector3(s.infeedX, 0.12, s.front - 0.3), target.clone()], now, 1100);
            parcel.path.at(now, group.position);
        }
        this.parcels.set(id, parcel);
        return parcel;
    }

    removeParcel(parcel) {
        this.scene.remove(parcel.group);
        this.parcels.delete(parcel.id);
    }

    applyRejects(count) {
        this.rejects.forEach((pallet, i) => pallet.visible = i < count);
    }

    applyAlerts(markers) {
        const live = new Set();
        for (const marker of markers) {
            live.add(marker.alarm);
            let alert = this.alerts.get(marker.alarm);
            if (!alert) {
                const el = document.createElement("button");
                el.type = "button";
                el.textContent = "!";
                el.addEventListener("click", () => this.pick("Alarm", marker.alarm));
                this.overlay.appendChild(el);
                alert = { el, spot: marker.spot, key: "" };
                alert.at = () => this.spotPosition(alert.spot, 1.0);
                this.tags.add(alert);
                this.alerts.set(marker.alarm, alert);
            }
            alert.spot = marker.spot;
            const key = `${marker.tone}|${marker.title}|${marker.detail}`;
            if (alert.key === key) continue;
            alert.key = key;
            alert.el.className = `world-alert tone-${marker.tone}`;
            alert.el.setAttribute("aria-label", marker.title);
            alert.el.dataset.tip = marker.title;
            alert.el.dataset.tipText = marker.detail;
        }
        for (const [id, alert] of this.alerts) {
            if (live.has(id)) continue;
            this.removeTag(alert);
            this.alerts.delete(id);
        }
    }

    applyEvents(events, departed, now) {
        const dispatched = [];
        if (events?.length) {
            const newest = events[0].seq;
            if (this.lastEvent !== null) {
                for (const event of events.filter(e => e.seq > this.lastEvent).reverse()) {
                    if (event.kind === "Dispatched") dispatched.push(event.spot);
                    const where = this.spotPosition(event.spot, 0.6);
                    if (where && event.points !== 0) this.popup(where, event.points);
                }
            }
            this.lastEvent = Math.max(this.lastEvent ?? newest, newest);
        }
        this.send(departed, dispatched, now);
    }

    send(departed, dispatched, now) {
        const s = this.site;
        for (const parcel of departed) {
            const from = parcel.group.position.clone();
            if (!parcel.inbound && parcel.position >= s.beltLength - 0.4) {
                const dock = Number(/^Dock (\d+)$/.exec(dispatched.shift() ?? "")?.[1] ?? 1);
                const route = dispatchRoute(s, dock).slice(1).map(([x, z]) => new THREE.Vector3(x, 0.05, z));
                parcel.path = new Path([from, ...route, s.truck(dock)], now, 2800);
            } else if (parcel.inbound && parcel.position < s.beltLength - 0.4) {
                parcel.path = new Path([from, new THREE.Vector3(from.x, 0.05, from.z + 0.6), new THREE.Vector3(s.reject.x, 0.02, s.reject.z)], now, 1400);
            } else {
                this.removeParcel(parcel);
            }
        }
    }

    popup(where, points) {
        const el = document.createElement("span");
        el.className = `world-pop ${points > 0 ? "is-gain" : "is-loss"}`;
        el.textContent = points > 0 ? `+${points}` : `−${Math.abs(points)}`;
        this.overlay.appendChild(el);
        const tag = { el, at: () => where };
        this.tags.add(tag);
        setTimeout(() => this.removeTag(tag), 1700);
    }

    spotPosition(spot, raise = 0) {
        if (!spot) return null;
        const s = this.site;
        const taxi = this.taxis.get(spot);
        if (taxi) return taxi.group.position.clone().setY(taxi.group.position.y + (taxi.carried.visible ? 1.4 : 0.5) + raise);
        const lift = this.lifts.get(spot);
        if (lift) return lift.group.position.clone().setY(lift.group.position.y + 0.6 + raise);
        const belt = this.beltFor(spot);
        if (belt) return belt.centre.clone().setY(1.35 + raise);
        if (spot === "Reject") return new THREE.Vector3(s.reject.x, 1.2 + raise, s.reject.z);
        const dock = /^Dock (\d+)$/.exec(spot);
        if (dock) return new THREE.Vector3(s.dockX(Number(dock[1])), 3.2 + raise, s.front - 0.4);
        const lane = parseLane(spot);
        return lane ? new THREE.Vector3(s.x(lane.bay), s.y(lane.level) + 1.1 + raise, -0.3) : null;
    }

    beltFor(spot) {
        for (const belt of this.belts.values()) {
            if (belt.id === spot || belt.name === spot) return belt;
        }
        return null;
    }

    focus(spot) {
        const where = this.spotPosition(spot);
        if (!where) return;
        const goal = this.rig.goal();
        this.rig.fly({ target: new THREE.Vector3(where.x, Math.max(1.5, where.y - 0.8), where.z + 1.5), size: Math.min(goal.size, 20) });
    }

    command(name) {
        const goal = this.rig.goal();
        switch (name) {
            case "rotate-left": this.rig.fly({ azimuth: goal.azimuth - Math.PI / 4 }, 450); break;
            case "rotate-right": this.rig.fly({ azimuth: goal.azimuth + Math.PI / 4 }, 450); break;
            case "zoom-in": this.rig.fly({ size: goal.size / 1.35 }, 280); break;
            case "zoom-out": this.rig.fly({ size: goal.size * 1.35 }, 280); break;
            case "home": this.rig.fly(this.rig.homeFor(this.aspect), 700); break;
            case "fullscreen": this.setImmersive(!this.immersive); break;
        }
    }

    labels(show) {
        this.overlay.classList.toggle("is-quiet", !show);
    }

    setImmersive(on, notify = true) {
        this.immersive = on;
        const root = document.documentElement;
        root.classList.toggle("is-immersive", on);
        if (on && !document.fullscreenElement) root.requestFullscreen?.().catch(() => {});
        if (!on && document.fullscreenElement) document.exitFullscreen?.().catch(() => {});
        if (notify) this.dotnet.invokeMethodAsync("Immersive", on).catch(() => {});
    }

    zoomAt(clientX, clientY, factor) {
        const before = this.groundAt(clientX, clientY);
        this.rig.zoomTo(this.rig.size * factor);
        this.rig.apply(this.aspect);
        const after = this.groundAt(clientX, clientY);
        if (!before || !after) return;
        this.rig.target.add(before.sub(after));
        this.rig.clamp();
    }

    groundAt(clientX, clientY) {
        this.aim(clientX, clientY);
        return this.ray.ray.intersectPlane(new THREE.Plane(new THREE.Vector3(0, 1, 0), -this.rig.target.y), new THREE.Vector3());
    }

    aim(clientX, clientY) {
        const box = this.canvas.getBoundingClientRect();
        this.pointer.set(((clientX - box.left) / box.width) * 2 - 1, -((clientY - box.top) / box.height) * 2 + 1);
        this.ray.setFromCamera(this.pointer, this.camera);
    }

    pickAt(clientX, clientY) {
        this.aim(clientX, clientY);
        for (const hit of this.ray.intersectObjects(this.pickables, true)) {
            let node = hit.object;
            while (node && !node.userData.pick) node = node.parent;
            if (!node) continue;
            const pick = node.userData.pick;
            if (pick.kind !== "Lane") return { ...pick };
            const index = hit.instanceId;
            if (index === undefined || index === null) continue;
            const level = Math.floor(index / this.site.bays);
            const bay = (index % this.site.bays) + 1;
            return { kind: "Lane", id: `L${level}·B${String(bay).padStart(2, "0")}`, index, level, bay };
        }
        return null;
    }

    click(clientX, clientY) {
        const hit = this.pickAt(clientX, clientY);
        this.pick(hit?.kind ?? "None", hit?.id ?? "");
    }

    pick(kind, id) {
        this.dotnet.invokeMethodAsync("Pick", kind, id).catch(() => {});
    }

    hoverAt(clientX, clientY) {
        this.hoverPoint = { x: clientX, y: clientY };
        if (this.hoverQueued) return;
        this.hoverQueued = true;
        requestAnimationFrame(() => {
            this.hoverQueued = false;
            if (!this.hoverPoint || this.disposed) return;
            this.hover(this.pickAt(this.hoverPoint.x, this.hoverPoint.y), this.hoverPoint);
        });
    }

    hover(hit, point) {
        if (!hit) this.hoverPoint = null;
        const lane = hit?.kind === "Lane" ? hit.index : null;
        if (lane !== this.hoverLane) {
            const before = this.hoverLane;
            this.hoverLane = lane;
            if (before !== null) this.drawLane(before);
            if (lane !== null) this.drawLane(lane);
            this.markStock();
        }
        this.hovered = hit;
        this.canvas.style.cursor = hit ? "pointer" : "";
        const content = hit && point ? this.tipFor(hit) : null;
        if (content) window.ardan?.tip?.follow(content, point.x, point.y);
        else window.ardan?.tip?.unfollow();
    }

    tipFor(hit) {
        if (hit.kind === "Lane") {
            const lane = this.lanes[hit.index];
            return lane ? { ...lines(lane.tip), meter: { value: lane.count, max: this.site.depth } } : { title: hit.id, lines: [] };
        }
        const text = {
            Taxi: this.taxis.get(hit.id)?.data?.tip,
            Lift: this.lifts.get(hit.id)?.data?.tip,
            Belt: this.belts.get(hit.id)?.data?.tip,
            Reject: this.rejectTip,
        }[hit.kind];
        return text ? lines(text) : null;
    }

    boxFor(spot) {
        if (!spot) return null;
        const s = this.site;
        const V = (x, y, z) => new THREE.Vector3(x, y, z);
        const lane = parseLane(spot);
        if (lane) return [V(s.x(lane.bay), lane.level * s.storey + s.storey / 2 + 0.08, -s.laneLength / 2), V(s.pitch - 0.06, s.storey - 0.08, s.laneLength + 0.08)];
        const taxi = this.taxis.get(spot);
        if (taxi) {
            const p = taxi.group.position;
            const h = taxi.carried.visible ? 1.5 : 0.62;
            return [V(p.x, p.y + h / 2, p.z), V(1.45, h, 1.35)];
        }
        const lift = this.lifts.get(spot);
        if (lift) return [lift.group.position.clone().setY(lift.group.position.y + 0.42), V(1.6, 0.95, 1.5)];
        const belt = this.beltFor(spot);
        if (belt) return [V(belt.centre.x, 0.55, belt.centre.z), V(1.2, 1.1, s.beltLength + 0.2)];
        if (spot === "Reject") return [V(s.reject.x, 0.7, s.reject.z), V(s.reject.w, 1.4, s.reject.d)];
        return null;
    }

    placeBox(object, spot) {
        const box = this.boxFor(spot);
        object.visible = !!box;
        if (!box) return;
        object.position.copy(box[0]);
        object.scale.copy(box[1]);
    }

    placeMarkers(time) {
        this.placeBox(this.selectLine, this.focusSpot);
        this.mats.selectLine.opacity = 0.7 + Math.sin(time / 260) * 0.3;
        const hovered = this.hovered && this.hovered.id !== this.focusSpot ? this.hovered : null;
        this.placeBox(this.hoverLine, hovered ? hovered.id || hovered.kind : null);
        this.hoverFill.visible = this.hoverLine.visible && hovered?.kind === "Lane";
        if (this.hoverFill.visible) {
            this.hoverFill.position.copy(this.hoverLine.position);
            this.hoverFill.scale.copy(this.hoverLine.scale);
        }
    }

    routeList() {
        const list = [];
        for (const entry of this.taxis.values()) {
            const taxi = entry.data;
            if (!taxi?.flow || list.length === MAX_TAXIS) continue;
            const next = entry.handling?.spot ?? (taxi.loaded ? taxi.to : taxi.from);
            if (!next) continue;
            list.push({ color: flows[taxi.flow], points: entry.handling ? [] : this.pathTo(entry, next), frame: this.frameFor(next) });
        }
        return list;
    }

    pathTo(entry, spot) {
        const s = this.site;
        const lane = parseLane(spot);
        const level = lane ? lane.level : 0;
        const goal = lane ? s.aisle(lane.bay, lane.level) : new THREE.Vector3(s.handoffX(spot), s.y(0), s.aisleZ);
        const from = entry.group.position.clone();
        const points = [from];
        if (entry.ride) {
            points.push(new THREE.Vector3(from.x, s.y(level), s.aisleZ));
        } else if (Math.abs(from.y - s.y(level)) > 0.05) {
            const shaft = s.shaftX(s.shafts - 1);
            points.push(new THREE.Vector3(shaft, from.y, s.aisleZ), new THREE.Vector3(shaft, s.y(level), s.aisleZ));
        }
        points.push(goal);
        return points.map(point => point.setY(point.y + 0.42));
    }

    frameFor(spot) {
        const s = this.site;
        const lane = parseLane(spot);
        if (lane) {
            return {
                position: new THREE.Vector3(s.x(lane.bay), lane.level * s.storey + s.storey / 2 + 0.08, -0.04),
                size: new THREE.Vector3(s.pitch - 0.12, s.storey - 0.2, 0.1),
            };
        }
        if (spot !== "Infeed" && spot !== "Outfeed") return null;
        return {
            position: new THREE.Vector3(s.beltX(spot === "Infeed"), s.beltTop + 0.62, s.beltStart),
            size: new THREE.Vector3(1.08, 1.26, 1.16),
        };
    }

    infeedEnd() {
        let end = null;
        for (const parcel of this.parcels.values()) {
            if (parcel.inbound && !parcel.leaving && parcel.position >= this.site.beltLength - 0.3 && (!end || parcel.position > end.position)) end = parcel;
        }
        return end;
    }

    animateTaxi(entry, time, fronts, parcels) {
        const s = this.site;
        const taxi = entry.data;
        entry.group.position.copy(entry.glide.step(time));
        entry.led.visible = taxi.tone !== "crit" || Math.floor(time / 300) % 2 === 0;

        const h = entry.handling;
        let reach = 0;
        let carry = !!taxi.loaded;
        let ghost = null;
        if (h) {
            const p = Math.min(1, Math.max(0, h.p + h.rate * (time - h.at)));
            const extend = smooth(p < 0.5 ? p / 0.5 : (1 - p) / 0.5);
            const done = p >= 0.5;
            if (h.lane) {
                const index = s.laneIndex(h.lane.level, h.lane.bay);
                const count = this.lanes[index]?.count ?? 0;
                const slot = h.phase === "pick" ? Math.max(0, count - 1) : Math.min(s.depth - 1, count);
                reach = (s.placeZ(slot) - s.aisleZ) * extend;
                if (done && h.phase === "pick") fronts.add(index);
                if (done && h.phase === "drop") ghost = s.slotAt(h.lane.level, h.lane.bay, slot);
            } else {
                reach = (s.beltStart - s.aisleZ) * extend;
                const end = h.phase === "pick" && done ? this.infeedEnd() : null;
                if (end) parcels.add(end.id);
                if (done && h.phase === "drop") ghost = s.beltPoint(false, 0);
            }
            carry = h.phase === "pick" ? done : !done;
        }
        entry.satellite.position.z = reach;
        entry.carried.visible = carry;
        entry.ghost.visible = !!ghost;
        if (ghost) entry.ghost.position.copy(ghost);
    }

    animateParcels(time) {
        for (const parcel of [...this.parcels.values()]) {
            if (parcel.path) {
                parcel.path.at(time, parcel.group.position);
                if (parcel.path.done(time)) {
                    if (parcel.leaving) {
                        this.removeParcel(parcel);
                        continue;
                    }
                    parcel.path = null;
                    parcel.glide.now.copy(parcel.group.position);
                    parcel.glide.set(parcel.target, time, 200);
                }
            } else {
                parcel.group.position.copy(parcel.glide.step(time));
            }
            parcel.group.visible = !this.hiddenParcels.has(parcel.id);
        }
    }

    animate(time, dt) {
        const fronts = new Set();
        const parcels = new Set();
        for (const entry of this.taxis.values()) this.animateTaxi(entry, time, fronts, parcels);
        const changed = [...new Set([...fronts, ...this.hiddenFronts])].filter(index => fronts.has(index) !== this.hiddenFronts.has(index));
        this.hiddenFronts = fronts;
        this.hiddenParcels = parcels;
        if (changed.length) {
            changed.forEach(index => this.drawLane(index));
            this.markStock();
        }

        for (const entry of this.lifts.values()) entry.group.position.copy(entry.glide.step(time));
        this.animateParcels(time);

        const flow = 0.18 * this.speed * dt;
        for (const belt of this.belts.values()) {
            if (belt.running) belt.texture.offset.y += (belt.inbound ? -flow : flow) / belt.texture.userData.metres;
        }
        this.routes.draw(this.routeList(), time);
    }

    placeTags() {
        const w = this.width;
        const h = this.height;
        for (const tag of this.tags) {
            const at = tag.at();
            if (!at) {
                tag.el.style.visibility = "hidden";
                continue;
            }
            const p = this.projected.copy(at).project(this.camera);
            const inside = p.x > -1.1 && p.x < 1.1 && p.y > -1.1 && p.y < 1.1;
            tag.el.style.visibility = inside ? "" : "hidden";
            if (inside) tag.el.style.transform = `translate3d(${((p.x + 1) / 2 * w).toFixed(1)}px, ${((1 - p.y) / 2 * h).toFixed(1)}px, 0)`;
        }
    }

    loop(time) {
        if (this.disposed) return;
        this.raf = requestAnimationFrame(this.loop);
        const ms = time - (this.last ?? time);
        this.last = time;
        this.pace(ms);
        this.rig.step(time);
        this.rig.apply(this.aspect);
        this.animate(time, Math.min(0.1, ms / 1000));
        this.placeMarkers(time);
        this.placeTags();
        this.renderer.render(this.scene, this.camera);
    }

    pace(ms) {
        if (document.visibilityState !== "visible" || ms <= 0 || ms > 100) return;
        this.paced.frames++;
        this.paced.ms += ms;
        if (this.paced.frames < 120) return;
        const ratio = this.renderer.getPixelRatio();
        if (this.paced.ms / this.paced.frames > 26 && ratio > 1) this.renderer.setPixelRatio(Math.max(1, ratio - 0.5));
        this.paced = { frames: 0, ms: 0 };
    }

    applyTheme() {
        const name = themeName();
        if (this.theme === palettes[name]) return;
        const p = this.theme = palettes[name];
        recolour(this.mats, p);
        this.dimColor.setHex(p.dim);
        this.scene.background.setHex(p.sky);
        this.hemi.color.setHex(p.hemi[0]);
        this.hemi.groundColor.setHex(p.hemi[1]);
        this.hemi.intensity = p.hemi[2];
        this.sun.intensity = p.sun;
        for (const wall of this.walls) paintStripes(wall.material.map, p.corrugation);
        for (const belt of this.belts.values()) paintBelt(belt.texture, p, belt.inbound);
        paintFloor(this.floor, this.site, p);
        this.lanes.forEach((lane, index) => lane && this.drawLane(index));
        this.markStock();
    }

    dispose() {
        this.disposed = true;
        cancelAnimationFrame(this.raf);
        window.ardan?.tip?.unfollow();
        if (this.immersive) this.setImmersive(false, false);
        this.controls.dispose();
        this.resizer.disconnect();
        this.themer.disconnect();
        document.removeEventListener("fullscreenchange", this.onFullscreen);
        const materials = new Set(Object.values(this.mats).flat());
        this.scene.traverse(node => {
            if (node.geometry && !SHARED.has(node.geometry) && node.geometry !== CHEVRON) node.geometry.dispose();
            if (node.material) materials.add(node.material);
        });
        for (const material of materials) {
            material.map?.dispose();
            material.dispose();
        }
        this.renderer.dispose();
        this.renderer.forceContextLoss();
        this.host.replaceChildren();
    }
}

function lines(text) {
    const [title, ...rest] = text.split("\n");
    return { title, lines: rest };
}
