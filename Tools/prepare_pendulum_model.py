"""Prepare the Chaoge bronze pendulum bob with Blender in background mode."""

from pathlib import Path
import json

import bmesh
import bpy
from mathutils import Vector
import numpy as np


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "ArtSource/Pendulum/2026-11-03/BronzePendulumBob_PBR.fbx"
OUTPUT = ROOT / "Assets/_Project/Art/Pendulum"
BASE_CUT = 0.153
HEIGHT_METRES = 4.6
TEXTURE_SIZE = 1024
LOD_TRIANGLES = (16000, 4000, 1000)


def trim_and_scale(obj):
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    bottom = min(v.co.z for v in mesh.verts)
    top = max(v.co.z for v in mesh.verts)
    cut = bottom + (top - bottom) * BASE_CUT
    result = bmesh.ops.bisect_plane(
        mesh,
        geom=[*mesh.verts, *mesh.edges, *mesh.faces],
        dist=1e-6,
        plane_co=(0, 0, cut),
        plane_no=(0, 0, 1),
        clear_inner=True,
    )
    cut_edges = [edge for edge in result["geom_cut"] if isinstance(edge, bmesh.types.BMEdge) and edge.is_boundary]
    if not cut_edges:
        raise ValueError("The display base cut did not produce a boundary.")
    uv_layer = mesh.loops.layers.uv.active
    border_uv = [loop[uv_layer].uv.copy() for edge in cut_edges for loop in edge.link_loops]
    sample_uv = sum(border_uv, Vector((0, 0))) / len(border_uv)
    caps = bmesh.ops.holes_fill(mesh, edges=cut_edges, sides=0)["faces"]
    if len(caps) != 1:
        raise ValueError(f"Expected one closed cap after trimming the base, got {len(caps)}.")
    for face in caps:
        face.smooth = False
        for loop in face.loops:
            loop[uv_layer].uv = sample_uv + Vector((loop.vert.co.x, loop.vert.co.y)) * 0.025

    hanger = [v.co for v in mesh.verts if v.co.z > top - (top - cut) * 0.14]
    center = Vector((
        (min(p.x for p in hanger) + max(p.x for p in hanger)) * 0.5,
        (min(p.y for p in hanger) + max(p.y for p in hanger)) * 0.5,
        top,
    ))
    scale = HEIGHT_METRES / (top - cut)
    for vert in mesh.verts:
        vert.co = (vert.co - center) * scale
    bmesh.ops.triangulate(mesh, faces=list(mesh.faces))
    bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
    mesh.to_mesh(obj.data)
    mesh.free()


def export_lods(obj):
    counts = []
    for level, budget in enumerate(LOD_TRIANGLES):
        lod = obj.copy()
        lod.data = obj.data.copy()
        lod.name = f"BronzePendulumBob_LOD{level}"
        bpy.context.collection.objects.link(lod)
        bpy.ops.object.select_all(action="DESELECT")
        lod.select_set(True)
        bpy.context.view_layer.objects.active = lod
        decimate = lod.modifiers.new("LOD triangles", "DECIMATE")
        decimate.ratio = budget / len(lod.data.polygons)
        decimate.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=decimate.name)
        counts.append(len(lod.data.polygons))
        lod.data.materials.clear()
        bpy.ops.export_scene.fbx(
            filepath=str(OUTPUT / "Meshes" / f"{lod.name}.fbx"),
            use_selection=True,
            object_types={"MESH"},
            axis_forward="-Z",
            axis_up="Y",
            apply_scale_options="FBX_SCALE_UNITS",
            mesh_smooth_type="FACE",
        )
        bpy.data.objects.remove(lod, do_unlink=True)
    return counts


def write_png(image, name):
    image.filepath_raw = str(OUTPUT / "Textures" / f"{name}.png")
    image.file_format = "PNG"
    image.save()


def export_textures():
    images = {image.name.rsplit(".", 1)[0].removeprefix("texture_pbr_20250901"): image for image in bpy.data.images}
    for suffix in ("", "_normal", "_metallic", "_roughness"):
        images[suffix].scale(TEXTURE_SIZE, TEXTURE_SIZE)
    write_png(images[""], "BronzePendulumAlbedo")
    write_png(images["_normal"], "BronzePendulumNormal")

    metal = np.empty(TEXTURE_SIZE * TEXTURE_SIZE * 4, dtype=np.float32)
    rough = np.empty_like(metal)
    images["_metallic"].pixels.foreach_get(metal)
    images["_roughness"].pixels.foreach_get(rough)
    packed = np.zeros_like(metal)
    packed[::4] = metal[::4]
    packed[3::4] = 1 - rough[::4]
    image = bpy.data.images.new("Metallic Smoothness", TEXTURE_SIZE, TEXTURE_SIZE, alpha=True)
    image.colorspace_settings.name = "Non-Color"
    image.pixels.foreach_set(packed)
    write_png(image, "BronzePendulumMetallicSmoothness")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.ops.import_scene.fbx(filepath=str(SOURCE))
    obj, = [obj for obj in bpy.context.selected_objects if obj.type == "MESH"]
    bpy.context.view_layer.objects.active = obj
    for folder in ("Meshes", "Textures"):
        (OUTPUT / folder).mkdir(parents=True, exist_ok=True)
    trim_and_scale(obj)
    export_textures()
    print(json.dumps({"triangles": export_lods(obj), "height_metres": HEIGHT_METRES, "base_cut": BASE_CUT}))


if __name__ == "__main__":
    main()
