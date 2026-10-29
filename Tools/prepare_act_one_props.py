"""Build metre-scale torch and ash meshes; reuse project photographic PBR materials in Unity."""
from pathlib import Path
import math
import random
import struct
import bpy
import bmesh
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'ArtSource/ActOne/Output'
OUT.mkdir(parents=True,exist_ok=True)

def clear():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)

def material(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)

def cylinder(name,radius,depth,z,slot,vertices=32):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=radius*.91,radius2=radius,depth=depth,location=(0,0,z))
    obj=bpy.context.object;obj.name=name;obj.data.materials.append(material(slot));return obj

def join(parts,name):
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:p.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();obj=parts[0];obj.name=name
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    return obj

def export(obj,path):
    mesh=obj.data
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.update()
    points=[];normals=[];uv=[];indices=[];slots=[]
    names=['Wood','Binding','Charcoal','Ash']
    for face in mesh.polygons:
        slots.append(names.index(mesh.materials[face.material_index].name))
        for vertex in face.vertices:
            v=mesh.vertices[vertex];points.append(v.co[:]);normals.append(face.normal[:]);uv.append((v.co.x*4,v.co.z*4));indices.append(len(points)-1)
    with path.open('wb') as f:
        f.write(struct.pack('<4siii',b'TBM1',len(points),len(indices),4))
        for values,dtype in ((points,'<f4'),(normals,'<f4'),(uv,'<f4'),(indices,'<i4'),(slots,'<i4')):np.asarray(values,dtype=dtype).tofile(f)

clear();parts=[]
shaft=cylinder('Weathered hardwood shaft',.027,.57,.285,'Wood',32)
for vertex in shaft.data.vertices:
    vertex.co.x*=1+.04*math.sin(vertex.co.z*40)
parts.append(shaft)
parts.append(cylinder('Pitch-soaked linen head',.059,.20,.60,'Binding',32))
parts.append(cylinder('Blackened crown',.052,.07,.715,'Charcoal',32))
for bottom,top,radius,turns in ((.47,.70,.062,5),(.17,.27,.030,3)):
    points=[];faces=[];segments=turns*40
    for i in range(segments+1):
        t=i/segments;a=t*math.tau*turns
        for j in range(6):
            theta=j/6*math.tau;r=radius+.003*math.cos(theta)
            points.append((r*math.cos(a),r*math.sin(a),bottom+t*(top-bottom)+.003*math.sin(theta)))
        if i:
            for j in range(6):faces.append(((i-1)*6+j,(i-1)*6+(j+1)%6,i*6+(j+1)%6,i*6+j))
    mesh=bpy.data.meshes.new('Spiral hemp lashing');mesh.from_pydata(points,[],faces);obj=bpy.data.objects.new(mesh.name,mesh);bpy.context.collection.objects.link(obj);mesh.materials.append(material('Binding'));parts.append(obj)
torch=join(parts,'Wrapped travel torch')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Torch.blend'))
for level,ratio in enumerate((1,.30,.075)):
    obj=torch.copy();obj.data=torch.data.copy();bpy.context.collection.objects.link(obj);bpy.context.view_layer.objects.active=obj
    if level:
        modifier=obj.modifiers.new('LOD simplification','DECIMATE');modifier.ratio=ratio;bpy.ops.object.modifier_apply(modifier=modifier.name)
    export(obj,OUT/f'Torch_LOD{level}.meshbin');bpy.data.objects.remove(obj,do_unlink=True)
clear();parts=[];rng=random.Random(228)
for i in range(25):
    a=rng.random()*math.tau;r=rng.random()*.42
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1,location=(math.cos(a)*r,math.sin(a)*r,.03+rng.random()*.025))
    obj=bpy.context.object;obj.scale=(.08+rng.random()*.12,.05+rng.random()*.12,.018+rng.random()*.04);obj.data.materials.append(material('Charcoal' if i%3==0 else 'Ash'));parts.append(obj)
ash=join(parts,'Wind-scattered ash and charred twigs')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Ash.blend'))
for level,ratio in enumerate((1,.4,.12)):
    obj=ash.copy();obj.data=ash.data.copy();bpy.context.collection.objects.link(obj);bpy.context.view_layer.objects.active=obj
    if level:
        modifier=obj.modifiers.new('LOD simplification','DECIMATE');modifier.ratio=ratio;bpy.ops.object.modifier_apply(modifier=modifier.name)
    export(obj,OUT/f'Ash_LOD{level}.meshbin');bpy.data.objects.remove(obj,do_unlink=True)
print('ACT_ONE_PROPS_READY',OUT)
