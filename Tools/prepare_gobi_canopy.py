"""Derive a sealed, hollow-underneath shelter cap from the prepared overhang.
Run with Blender --background --python Tools/prepare_gobi_canopy.py.
The library model and its input archive are preserved.
"""
import importlib.util
from pathlib import Path

import bpy
import bmesh

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("environment_meshes", ROOT / "Tools/prepare_environment_meshes.py")
pipeline = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pipeline)
output = ROOT / "ArtSource/Environment/GobiDressing"
output.mkdir(parents=True, exist_ok=True)

for level in range(3):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    obj = pipeline.read_mesh(pipeline.OUTPUT / "RockOverhang" / f"LOD{level}.meshbin")
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.0002)
    bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                          plane_co=(0, 0, 9), plane_no=(0, 0, 1), dist=.00001,
                          clear_inner=True, clear_outer=False)
    edges = [e for e in bm.edges if e.is_boundary and all(abs(v.co.z - 9) < .002 for v in e.verts)]
    caps = bmesh.ops.holes_fill(bm, edges=edges, sides=0)["faces"]
    for face in caps:
        face.material_index = 0
        face.smooth = False
    for vertex in bm.verts:
        vertex.co.z -= 9
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    pipeline.write_mesh(obj, output / f"Canopy_LOD{level}.meshbin")
    if level == 0:
        bpy.ops.wm.save_as_mainfile(filepath=str(output / "ShelterCanopy.blend"))
print("GOBI_CANOPY_READY", output)
