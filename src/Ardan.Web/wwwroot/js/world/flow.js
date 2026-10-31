import * as THREE from "https://cdn.jsdelivr.net/npm/three@0.170.0/build/three.module.min.js";
import { BOX } from "./site.js";

const SPACING = 0.9;
const SPEED = 1.5;
const PER_ROUTE = 48;
const SIZE = 1.7;
const RIGHT = new THREE.Vector3(1, 0, 0);

export const CHEVRON = (() => {
    const shape = new THREE.Shape();
    shape.moveTo(0.22, 0);
    shape.lineTo(-0.1, 0.18);
    shape.lineTo(-0.02, 0);
    shape.lineTo(-0.1, -0.18);
    shape.closePath();
    return new THREE.ShapeGeometry(shape).rotateX(-Math.PI / 2);
})();

// Where each Taxi is heading: arrows that flow along its path and a frame on the place it is going to.
export class Routes {
    constructor(scene, count) {
        this.arrows = new THREE.InstancedMesh(
            CHEVRON,
            new THREE.MeshBasicMaterial({ side: THREE.DoubleSide, transparent: true, opacity: 0.95, depthWrite: false }),
            count * PER_ROUTE);
        this.arrows.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
        this.arrows.frustumCulled = false;
        this.arrows.renderOrder = 4;
        const white = new THREE.Color(0xffffff);
        for (let i = 0; i < count * PER_ROUTE; i++) this.arrows.setColorAt(i, white);
        this.arrows.count = 0;

        this.frames = Array.from({ length: count }, () => {
            const line = new THREE.LineSegments(new THREE.EdgesGeometry(BOX), new THREE.LineBasicMaterial({ transparent: true, depthTest: false }));
            const fill = new THREE.Mesh(BOX, new THREE.MeshBasicMaterial({ transparent: true, depthWrite: false }));
            line.renderOrder = fill.renderOrder = 6;
            line.visible = fill.visible = false;
            scene.add(line, fill);
            return { line, fill };
        });
        scene.add(this.arrows);

        this.matrix = new THREE.Matrix4();
        this.rotation = new THREE.Quaternion();
        this.point = new THREE.Vector3();
        this.direction = new THREE.Vector3();
        this.scale = new THREE.Vector3(SIZE, SIZE, SIZE);
        this.color = new THREE.Color();
    }

    draw(routes, time) {
        const phase = (time / 1000 * SPEED) % SPACING;
        const pulse = 0.55 + Math.sin(time / 240) * 0.35;
        let count = 0;
        routes.forEach((route, index) => {
            this.color.setHex(route.color);
            count = this.lay(route.points, phase, count, count + PER_ROUTE);
            const { line, fill } = this.frames[index];
            line.visible = fill.visible = !!route.frame;
            if (!route.frame) return;
            for (const mesh of [line, fill]) {
                mesh.position.copy(route.frame.position);
                mesh.scale.copy(route.frame.size);
                mesh.material.color.setHex(route.color);
            }
            line.material.opacity = pulse;
            fill.material.opacity = 0.14 + pulse * 0.16;
        });
        for (let index = routes.length; index < this.frames.length; index++) this.frames[index].line.visible = this.frames[index].fill.visible = false;
        this.arrows.count = count;
        this.arrows.instanceMatrix.needsUpdate = true;
        this.arrows.instanceColor.needsUpdate = true;
    }

    lay(points, phase, count, limit) {
        let travelled = 0;
        let next = phase;
        for (let i = 1; i < points.length && count < limit; i++) {
            const a = points[i - 1];
            const length = a.distanceTo(points[i]);
            if (length < 0.001) continue;
            this.direction.subVectors(points[i], a).divideScalar(length);
            this.rotation.setFromUnitVectors(RIGHT, this.direction);
            for (; next <= travelled + length && count < limit; next += SPACING) {
                this.point.copy(a).addScaledVector(this.direction, next - travelled);
                this.arrows.setMatrixAt(count, this.matrix.compose(this.point, this.rotation, this.scale));
                this.arrows.setColorAt(count, this.color);
                count++;
            }
            travelled += length;
        }
        return count;
    }

}
