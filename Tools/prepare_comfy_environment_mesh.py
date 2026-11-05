"""Reduce a local ComfyUI GLB to two silhouette meshes for Unity backdrops."""

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
import trimesh


def sealed_lods(model, budgets):
    from scipy import ndimage

    pitch = float(max(model.extents)) / 128
    grid = model.voxelized(pitch)
    volume = np.pad(grid.matrix, 4)
    volume = ndimage.binary_closing(volume, iterations=3)
    # Agreeing spans on two axes close the interior of a rock even if the
    # source has an open seam that would leak in a flood fill.
    spans = []
    for axis in range(3):
        before = np.maximum.accumulate(volume, axis=axis)
        after = np.flip(np.maximum.accumulate(np.flip(volume, axis=axis), axis=axis), axis=axis)
        spans.append(before & after)
    x, y, z = spans
    volume = ndimage.binary_fill_holes(volume | (x & y) | (x & z) | (y & z)).astype(np.float32)
    origin = grid.translation - 4 * pitch
    result = []
    for budget in budgets:
        resolution = 40
        while resolution >= 12:
            coarse = ndimage.zoom(volume, resolution / max(volume.shape), order=1)
            mesh = trimesh.voxel.ops.matrix_to_marching_cubes(coarse >= .5)
            mesh.vertices *= (np.array(volume.shape) - 1) / (np.array(coarse.shape) - 1) * pitch
            mesh.apply_translation(origin)
            if len(mesh.faces) <= budget:
                mesh = max(mesh.split(only_watertight=True), key=lambda item: item.area)
                trimesh.smoothing.filter_taubin(mesh, iterations=6)
                result.append(mesh)
                break
            resolution = min(resolution - 1, int(resolution * np.sqrt(budget / len(mesh.faces))))
        else:
            raise ValueError("A sealed surface could not meet the triangle budget.")
    return result


def terrain_lods(model, budgets):
    """Fit a closed height field to the generated ridge, with a buried apron."""
    from scipy import ndimage

    lower, upper = model.bounds
    width, height, depth = model.extents
    footprint = np.array([width * 1.22, depth * 1.5])
    center = model.bounds.mean(axis=0)[[0, 2]]
    result = []
    for budget in budgets:
        columns = max(8, int(np.sqrt(budget * .40 * footprint[0] / footprint[1])))
        rows = max(8, int(columns * footprint[1] / footprint[0]))
        while 2 * (columns - 1) * (rows - 1) + 6 * (columns + rows - 2) > budget:
            columns -= 1
        sampled = np.full((rows, columns), -np.inf)
        unit = (model.vertices[:, [0, 2]] - center) / footprint + .5
        cell = np.rint(unit * [columns - 1, rows - 1]).astype(int)
        np.maximum.at(sampled, (cell[:, 1], cell[:, 0]), model.vertices[:, 1] - lower[1])
        missing = ~np.isfinite(sampled)
        nearest = ndimage.distance_transform_edt(missing, return_distances=False, return_indices=True)
        sampled[missing] = sampled[tuple(nearest[:, missing])]
        sampled = ndimage.gaussian_filter(sampled, .65)
        u, v = np.meshgrid(np.linspace(0, 1, columns), np.linspace(0, 1, rows))
        apron = np.minimum.reduce((u / .14, (1 - u) / .14, v / .20, (1 - v) / .20))
        apron = np.clip(apron, 0, 1)
        apron = apron * apron * (3 - 2 * apron)
        sampled *= apron
        sampled *= height / sampled.max()
        vertices = np.column_stack(((center[0] + (u - .5) * footprint[0]).ravel(),
                                    sampled.ravel(),
                                    (center[1] + (v - .5) * footprint[1]).ravel())).tolist()
        faces = []
        for row in range(rows - 1):
            for col in range(columns - 1):
                a = row * columns + col
                faces.extend(((a, a + columns + 1, a + 1), (a, a + columns, a + columns + 1)))
        edge = list(range(columns)) + [row * columns + columns - 1 for row in range(1, rows)]
        edge += [((rows - 1) * columns + col) for col in range(columns - 2, -1, -1)]
        edge += [row * columns for row in range(rows - 2, 0, -1)]
        back = len(vertices)
        vertices.extend((vertices[i][0], -height * .025, vertices[i][2]) for i in edge)
        base = len(vertices)
        vertices.append([center[0], -height * .025, center[1]])
        for i, a in enumerate(edge):
            j = (i + 1) % len(edge)
            faces.extend(((a, edge[j], back + j), (a, back + j, back + i), (base, back + i, back + j)))
        mesh = trimesh.Trimesh(vertices=vertices, faces=faces)
        mesh.fix_normals()
        result.append(mesh)
    return result


def cast_plaque(model, budget, front):
    """Bake a shallow, continuous metal relief from the generated face depth."""
    from scipy import ndimage

    width, height, thickness = model.extents
    if not .25 <= width / height <= 4:
        raise ValueError("The face aspect ratio does not describe a plaque.")
    columns = max(5, int(np.sqrt(budget * .4 * width / height)))
    rows = max(5, int(columns * height / width))
    while 2 * (columns - 1) * (rows - 1) + 6 * (columns + rows - 2) > budget:
        rows -= 1
    lower, upper = model.bounds
    xy = model.vertices[:, :2]
    cell = np.rint((xy - lower[:2]) / model.extents[:2] * [columns - 1, rows - 1]).astype(int)
    relief = np.full((rows, columns), -np.inf)
    np.maximum.at(relief, (cell[:, 1], cell[:, 0]), model.vertices[:, 2] * front)
    missing = ~np.isfinite(relief)
    if missing.mean() > .45:
        raise ValueError("The source is too sparse to form a continuous cast plaque.")
    nearest = ndimage.distance_transform_edt(missing, return_distances=False, return_indices=True)
    relief[missing] = relief[tuple(nearest[:, missing])]
    lo, hi = np.percentile(relief, [5, 97])
    if hi - lo < thickness * .02:
        raise ValueError("The source face has no discernible relief.")
    height_map = ndimage.gaussian_filter(np.clip((relief - lo) / (hi - lo), 0, 1), .48)
    x, y = np.meshgrid(np.linspace(lower[0], upper[0], columns), np.linspace(lower[1], upper[1], rows))
    vertices = np.column_stack((x.ravel(), y.ravel(), ((.10 + .40 * height_map) * thickness * front).ravel())).tolist()
    faces = []
    for row in range(rows - 1):
        for col in range(columns - 1):
            a = row * columns + col
            faces.extend(((a, a + 1, a + columns + 1), (a, a + columns + 1, a + columns)))
    edge = list(range(columns)) + [row * columns + columns - 1 for row in range(1, rows)]
    edge += [((rows - 1) * columns + col) for col in range(columns - 2, -1, -1)]
    edge += [row * columns for row in range(rows - 2, 0, -1)]
    back = len(vertices)
    back_z = -.51 * thickness * front
    vertices.extend((vertices[i][0], vertices[i][1], back_z) for i in edge)
    centre = len(vertices)
    vertices.append([0, (lower[1] + upper[1]) * .5, back_z])
    for i, a in enumerate(edge):
        j = (i + 1) % len(edge)
        faces.extend(((a, back + i, back + j), (a, back + j, edge[j]), (centre, back + j, back + i)))
    mesh = trimesh.Trimesh(vertices=vertices, faces=faces)
    mesh.fix_normals()
    if not mesh.is_watertight:
        raise ValueError("The plaque is not closed.")
    return mesh


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output_stem", type=Path)
    parser.add_argument("--triangles", type=int, nargs=2, default=(2100, 1200))
    repair = parser.add_mutually_exclusive_group()
    repair.add_argument("--seal", action="store_true", help="Close thin holes before simplification.")
    repair.add_argument("--plaque-front", choices=("positive", "negative"), help="Build a backed relief on the selected Z face.")
    repair.add_argument("--terrain", action="store_true", help="Repair a ridge as a closed terrain surface with a sloping apron.")
    args = parser.parse_args()
    high, low = args.triangles
    if high <= low or low < 32:
        parser.error("Provide descending triangle budgets, each at least 32.")
    if args.plaque_front and low < 128:
        parser.error("A plaque needs at least 128 triangles in each tier.")
    if args.terrain and low < 256:
        parser.error("A terrain surface needs at least 256 triangles in each tier.")

    model = trimesh.load_scene(args.source).to_mesh()
    model.update_faces(model.nondegenerate_faces() & model.unique_faces())
    model.remove_unreferenced_vertices()
    if not len(model.faces) or not np.isfinite(model.vertices).all():
        raise ValueError("The generated GLB has no usable surface.")
    islands = sorted(model.split(only_watertight=False), key=lambda island: island.area, reverse=True)
    model = trimesh.util.concatenate([island for island in islands if island.area >= islands[0].area * .045])
    model.fix_normals()
    if model.extents[2] > model.extents[0]:
        model.apply_transform(trimesh.transformations.rotation_matrix(np.pi / 2, [0, 1, 0]))
    model.apply_translation([-model.bounds.mean(axis=0)[0], -model.bounds[0, 1], -model.bounds.mean(axis=0)[2]])
    if np.min(model.extents) < 1e-4:
        raise ValueError("The generated mesh has a collapsed axis.")

    digest = hashlib.sha256(args.source.read_bytes()).hexdigest()
    report = {"source": args.source.name, "sha256": digest, "triangles": len(model.faces), "lods": []}
    if len(model.faces) <= high:
        raise ValueError("Source does not contain enough detail for the requested budgets.")
    if args.seal:
        surfaces = sealed_lods(model, args.triangles)
    elif args.terrain:
        surfaces = terrain_lods(model, args.triangles)
    elif args.plaque_front:
        front = 1 if args.plaque_front == "positive" else -1
        surfaces = [cast_plaque(model, budget, front) for budget in args.triangles]
    else:
        surfaces = [model.simplify_quadric_decimation(face_count=budget, aggression=9) for budget in args.triangles]
    lods = []
    for index, (budget, reduced) in enumerate(zip(args.triangles, surfaces)):
        if len(reduced.faces) > budget or np.min(reduced.extents / model.extents) < .8:
            raise ValueError(f"LOD {index} lost its silhouette or exceeded its budget.")
        if (args.seal or args.terrain) and not reduced.is_watertight:
            raise ValueError(f"LOD {index} reopened the sealed surface.")
        destination = args.output_stem.with_name(f"{args.output_stem.name}_LOD{index}.obj")
        lods.append((destination, reduced))
    args.output_stem.parent.mkdir(parents=True, exist_ok=True)
    for destination, reduced in lods:
        destination.write_text(trimesh.exchange.obj.export_obj(reduced, include_normals=True, include_texture=False,
                                                             digits=6, header=f"Source SHA-256 {digest}"))
        report["lods"].append({"triangles": len(reduced.faces), "extents": reduced.extents.tolist(), "bytes": destination.stat().st_size})
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
