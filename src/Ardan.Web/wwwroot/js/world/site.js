import * as THREE from "https://cdn.jsdelivr.net/npm/three@0.170.0/build/three.module.min.js";

export const BOX = new THREE.BoxGeometry(1, 1, 1);
export const UPRIGHT = new THREE.Quaternion();
export const HIDDEN = new THREE.Vector3(0, 0, 0);

export const cargo = {
    fill: 0xd6ad78,
    soon: 0xec8b22,
    week: 0xc9b030,
    fresh: 0x4fae73,
    expired: 0xd9533d,
    "v-a": 0x25a46c,
    "v-b": 0x4a86d4,
    "v-c": 0x9aa5ae,
};

export const tones = {
    neutral: 0x9aa5ae,
    ok: 0x2fbf73,
    info: 0x3d8ae6,
    warn: 0xf0a12f,
    crit: 0xf0503c,
    brand: 0x22b07a,
};

export const flows = { in: 0x16b364, out: 0x2f7cf6 };

export const palettes = {
    light: {
        sky: 0xdde6e8, grass: 0xb9d293, asphalt: 0xa6abaf, slab: 0xe9e6de, trim: 0xf1efe8,
        post: 0x2d5c9e, beam: 0xe8772e, steel: 0x7d878e, mast: 0x55606a, machine: 0xf2b632, dark: 0x2b3137,
        belt: 0x434b52, trailer: 0xf3f3f0, glass: 0x2a3640, door: 0xc6ccd1, wood: 0xb88a52, cardboard: 0xd6ad78,
        leaf: 0x6a9c4c, trunk: 0x7b5a3a, dim: 0xd5d8d1, focus: 0x2f7fe0,
        roller: ["#8c959b", "#6b747b"], corrugation: ["#557fb9", "#4a73ab"],
        line: "#d9a82a", paint: "rgba(48, 56, 52, 0.42)", label: "rgba(48, 56, 52, 0.62)", hatch: "rgba(205, 70, 50, 0.3)",
        inbound: "#16b364", outbound: "#2f7cf6",
        hemi: [0xffffff, 0x9a9585, 1.9], sun: 2.5,
    },
    dark: {
        sky: 0x0e1318, grass: 0x1a241e, asphalt: 0x252b31, slab: 0x2b323a, trim: 0x46515d,
        post: 0x3b6fb6, beam: 0xd56a2a, steel: 0x6a747d, mast: 0x46505a, machine: 0xe0a92e, dark: 0x1b2025,
        belt: 0x353d45, trailer: 0xb8bdc1, glass: 0x141a20, door: 0x56616b, wood: 0x8f6a3e, cardboard: 0xbf9862,
        leaf: 0x2f4b2b, trunk: 0x4a3a2a, dim: 0x3a434d, focus: 0x6aa7eb,
        roller: ["#5d676f", "#48525a"], corrugation: ["#2f4b72", "#294264"],
        line: "#b38f2c", paint: "rgba(205, 214, 224, 0.28)", label: "rgba(215, 222, 230, 0.55)", hatch: "rgba(235, 106, 82, 0.28)",
        inbound: "#2bd47f", outbound: "#5b9bff",
        hemi: [0xbfd4ff, 0x20262c, 1.6], sun: 1.7,
    },
};

export function themeName() {
    return document.documentElement.dataset.theme === "dark" ? "dark" : "light";
}

export function parseLane(spot) {
    const match = /^L(\d+)·B(\d+)$/.exec(spot ?? "");
    return match ? { level: Number(match[1]), bay: Number(match[2]) } : null;
}

export const smooth = t => t * t * (3 - 2 * t);

export class Site {
    constructor(setup) {
        this.levels = setup.levels;
        this.bays = setup.bays;
        this.depth = setup.laneDepth;
        this.pitch = setup.bayPitchMetres;
        this.storey = setup.levelHeightMetres;
        this.slot = setup.palletPitchMetres;
        this.shafts = setup.lifts.length;
        this.belts = setup.belts;
        this.beltLength = setup.beltLength;
        this.checkAt = setup.profileCheckAt;
        this.docks = setup.docks;

        this.aisleZ = 0.8;
        this.aisleFront = 1.55;
        this.laneLength = this.depth * this.slot + 0.2;
        this.top = this.levels * this.storey + 0.3;
        this.rackStart = this.pitch / 2;
        this.rackEnd = (this.bays + 0.5) * this.pitch;
        this.beltStart = this.aisleFront + 0.2;
        this.beltEnd = this.beltStart + this.beltLength;
        this.beltTop = 0.43;
        this.infeedX = -0.55;
        this.outfeedX = 0.55;
        this.front = this.beltEnd + 1.6;
        this.left = this.shaftX(0) - 2.4;
        this.right = this.rackEnd + 2.2;
        this.back = -this.laneLength - 1.6;
        this.reject = { x: this.infeedX - 2.25, z: this.beltEnd - 1.3, w: 1.9, d: 2.6 };
    }

    get lanes() { return this.levels * this.bays; }
    get centre() { return new THREE.Vector3((this.left + this.right) / 2, 0, (this.back + this.front) / 2); }

    x(bays) { return bays * this.pitch; }
    y(level) { return level * this.storey + 0.16; }
    shaftX(shaft) { return -1.75 * (this.shafts - shaft); }
    placeZ(slot) { return -0.1 - (this.depth - slot - 0.5) * this.slot; }
    dockX(dock) { return 2.8 + (dock - 1) * 3; }
    beltX(inbound) { return inbound ? this.infeedX : this.outfeedX; }
    beltZ(inbound, position) { return inbound ? this.beltEnd - position : this.beltStart + position; }
    laneIndex(level, bay) { return level * this.bays + bay - 1; }
    handoffX(spot) { return spot === "Infeed" ? this.infeedX : spot === "Outfeed" ? this.outfeedX : 0; }
    aisle(x, level) { return new THREE.Vector3(this.x(x), this.y(level), this.aisleZ); }
    carriage(shaft, position) { return new THREE.Vector3(this.shaftX(shaft), this.y(position), this.aisleZ); }
    slotAt(level, bay, slot) { return new THREE.Vector3(this.x(bay), this.y(level), this.placeZ(slot)); }
    beltPoint(inbound, position) { return new THREE.Vector3(this.beltX(inbound), this.beltTop, this.beltZ(inbound, position)); }
    goodsIn() { return new THREE.Vector3(this.infeedX, 1.1, this.front + 1.6); }
    truck(dock) { return new THREE.Vector3(this.dockX(dock), 1.1, this.front + 1.6); }
}

export class Glide {
    constructor(position) {
        this.from = position.clone();
        this.to = position.clone();
        this.now = position.clone();
        this.start = 0;
        this.span = 1;
    }

    set(target, time, span, jump = 9) {
        if (this.now.distanceTo(target) > jump) this.now.copy(target);
        this.from.copy(this.now);
        this.to.copy(target);
        this.start = time;
        this.span = span;
    }

    step(time) {
        return this.now.lerpVectors(this.from, this.to, Math.min(1, (time - this.start) / this.span));
    }
}

export class Path {
    constructor(points, start, ms) {
        this.points = points;
        this.start = start;
        this.ms = ms;
        this.lengths = [0];
        for (let i = 1; i < points.length; i++) this.lengths.push(this.lengths[i - 1] + points[i].distanceTo(points[i - 1]));
    }

    done(time) {
        return time - this.start >= this.ms;
    }

    at(time, out) {
        const total = this.lengths[this.lengths.length - 1];
        const distance = smooth(Math.min(1, Math.max(0, (time - this.start) / this.ms))) * total;
        let i = 1;
        while (i < this.lengths.length - 1 && this.lengths[i] < distance) i++;
        const span = this.lengths[i] - this.lengths[i - 1];
        return out.lerpVectors(this.points[i - 1], this.points[i], span > 0 ? (distance - this.lengths[i - 1]) / span : 1);
    }
}

export function range(from, to) {
    return Array.from({ length: Math.max(0, to - from + 1) }, (_, i) => from + i);
}

export function at(x, y, z, w, h, d) {
    return { x, y, z, w, h, d };
}

export function solid(material, w, h, d, x, y, z) {
    const mesh = new THREE.Mesh(BOX, material);
    mesh.scale.set(w, h, d);
    mesh.position.set(x, y, z);
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    return mesh;
}

export function instanced(boxes, material, shadows = true) {
    const mesh = new THREE.InstancedMesh(BOX, material, boxes.length);
    const matrix = new THREE.Matrix4();
    boxes.forEach((b, i) => mesh.setMatrixAt(i, matrix.compose(new THREE.Vector3(b.x, b.y, b.z), UPRIGHT, new THREE.Vector3(b.w, b.h, b.d))));
    mesh.instanceMatrix.needsUpdate = true;
    mesh.castShadow = shadows;
    mesh.receiveShadow = shadows;
    mesh.computeBoundingSphere();
    return mesh;
}

export function blank(material, count, shadows = true) {
    const mesh = new THREE.InstancedMesh(BOX, material, count);
    const hidden = new THREE.Matrix4().compose(HIDDEN, UPRIGHT, HIDDEN);
    for (let i = 0; i < count; i++) mesh.setMatrixAt(i, hidden);
    mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    mesh.castShadow = shadows;
    mesh.receiveShadow = shadows;
    mesh.frustumCulled = false;
    return mesh;
}
