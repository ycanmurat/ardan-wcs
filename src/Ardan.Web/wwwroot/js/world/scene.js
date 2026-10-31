import * as THREE from "https://cdn.jsdelivr.net/npm/three@0.170.0/build/three.module.min.js";
import { BOX, UPRIGHT, cargo, tones, at, range, solid, instanced, blank } from "./site.js";

const WHEEL = new THREE.CylinderGeometry(0.45, 0.45, 0.32, 14).rotateZ(Math.PI / 2);
const TRUNK = new THREE.CylinderGeometry(0.16, 0.22, 1.2, 7);
const CROWN = new THREE.ConeGeometry(1.25, 3, 8);
const LED = new THREE.BoxGeometry(0.16, 0.08, 0.16);
const PAINTED = ["grass", "asphalt", "slab", "trim", "post", "beam", "steel", "mast", "machine", "dark", "belt", "trailer", "glass", "door", "wood", "cardboard", "leaf", "trunk"];
const FLOOR_PPM = 48;
const BELT_TILE = 0.9;

export const SHARED = new Set([BOX, WHEEL, TRUNK, CROWN, LED]);

export function createMaterials(theme) {
    const lambert = color => new THREE.MeshLambertMaterial({ color });
    return {
        ...Object.fromEntries(PAINTED.map(key => [key, lambert(theme[key])])),
        load: lambert(0xffffff),
        charger: new THREE.MeshBasicMaterial({ color: tones.ok }),
        stripe: lambert(tones.brand),
        cabs: [0x2f6db2, 0xd2452f, 0x23955f, 0xe3a21f].map(lambert),
        hit: new THREE.MeshBasicMaterial({ visible: false }),
        hoverFill: new THREE.MeshBasicMaterial({ color: theme.focus, transparent: true, opacity: 0.2, depthWrite: false }),
        hoverLine: new THREE.LineBasicMaterial({ color: theme.focus, transparent: true, depthTest: false }),
        selectLine: new THREE.LineBasicMaterial({ color: theme.focus, transparent: true, depthTest: false }),
    };
}

export function recolour(mats, theme) {
    for (const key of PAINTED) mats[key].color.setHex(theme[key]);
    for (const key of ["hoverFill", "hoverLine", "selectLine"]) mats[key].color.setHex(theme.focus);
}

export function buildSite({ scene, mats, site, theme, renderer }) {
    const built = { pickables: [] };
    Object.assign(built, buildGround(scene, mats, site, theme, renderer));
    buildRack(scene, mats, site);
    buildShafts(scene, mats, site);
    Object.assign(built, buildBelts(scene, mats, site, theme, built.pickables));
    buildDocks(scene, mats, site);
    Object.assign(built, buildStock(scene, mats, site, built.pickables));
    return built;
}

function buildGround(scene, mats, site, theme, renderer) {
    const s = site;
    const width = s.right - s.left;
    const depth = s.front - s.back;

    const grass = new THREE.Mesh(new THREE.PlaneGeometry(420, 420), mats.grass);
    grass.rotation.x = -Math.PI / 2;
    grass.position.set(s.centre.x, -0.06, s.centre.z);
    grass.receiveShadow = true;

    const apronEnd = s.dockX(s.docks) + 4.5;
    const apron = solid(mats.asphalt, apronEnd - s.left, 0.04, 12, (s.left + apronEnd) / 2, -0.03, s.front + 6);
    const slab = solid(mats.slab, width, 0.2, depth, s.centre.x, -0.1, s.centre.z);
    apron.castShadow = slab.castShadow = false;

    const floor = new THREE.CanvasTexture(document.createElement("canvas"));
    floor.colorSpace = THREE.SRGBColorSpace;
    floor.anisotropy = renderer.capabilities.getMaxAnisotropy();
    paintFloor(floor, s, theme);
    const decal = new THREE.Mesh(new THREE.PlaneGeometry(width, depth), new THREE.MeshLambertMaterial({ map: floor, transparent: true, depthWrite: false }));
    decal.rotation.x = -Math.PI / 2;
    decal.position.set(s.centre.x, 0.01, s.centre.z);
    decal.receiveShadow = true;

    const wallHeight = s.top + 1.2;
    const walls = [
        tiled(solid(mats.slab, width, wallHeight, 0.25, s.centre.x, wallHeight / 2, s.back), theme.corrugation, width),
        tiled(solid(mats.slab, 0.25, wallHeight, depth, s.right, wallHeight / 2, s.centre.z), theme.corrugation, depth),
    ];
    const trims = [
        solid(mats.trim, width + 0.3, 0.35, 0.4, s.centre.x, wallHeight, s.back),
        solid(mats.trim, 0.4, 0.35, depth, s.right, wallHeight, s.centre.z),
    ];

    const kerbs = [solid(mats.trim, 0.25, 0.9, depth, s.left, 0.45, s.centre.z)];
    const doors = [s.infeedX, ...range(1, s.docks).map(dock => s.dockX(dock))].sort((a, b) => a - b);
    let from = s.left;
    for (const to of [...doors.map(x => x - 1.5), s.right]) {
        if (to - from > 0.05) kerbs.push(solid(mats.trim, to - from, 0.9, 0.25, (from + to) / 2, 0.45, s.front));
        from = to + 3;
    }

    const trees = [[-10, -6], [-11, 3], [-9.5, 12], [s.right + 4, -8], [s.right + 5, 1], [s.right + 3.5, 10], [16, s.front + 9], [22, s.front + 7.5], [27, s.front + 10], [6, s.back - 5], [14, s.back - 4.5]]
        .map(([x, z], i) => tree(mats, x, z, 0.85 + (i % 3) * 0.15));

    scene.add(grass, apron, slab, decal, ...walls, ...trims, ...kerbs, ...trees);
    return { walls, floor };
}

function tiled(mesh, colors, length) {
    const texture = new THREE.CanvasTexture(document.createElement("canvas"));
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.wrapS = texture.wrapT = THREE.RepeatWrapping;
    texture.repeat.set(length / 0.6, 1);
    paintStripes(texture, colors);
    mesh.material = new THREE.MeshLambertMaterial({ color: 0xffffff, map: texture });
    return mesh;
}

export function paintStripes(texture, colors) {
    const canvas = texture.image;
    canvas.width = 32;
    canvas.height = 8;
    const g = canvas.getContext("2d");
    g.fillStyle = colors[0];
    g.fillRect(0, 0, 32, 8);
    g.fillStyle = colors[1];
    g.fillRect(0, 0, 11, 8);
    texture.needsUpdate = true;
}

export function paintBelt(texture, theme, inbound) {
    const canvas = texture.image;
    canvas.width = 64;
    canvas.height = 64;
    const g = canvas.getContext("2d");
    g.fillStyle = theme.roller[0];
    g.fillRect(0, 0, 64, 64);
    g.fillStyle = theme.roller[1];
    for (let y = 0; y < 64; y += 16) g.fillRect(0, y, 64, 5);
    g.strokeStyle = inbound ? theme.inbound : theme.outbound;
    g.lineWidth = 7;
    g.lineCap = "round";
    g.lineJoin = "round";
    g.beginPath();
    const [tip, tail] = inbound ? [24, 42] : [40, 22];
    g.moveTo(17, tail);
    g.lineTo(32, tip);
    g.lineTo(47, tail);
    g.stroke();
    texture.needsUpdate = true;
}

export function paintFloor(texture, site, theme) {
    const s = site;
    const ppm = FLOOR_PPM;
    const canvas = texture.image;
    canvas.width = Math.round((s.right - s.left) * ppm);
    canvas.height = Math.round((s.front - s.back) * ppm);
    const g = canvas.getContext("2d");
    const u = x => (x - s.left) * ppm;
    const v = z => (z - s.back) * ppm;
    g.clearRect(0, 0, canvas.width, canvas.height);

    g.strokeStyle = theme.line;
    g.lineWidth = 0.12 * ppm;
    g.beginPath();
    g.moveTo(u(s.shaftX(0) - 1), v(s.aisleFront + 0.12));
    g.lineTo(u(s.rackEnd), v(s.aisleFront + 0.12));
    g.stroke();

    g.strokeStyle = theme.paint;
    g.lineWidth = 0.06 * ppm;
    for (let dock = 1; dock <= s.docks; dock++) g.strokeRect(u(s.dockX(dock) - 1.2), v(s.front - 2.3), 2.4 * ppm, 2.1 * ppm);

    const r = s.reject;
    g.save();
    g.beginPath();
    g.rect(u(r.x - r.w / 2), v(r.z - r.d / 2), r.w * ppm, r.d * ppm);
    g.clip();
    g.strokeStyle = theme.hatch;
    g.lineWidth = 0.14 * ppm;
    for (let k = -r.d; k < r.w + r.d; k += 0.45) {
        g.beginPath();
        g.moveTo(u(r.x - r.w / 2 + k), v(r.z - r.d / 2));
        g.lineTo(u(r.x - r.w / 2 + k - r.d), v(r.z + r.d / 2));
        g.stroke();
    }
    g.restore();

    arrow(g, [[u(s.infeedX), v(s.front - 0.2)], [u(s.infeedX), v(s.beltEnd + 0.35)]], theme.inbound, ppm);
    for (let dock = 1; dock <= s.docks; dock++) {
        arrow(g, dispatchRoute(s, dock).map(([x, z]) => [u(x), v(z)]), theme.outbound, ppm);
    }

    g.fillStyle = theme.label;
    g.textAlign = "center";
    g.textBaseline = "middle";
    const font = size => `700 ${size * ppm}px system-ui, -apple-system, "Segoe UI", sans-serif`;
    g.font = font(0.42);
    for (let bay = 1; bay <= s.bays; bay++) g.fillText(String(bay), u(s.x(bay)), v(s.aisleFront + 0.62));
    g.font = font(0.36);
    g.fillText("GOODS IN", u(s.infeedX), v(s.front - 0.5));
    for (let dock = 1; dock <= s.docks; dock++) g.fillText(`DOCK ${dock}`, u(s.dockX(dock)), v(s.front - 0.5));
    g.fillText("REJECT", u(r.x), v(r.z + r.d / 2 + 0.35));
    g.font = font(0.3);
    for (let shaft = 0; shaft < s.shafts; shaft++) g.fillText(`V-${shaft + 1}`, u(s.shaftX(shaft)), v(s.aisleFront + 0.62));
    sideways(g, "INFEED · INTO STORE", u(s.infeedX - 0.75), v((s.beltStart + s.beltEnd) / 2), theme.inbound, font(0.28));
    sideways(g, "OUTFEED · TO DOCKS", u(s.outfeedX + 0.75), v((s.beltStart + s.beltEnd) / 2), theme.outbound, font(0.28));
    texture.needsUpdate = true;
}

export function dispatchRoute(site, dock) {
    const lane = site.front - 1;
    return [[site.outfeedX, site.beltEnd + 0.35], [site.outfeedX, lane], [site.dockX(dock), lane], [site.dockX(dock), site.front - 0.35]];
}

function arrow(g, points, color, ppm) {
    g.save();
    g.strokeStyle = color;
    g.fillStyle = color;
    g.globalAlpha = 0.75;
    g.lineWidth = 0.1 * ppm;
    g.setLineDash([0.32 * ppm, 0.22 * ppm]);
    g.beginPath();
    g.moveTo(...points[0]);
    for (const point of points.slice(1)) g.lineTo(...point);
    g.stroke();
    const [[x1, y1], [x2, y2]] = points.slice(-2);
    const angle = Math.atan2(y2 - y1, x2 - x1);
    const size = 0.34 * ppm;
    g.setLineDash([]);
    g.beginPath();
    g.moveTo(x2, y2);
    g.lineTo(x2 - size * Math.cos(angle - 0.5), y2 - size * Math.sin(angle - 0.5));
    g.lineTo(x2 - size * Math.cos(angle + 0.5), y2 - size * Math.sin(angle + 0.5));
    g.closePath();
    g.fill();
    g.restore();
}

function sideways(g, text, x, y, color, font) {
    g.save();
    g.translate(x, y);
    g.rotate(-Math.PI / 2);
    g.fillStyle = color;
    g.font = font;
    g.fillText(text, 0, 0);
    g.restore();
}

function buildRack(scene, mats, site) {
    const s = site;
    const posts = [];
    const beams = [];
    const rails = [];
    const span = s.rackEnd - s.rackStart;
    const middle = (s.rackStart + s.rackEnd) / 2;

    for (let bay = 0; bay <= s.bays; bay++) {
        const x = (bay + 0.5) * s.pitch;
        for (const z of [-0.05, -s.laneLength / 2, -s.laneLength + 0.05]) posts.push(at(x, s.top / 2, z, 0.12, s.top, 0.12));
        if (bay % 3 === 0 || bay === s.bays) posts.push(at(x, s.top / 2, s.aisleFront, 0.12, s.top, 0.12));
    }

    for (let level = 0; level <= s.levels; level++) {
        const y = level === s.levels ? s.top - 0.07 : level * s.storey + 0.07;
        beams.push(at(middle, y, -0.05, span, 0.14, 0.1), at(middle, y, -s.laneLength + 0.05, span, 0.14, 0.1));
        if (level > 0) beams.push(at(middle, y, s.aisleFront, span, 0.14, 0.1));
    }

    const aisleFrom = s.shaftX(0) - 0.7;
    for (let level = 0; level < s.levels; level++) {
        const y = level * s.storey + 0.11;
        for (const z of [s.aisleZ - 0.48, s.aisleZ + 0.48]) rails.push(at((aisleFrom + s.rackEnd) / 2, y, z, s.rackEnd - aisleFrom, 0.06, 0.07));
        for (let bay = 1; bay <= s.bays; bay++) {
            for (const side of [-0.44, 0.44]) rails.push(at(s.x(bay) + side, level * s.storey + 0.12, -s.laneLength / 2, 0.07, 0.08, s.laneLength));
        }
        if (level > 0) {
            for (let bay = 0; bay <= s.bays; bay += 3) rails.push(at((bay + 0.5) * s.pitch, level * s.storey + 0.06, s.aisleFront / 2, 0.1, 0.1, s.aisleFront));
        }
    }

    const chargers = range(0, s.levels - 1).map(level => at(-0.1, s.y(level) + 0.3, s.aisleZ - 0.66, 0.34, 0.2, 0.1));
    scene.add(instanced(posts, mats.post), instanced(beams, mats.beam), instanced(rails, mats.steel), instanced(chargers, mats.charger, false));
}

function buildShafts(scene, mats, site) {
    const s = site;
    const parts = [];
    for (let shaft = 0; shaft < s.shafts; shaft++) {
        const x = s.shaftX(shaft);
        for (const dx of [-0.7, 0.7]) {
            for (const dz of [-0.66, 0.66]) parts.push(at(x + dx, (s.top + 0.8) / 2, s.aisleZ + dz, 0.1, s.top + 0.8, 0.1));
        }
        parts.push(at(x, s.top + 0.8, s.aisleZ, 1.52, 0.16, 1.44), at(x, 0.04, s.aisleZ, 1.5, 0.08, 1.42));
    }
    scene.add(instanced(parts, mats.mast));
}

function buildBelts(scene, mats, site, theme, pickables) {
    const s = site;
    const belts = new Map();
    for (const belt of s.belts) {
        const x = s.beltX(belt.inbound);
        const z = (s.beltStart + s.beltEnd) / 2;
        const texture = new THREE.CanvasTexture(document.createElement("canvas"));
        texture.colorSpace = THREE.SRGBColorSpace;
        texture.wrapS = texture.wrapT = THREE.RepeatWrapping;
        texture.repeat.set(1, s.beltLength / BELT_TILE);
        texture.userData.metres = BELT_TILE;
        paintBelt(texture, theme, belt.inbound);
        const top = new THREE.Mesh(new THREE.PlaneGeometry(0.88, s.beltLength), new THREE.MeshLambertMaterial({ color: 0xffffff, map: texture }));
        top.rotation.x = -Math.PI / 2;
        top.position.set(x, s.beltTop - 0.005, z);
        top.receiveShadow = true;
        const group = new THREE.Group();
        group.add(solid(mats.belt, 0.98, s.beltTop - 0.01, s.beltLength, x, (s.beltTop - 0.01) / 2, z), top);
        group.userData.pick = { kind: "Belt", id: belt.id };
        belts.set(belt.id, { ...belt, group, top, texture, running: true, centre: new THREE.Vector3(x, s.beltTop, z) });
        pickables.push(group);
        scene.add(group);
    }

    const gateZ = s.beltZ(true, s.checkAt);
    const beacon = new THREE.Mesh(LED, new THREE.MeshBasicMaterial({ color: tones.info }));
    beacon.position.set(s.infeedX, 2.02, gateZ);
    scene.add(
        solid(mats.mast, 0.09, 1.9, 0.09, s.infeedX - 0.6, 0.95, gateZ),
        solid(mats.mast, 0.09, 1.9, 0.09, s.infeedX + 0.6, 0.95, gateZ),
        solid(mats.mast, 1.3, 0.12, 0.12, s.infeedX, 1.92, gateZ),
        beacon);

    const r = s.reject;
    const zone = solid(mats.hit, r.w, 0.6, r.d, r.x, 0.3, r.z);
    zone.castShadow = zone.receiveShadow = false;
    zone.userData.pick = { kind: "Reject", id: "" };
    pickables.push(zone);
    const rejects = [[-0.45, -0.6], [0.45, -0.6], [-0.45, 0.6], [0.45, 0.6]].map(([dx, dz]) => {
        const pallet = makePallet(mats);
        pallet.position.set(r.x + dx, 0, r.z + dz);
        pallet.scale.setScalar(0.82);
        pallet.visible = false;
        return pallet;
    });
    scene.add(zone, ...rejects);
    return { belts, rejects };
}

function buildDocks(scene, mats, site) {
    const s = site;
    const doors = [{ x: s.infeedX, cab: 0 }, ...range(1, s.docks).map(dock => ({ x: s.dockX(dock), cab: dock }))];
    for (const { x, cab } of doors) {
        scene.add(
            solid(mats.trim, 0.2, 3.7, 0.4, x - 1.42, 1.85, s.front),
            solid(mats.trim, 0.2, 3.7, 0.4, x + 1.42, 1.85, s.front),
            solid(mats.trim, 3.04, 0.4, 0.4, x, 3.9, s.front),
            solid(mats.door, 2.64, 0.55, 0.3, x, 3.42, s.front),
            solid(mats.dark, 2.3, 0.08, 1.1, x, 0.04, s.front + 0.55),
            truck(mats, s, x, mats.cabs[cab % mats.cabs.length]));
    }
}

function buildStock(scene, mats, site, pickables) {
    const s = site;
    const count = s.lanes * s.depth;
    const bases = blank(mats.wood, count);
    const loads = blank(mats.load, count);
    const color = new THREE.Color(cargo.fill);
    for (let i = 0; i < count; i++) loads.setColorAt(i, color);
    const hits = blank(mats.hit, s.lanes, false);
    const matrix = new THREE.Matrix4();
    for (let level = 0; level < s.levels; level++) {
        for (let bay = 1; bay <= s.bays; bay++) {
            matrix.compose(
                new THREE.Vector3(s.x(bay), level * s.storey + s.storey / 2 + 0.08, -s.laneLength / 2),
                UPRIGHT,
                new THREE.Vector3(s.pitch - 0.08, s.storey - 0.1, s.laneLength));
            hits.setMatrixAt(s.laneIndex(level, bay), matrix);
        }
    }
    hits.instanceMatrix.needsUpdate = true;
    hits.computeBoundingSphere();
    hits.userData.pick = { kind: "Lane" };
    pickables.push(hits);
    scene.add(bases, loads, hits);
    return { bases, loads, hits };
}

function truck(mats, site, x, cab) {
    const z = site.front + 0.75;
    const group = new THREE.Group();
    group.add(
        solid(mats.trailer, 2.4, 2.8, 7.2, x, 2.0, z + 3.6),
        solid(mats.stripe, 2.44, 0.24, 6.9, x, 1.35, z + 3.6),
        solid(mats.dark, 2.2, 0.22, 7.0, x, 0.6, z + 3.6),
        solid(cab, 2.4, 2.3, 1.9, x, 1.75, z + 8.35),
        solid(mats.glass, 2.16, 0.78, 0.06, x, 2.3, z + 9.31));
    for (const wz of [z + 1.1, z + 2.1, z + 8.1]) {
        for (const side of [-1.06, 1.06]) {
            const wheel = new THREE.Mesh(WHEEL, mats.dark);
            wheel.position.set(x + side, 0.45, wz);
            wheel.castShadow = true;
            group.add(wheel);
        }
    }
    return group;
}

function tree(mats, x, z, scale) {
    const group = new THREE.Group();
    const trunk = new THREE.Mesh(TRUNK, mats.trunk);
    trunk.position.y = 0.6;
    const crown = new THREE.Mesh(CROWN, mats.leaf);
    crown.position.y = 2.6;
    trunk.castShadow = crown.castShadow = true;
    group.add(trunk, crown);
    group.position.set(x, 0, z);
    group.scale.setScalar(scale);
    return group;
}

export function makePallet(mats) {
    const group = new THREE.Group();
    group.add(solid(mats.wood, 1.0, 0.14, 1.05, 0, 0.07, 0), solid(mats.cardboard, 0.92, 0.95, 0.98, 0, 0.615, 0));
    return group;
}

export function makeTaxi(mats) {
    const group = new THREE.Group();
    const led = new THREE.Mesh(LED, new THREE.MeshBasicMaterial({ color: tones.neutral }));
    led.position.set(0.53, 0.28, 0.42);
    const carried = makePallet(mats);
    carried.position.y = 0.03;
    const satellite = new THREE.Group();
    satellite.add(solid(mats.dark, 0.84, 0.05, 0.96, 0, 0, 0), carried);
    satellite.position.y = 0.2;
    group.add(
        solid(mats.dark, 1.2, 0.07, 1.1, 0, 0.035, 0),
        solid(mats.machine, 0.14, 0.17, 1.08, -0.53, 0.155, 0),
        solid(mats.machine, 0.14, 0.17, 1.08, 0.53, 0.155, 0),
        led,
        satellite);
    return { group, led, satellite, carried };
}

export function makeLift(mats) {
    const group = new THREE.Group();
    const led = new THREE.Mesh(LED, new THREE.MeshBasicMaterial({ color: tones.neutral }));
    led.position.set(0.66, 0.48, 0.5);
    group.add(
        solid(mats.steel, 1.36, 0.1, 1.28, 0, -0.05, 0),
        solid(mats.machine, 0.06, 0.44, 1.28, -0.66, 0.22, 0),
        solid(mats.machine, 0.06, 0.44, 1.28, 0.66, 0.22, 0),
        led);
    return { group, led };
}
