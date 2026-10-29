"""Blender batch mesh repair. Input archives stay immutable; outputs are metre-sized.

Run with Blender --background --python Tools/prepare_environment_meshes.py -- [IDs].
TBM1 is deliberately simple and keeps this workflow independent of FBX axis rules.
"""
import json
import math
import struct
import sys
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector, noise

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / "ArtSource/Environment/Prepared/2026-10-28"
OUTPUT = WORK / "Output"
MATERIALS = ["Rock", "Masonry", "Earth", "Wood", "Thatch", "RoofTile", "Bark", "OriginalSupplies"]
SLOTS = {name: i for i, name in enumerate(MATERIALS)}
SOURCES = {e["id"]: e for e in json.loads((WORK / "sources.json").read_text())["assets"]}
NATURAL = {"GobiMountainPeak", "GobiRidge", "WindErodedCliff", "YardangMesa", "GobiRockCluster", "RockOverhang"}
ROOFS = {"ShangDwelling": .61, "ShangWorkshop": .56, "ShangGranary": .55, "ShangMarketStall": .62, "RammedEarthGatehouse": .68}
BUDGETS = {
    "GobiMountainPeak": (110000, 25000, 5000), "GobiRidge": (130000, 30000, 6000),
    "WindErodedCliff": (85000, 20000, 4500), "YardangMesa": (75000, 18000, 4000),
    "GobiRockCluster": (32000, 8000, 1800), "RockOverhang": (80000, 18000, 4000),
    "DeadTamarisk": (32000, 8000, 1800), "DesertThornBush": (18000, 5000, 1200),
    "GobiWaystone": (6500, 1800, 450), "CaravanSupplies": (36000, 10000, 2400),
    "ShangDwelling": (65000, 16000, 3600), "ShangWorkshop": (70000, 18000, 4000),
    "ShangGranary": (30000, 8000, 1800), "ShangMarketStall": (26000, 7000, 1600),
    "RammedEarthGatehouse": (80000, 20000, 4500), "PyramidPortal": (30000, 8000, 1800),
    "PyramidStonePanel": (20000, 5500, 1200), "MedievalTownhouse": (75000, 18000, 4000),
    "GothicWindowWall": (32000, 8000, 1800), "MedievalButtress": (24000, 6500, 1500),
}


def read_mesh(path):
    with path.open("rb") as f:
        magic, nv, ni, ns = struct.unpack("<4siii", f.read(16))
        assert magic == b"TBM1"
        v = np.fromfile(f, "<f4", nv * 3).reshape(-1, 3)
        n = np.fromfile(f, "<f4", nv * 3).reshape(-1, 3)
        uv = np.fromfile(f, "<f4", nv * 2).reshape(-1, 2)
        faces = np.fromfile(f, "<i4", ni).reshape(-1, 3)
        slots = np.fromfile(f, "<i4", ni // 3)
    mesh = bpy.data.meshes.new(path.stem)
    mesh.from_pydata(v.tolist(), [], faces.tolist())
    for face in mesh.polygons: face.use_smooth = True
    layer = mesh.uv_layers.new(name="UVMap")
    loop_vertices = np.empty(len(mesh.loops), dtype=np.int32)
    mesh.loops.foreach_get("vertex_index", loop_vertices)
    layer.data.foreach_set("uv", uv[loop_vertices].ravel())
    mesh.polygons.foreach_set("material_index", slots)
    obj = bpy.data.objects.new(path.stem, mesh)
    bpy.context.collection.objects.link(obj)
    for name in MATERIALS:
        material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        material.diffuse_color = {"Rock": (.34, .29, .22, 1), "Wood": (.18, .095, .04, 1),
                                 "Thatch": (.32, .24, .12, 1), "Earth": (.48, .36, .24, 1)}.get(name, (.4, .4, .37, 1))
        mesh.materials.append(material)
    return obj


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def bounds(obj):
    p = np.empty(len(obj.data.vertices) * 3, dtype=np.float32)
    obj.data.vertices.foreach_get("co", p)
    p = p.reshape(-1, 3)
    return p.min(0), p.max(0)


def flatten(obj):
    obj.data.transform(obj.matrix_world)
    obj.matrix_world = Matrix.Identity(4)


def normalize(obj, height=None, width=None):
    flatten(obj)
    lo, hi = bounds(obj)
    scale = height / (hi[2] - lo[2]) if height else width / (hi[0] - lo[0]) if width else 1
    centre = Vector(((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, lo[2]))
    for v in obj.data.vertices:
        v.co = (v.co - centre) * scale
    obj.data.update()


def weld_and_crop(obj, bottom=0, top=None):
    lo, hi = bounds(obj)
    height = hi[2] - lo[2]
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=max(height * .000015, .00001))
    cuts = [(lo[2] + bottom * height, True)] if bottom else []
    if top is not None: cuts.append((lo[2] + top * height, False))
    for z, below in cuts:
        bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                              plane_co=(0, 0, z), plane_no=(0, 0, 1), dist=height * .000001,
                              clear_inner=below, clear_outer=not below)
        edges = [e for e in bm.edges if e.is_boundary and all(abs(v.co.z - z) < height * .0001 for v in e.verts)]
        if edges: bmesh.ops.holes_fill(bm, edges=edges, sides=0)
    bm.normal_update(); bm.to_mesh(obj.data); bm.free(); obj.data.update()


def assign_surface(obj, entry):
    identifier = entry["id"]
    if len(obj.data.materials) != len(MATERIALS):
        obj.data.materials.clear()
        for name in MATERIALS: obj.data.materials.append(bpy.data.materials[name])
    if identifier in NATURAL:
        for p in obj.data.polygons: p.material_index = SLOTS["Rock"]
        return
    if identifier == "GobiWaystone":
        for p in obj.data.polygons: p.material_index = SLOTS["Rock"]
        return
    if identifier in {"PyramidPortal", "PyramidStonePanel", "GothicWindowWall", "MedievalButtress"}:
        for p in obj.data.polygons: p.material_index = SLOTS["Masonry"]
        return
    if identifier in {"DeadTamarisk", "DesertThornBush"}:
        for p in obj.data.polygons: p.material_index = SLOTS["Bark"]
        return
    if identifier == "CaravanSupplies":
        for p in obj.data.polygons: p.material_index = SLOTS["OriginalSupplies"]
        return
    image = bpy.data.images.load(entry["albedo"], check_existing=False)
    image.colorspace_settings.name = "Non-Color"
    rgba = np.empty(image.size[0] * image.size[1] * 4, dtype=np.float32)
    image.pixels.foreach_get(rgba); rgba = rgba.reshape(image.size[1], image.size[0], 4)
    uv = obj.data.uv_layers.active.data
    lo, hi = bounds(obj); height = hi[2] - lo[2]
    for face in obj.data.polygons:
        mean = sum((uv[i].uv for i in face.loop_indices), Vector((0, 0))) / len(face.loop_indices)
        r, g, b = rgba[min(int(mean.y * image.size[1]), image.size[1] - 1) % image.size[1],
                       min(int(mean.x * image.size[0]), image.size[0] - 1) % image.size[0], :3]
        z = (face.center.z - lo[2]) / height
        dark = max(r, g, b) < .47
        timber = dark or (r > g * 1.22 and g > b * 1.12 and r < .69)
        green_trim = g > r * .94 and g > b * 1.08
        if identifier == "MedievalTownhouse":
            category = "Masonry" if z < .34 else "RoofTile" if z > .48 and abs(face.normal.z) > .25 else "Wood" if timber else "Earth"
        elif identifier == "ShangGranary" and z < .27:
            category = "Wood"
        else:
            category = "Masonry" if z < .13 else "Wood" if timber or green_trim else "Earth"
        face.material_index = SLOTS[category]
    bpy.data.images.remove(image)


def weather_rock(obj, seed):
    lo, hi = bounds(obj); h = hi[2] - lo[2]
    bm = bmesh.new(); bm.from_mesh(obj.data)
    movable = [v for v in bm.verts if v.co.z > lo[2] + .003 * h]
    for _ in range(2): bmesh.ops.smooth_vert(bm, verts=movable, factor=.18, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.normal_update()
    for v in movable:
        p = v.co / h
        amount = (noise.noise(p * 7 + Vector((seed, 17, 39))) * .7 + noise.noise(p * 37 + Vector((12, seed, 5))) * .3) * h * .025
        v.co += Vector((v.normal.x, v.normal.y, v.normal.z * .45)) * amount
    bm.normal_update(); bm.to_mesh(obj.data); bm.free()
    for face in obj.data.polygons: face.use_smooth = True


def remove_region(obj, predicate):
    bm = bmesh.new(); bm.from_mesh(obj.data)
    doomed = [f for f in bm.faces if predicate(f.calc_center_median())]
    cut_vertices = {v for f in doomed for v in f.verts}
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    boundary = [e for e in bm.edges if e.is_boundary and all(v in cut_vertices for v in e.verts)]
    if boundary: bmesh.ops.holes_fill(bm, edges=boundary, sides=0)
    bm.normal_update(); bm.to_mesh(obj.data); bm.free()
    return len(doomed)


def join(objects, name):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = objects[0]; obj.name = name; flatten(obj)
    return obj


def cylinder_between(a, b, radius, material="Wood", sides=16):
    a, b = Vector(a), Vector(b)
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=(b-a).length, location=(a+b)/2)
    obj = bpy.context.object
    obj.rotation_euler = (b-a).to_track_quat("Z", "Y").to_euler()
    obj.data.materials.append(bpy.data.materials[material])
    for p in obj.data.polygons: p.use_smooth = len(p.vertices) == 4
    return obj


def roof_for(obj, entry):
    lo, hi = bounds(obj); w, d = hi[0] - lo[0], hi[1] - lo[1]
    base = hi[2] - .04
    rise = max(.5, entry["height"] - base)
    w *= 1.08; d *= 1.11
    vertices, faces = [], []
    nx, ny = 42, 28
    for i in range(nx+1):
        x = (i/nx-.5) * w
        for j in range(ny+1):
            y = (j/ny-.5) * d
            z = base + rise * (1-abs(y)/(d*.5))
            z += .025 * noise.noise(Vector((x*4, y*2, 31)))
            vertices.append((x, y, z))
            if i < nx and j < ny:
                a = i*(ny+1)+j
                faces.append((a, a+ny+1, a+ny+2, a+1))
    mesh = bpy.data.meshes.new("Straight thatch roof"); mesh.from_pydata(vertices, [], faces); mesh.update()
    roof = bpy.data.objects.new("Straight thatch roof", mesh); bpy.context.collection.objects.link(roof)
    mesh.materials.append(bpy.data.materials["Thatch"])
    uv = mesh.uv_layers.new()
    for p in mesh.polygons:
        for loop in p.loop_indices:
            point = mesh.vertices[mesh.loops[loop].vertex_index].co
            uv.data[loop].uv = (point.x / 1.5, point.y / 1.5)
    activate(roof)
    solid = roof.modifiers.new("Thatch thickness", "SOLIDIFY"); solid.thickness = .18 if w < 8 else .28
    bpy.ops.object.modifier_apply(modifier=solid.name)
    parts = [obj, roof]
    radius = .065 if w < 8 else .11
    for y in (-d*.5, d*.5): parts.append(cylinder_between((-w*.5,y,base-.06),(w*.5,y,base-.06),radius))
    parts.append(cylinder_between((-w*.49,0,base+rise),(w*.49,0,base+rise),radius*.8))
    for x in (-w*.46,w*.46):
        for sign in (-1,1): parts.append(cylinder_between((x,0,base+rise-.13),(x,sign*d*.5,base-.10),radius*.75))
    return join(parts, entry["id"])


def carve_portal(obj, width, height):
    bm=bmesh.new();bm.from_mesh(obj.data)
    planes=[((width/2,0,0),(1,0,0)),((-width/2,0,0),(1,0,0)),((0,0,height),(0,0,1))]
    for point,normal in planes:
        bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),plane_co=point,plane_no=normal,dist=.00001)
    remove=[f for f in bm.faces if abs(f.calc_center_median().x)<width/2-.00001 and f.calc_center_median().z<height-.00001]
    bmesh.ops.delete(bm,geom=remove,context="FACES")
    for axis,value in ((0,width/2),(0,-width/2),(2,height)):
        edges=[e for e in bm.edges if e.is_boundary and all(abs(v.co[axis]-value)<.0001 for v in e.verts)]
        if edges:bmesh.ops.holes_fill(bm,edges=edges,sides=0)
    bm.normal_update();bm.to_mesh(obj.data);bm.free();obj.data.update()


def make_ridge():
    parts=[]
    for i,(scale,y,angle) in enumerate(((.72,-56,-.19),(1,0,.08),(.58,57,.28))):
        p=read_mesh(OUTPUT/"GobiMountainPeak/LOD0.meshbin")
        p.scale=(scale*.95, scale*.80, scale*.75)
        p.location=(y,math.sin(i*2)*9,0);p.rotation_euler.z=angle
        parts.append(p)
    obj=join(parts,"GobiRidge")
    activate(obj)
    obj.data.remesh_voxel_size=1.05
    bpy.ops.object.voxel_remesh()
    for p in obj.data.polygons:p.material_index=0;p.use_smooth=True
    normalize(obj,height=90)
    return obj


def make_buttress():
    parts=[]
    # Staggered, individually dressed perimeter stones provide real silhouette
    # detail without retaining the generated shrine, statue or turret.
    for course in range(30):
        z=(course+.5)*.4
        width,depth,y=(2.2,4,.1) if z<2.4 else (1.85,3.1,-.2) if z<5.6 else (1.5,2.2,-.55) if z<9 else (1.22,1.4,-.9)
        nx,ny=max(2,round(width/.48)),max(2,round(depth/.55))
        for i in range(nx):
            for j in range(ny):
                if i not in (0,nx-1) and j not in (0,ny-1):continue
                jitter=.008*math.sin(course*19+i*7+j*11)
                x=(i+.5)*width/nx-width/2+jitter
                yy=(j+.5)*depth/ny-depth/2+y
                bpy.ops.mesh.primitive_cube_add(size=1,location=(x,yy,z))
                p=bpy.context.object;p.dimensions=(width/nx-.012,depth/ny-.012,.388)
                activate(p);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
                p.data.materials.append(bpy.data.materials["Masonry"])
                bevel=p.modifiers.new("Dressed stone edges","BEVEL");bevel.width=.018;bevel.segments=2
                bpy.ops.object.modifier_apply(modifier=bevel.name);parts.append(p)
    obj=join(parts,"MedievalButtress");normalize(obj,height=12)
    return obj


def triangulate(obj):
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    bm.normal_update();bm.to_mesh(obj.data);bm.free();obj.data.update()


def write_mesh(obj,path):
    flatten(obj);triangulate(obj)
    mesh=obj.data
    uv=mesh.uv_layers.active.data if mesh.uv_layers.active else None
    points=[];normals=[];tex=[];indices=[];materials=[];cache={}
    for face in mesh.polygons:
        materials.append(SLOTS[mesh.materials[face.material_index].name])
        for loop_index in face.loop_indices:
            loop=mesh.loops[loop_index];v=mesh.vertices[loop.vertex_index]
            n=v.normal if face.use_smooth else face.normal
            t=uv[loop_index].uv[:] if uv else (0,0)
            key=(loop.vertex_index,round(t[0],6),round(t[1],6),tuple(round(x,5) for x in n))
            if key not in cache:
                cache[key]=len(points);points.append(v.co[:]);normals.append(n[:]);tex.append(t)
            indices.append(cache[key])
    with path.open("wb") as f:
        f.write(struct.pack("<4siii",b"TBM1",len(points),len(indices),len(MATERIALS)))
        for data,dtype in ((points,"<f4"),(normals,"<f4"),(tex,"<f4"),(indices,"<i4"),(materials,"<i4")):
            np.asarray(data,dtype=dtype).tofile(f)
    return len(indices)//3


def process(identifier):
    bpy.ops.object.select_all(action="SELECT");bpy.ops.object.delete(use_global=False)
    entry=SOURCES[identifier]
    obj=read_mesh(WORK/"Input"/(identifier+".meshbin"))
    normalize(obj)
    weld_and_crop(obj)
    modifications=[]
    if identifier=="GobiRidge":
        bpy.data.objects.remove(obj,do_unlink=True);obj=make_ridge();modifications.append("Reconstructed three-peak ridge from repaired mountain; hallucinated gate source excluded")
    elif identifier=="MedievalButtress":
        bpy.data.objects.remove(obj,do_unlink=True);obj=make_buttress();modifications.append("Rebuilt structural stepped buttress; shrine and statue excluded")
    elif identifier in NATURAL:
        weld_and_crop(obj,bottom=.10 if identifier=="GobiRockCluster" else .065)
        normalize(obj,height=entry["height"] if identifier!="RockOverhang" else None,width=28 if identifier=="RockOverhang" else None)
        weather_rock(obj,17+list(SOURCES).index(identifier)*3)
        modifications.extend(["Display plinth cropped and capped", "Smoothed and displaced geological surface"])
    elif identifier in ROOFS:
        weld_and_crop(obj,top=ROOFS[identifier])
        assign_surface(obj,entry);obj=roof_for(obj,entry)
        modifications.append("Curved jade-trim roof removed; straight pitched timber and thick thatch roof rebuilt")
        if identifier=="ShangWorkshop":
            lo,hi=bounds(obj)
            removed=remove_region(obj,lambda p:p.x<lo[0]+(hi[0]-lo[0])*.09 and p.z>entry['height']*.35)
            modifications.append("High chimney plume region removed: "+str(removed)+" faces")
    elif identifier in {"DeadTamarisk","DesertThornBush"}:
        weld_and_crop(obj,bottom=.065 if identifier=="DeadTamarisk" else .025)
        assign_surface(obj,entry)
        for p in obj.data.polygons:p.use_smooth=True
        if identifier=="DesertThornBush":
            parts=[obj]
            for scale,position,angle in ((.73,(.23,.12,0),2.0),(.61,(-.20,-.14,0),-1.8)):
                child=obj.copy();child.data=obj.data.copy();bpy.context.collection.objects.link(child)
                child.scale=(scale,)*3;child.location=position;child.rotation_euler.z=angle;parts.append(child)
            obj=join(parts,identifier)
        normalize(obj,height=entry["height"]);modifications.append("Display soil base trimmed; vegetation surface rebuilt")
    elif identifier=="GobiWaystone":
        weld_and_crop(obj,bottom=.18)
        lo,hi=bounds(obj);height=hi[2]-lo[2]
        # Studs protrude on the front face; flatten only their shallow front surface.
        front=float(lo[1]+(hi[1]-lo[1])*.16)
        for v in obj.data.vertices:
            if .13<(v.co.z-lo[2])/height<.86 and v.co.y<front:v.co.y=front
        normalize(obj,height=1.7);modifications.append("Pedestal cropped; protruding front hardware flattened into stone")
    elif identifier=="PyramidStonePanel":
        for v in obj.data.vertices:v.co.y*=.65/entry["depth"]
        normalize(obj,width=6);modifications.append("Wall depth reduced to 0.65 m")
    elif identifier=="GothicWindowWall":
        weld_and_crop(obj,top=.88)
        lo,hi=bounds(obj);top=float(hi[2])
        points=[v.co for v in obj.data.vertices if v.co.z>top-.015]
        left,right=min(v.x for v in points),max(v.x for v in points)
        front,back=min(v.y for v in points),max(v.y for v in points)
        verts=[(left,front,top-.01),(right,front,top-.01),(0,front,entry['height']),
               (left,back,top-.01),(right,back,top-.01),(0,back,entry['height'])]
        mesh=bpy.data.meshes.new('Plain gable coping');mesh.from_pydata(verts,[],[(0,1,2),(3,5,4),(0,3,4,1),(1,4,5,2),(2,5,3,0)])
        cap=bpy.data.objects.new('Plain gable coping',mesh);bpy.context.collection.objects.link(cap)
        mesh.materials.append(bpy.data.materials['Masonry'])
        obj=join([obj,cap],identifier)
        modifications.append("Decorative apex removed and solid plain gable coping reconstructed; window opening retained")
    elif identifier=="CaravanSupplies":
        modifications.append("Seams welded, original leather/cloth UVs preserved; small fasteners retained as decorative fittings")
    if identifier not in ROOFS:assign_surface(obj,entry)
    if identifier in {"PyramidPortal","RammedEarthGatehouse"}:
        carve_portal(obj,1.75 if identifier=="PyramidPortal" else 3.6,2.55 if identifier=="PyramidPortal" else 4.7)
        modifications.append("Through aperture carved for first-person capsule clearance")
    normalize(obj)
    triangulate(obj)
    folder=OUTPUT/identifier;folder.mkdir(parents=True,exist_ok=True)
    results=[]
    high=obj
    for level,target in enumerate(BUDGETS[identifier]):
        current=high if level==0 else high.copy()
        if level:
            current.data=high.data.copy();bpy.context.collection.objects.link(current)
        current.name=identifier+"_LOD"+str(level);activate(current)
        count=len(current.data.polygons)
        target=min(target, int(len(high.data.polygons)*(.30**level))) if level else target
        if count>target:
            modifier=current.modifiers.new("UV-preserving QEM LOD","DECIMATE")
            modifier.ratio=target/count;modifier.use_collapse_triangulate=True
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        results.append(write_mesh(current,folder/("LOD"+str(level)+".meshbin")))
        if level:current.hide_render=True;current.hide_set(True)
    lo,hi=bounds(high)
    report={"id":identifier,"sourcePrefab":entry["sourcePrefab"],"modifications":modifications,
            "materials":MATERIALS,"trianglesByLod":results,"sizeBlender":(hi-lo).tolist(),
            "staticExterior":identifier in {"ShangDwelling","ShangWorkshop","ShangGranary","MedievalTownhouse"}}
    (folder/"processing.json").write_text(json.dumps(report,indent=2)+"\n")
    bpy.ops.wm.save_as_mainfile(filepath=str(folder/(identifier+".blend")))
    print("ENVIRONMENT_READY "+json.dumps(report),flush=True)


if __name__=="__main__":
    args=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else list(SOURCES)
    OUTPUT.mkdir(parents=True,exist_ok=True)
    for identifier in args:process(identifier)
