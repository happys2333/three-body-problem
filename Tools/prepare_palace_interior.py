"""Author editable, bevelled Shang-inspired interior furnishings in Blender.

Raw generated inputs are untouched. This batch is local art production and does
not spend Hunyuan requests. Four material slots: Stone / Wood / Metal / Leather.
"""
import importlib.util
import math
import json
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('gear_geometry', ROOT / 'Tools/prepare_era_guard_weapons.py')
g = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g)
g.OUT = ROOT / 'ArtSource/PalaceInterior/2026-11-01'
g.SLOTS = ['Stone', 'Wood', 'Metal', 'Leather']


def reset():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)


def ball(name, position, radius, scale, slot='Metal'):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=radius, location=position)
    obj = bpy.context.object
    obj.scale = scale
    return g.finish(obj, name, slot, smooth=True)


def face_motif(x, z, size, y=-.1):
    parts = []
    outline = [(-.5, -.57), (-.64, .12), (-.43, .48), (0, .61), (.43, .48), (.64, .12), (.5, -.57), (0, -.7)]
    body = g.blade('Cast beast face plate', [(x+a*size, z+b*size) for a,b in outline], .045*size)
    body.location.y = y
    parts.append(body)
    for side in (-1, 1):
        ex, ez = x+side*.29*size, z+.07*size
        points = [(ex+math.cos(a)*.20*size, y-.06*size, ez+math.sin(a)*.12*size) for a in [i*math.pi/20 for i in range(41)]]
        parts.append(g.cord('Raised almond eye surround', points, .024*size, 'Metal'))
        parts.append(ball('Deep eye inset', (ex,y-.035*size,ez), .085*size, (1,.4,1), 'Leather'))
        parts.append(ball('Bronze eye pupil', (ex,y-.09*size,ez), .032*size, (1,.5,1)))
        horns = [(x+side*a*size,y-.04*size,z+b*size) for a,b in ((.14,.40),(.27,.70),(.52,.84),(.67,.73),(.63,.55))]
        parts.append(g.cord('Curved casting horn', horns, .032*size, 'Metal'))
        for j in range(3):
            xx=x+side*(.17+j*.12)*size
            parts.append(g.box('Cheek casting rib',(xx,y-.055*size,z-(.25+j*.035)*size),(.075*size,.035*size,.20*size),'Metal',.008*size))
    parts.append(g.box('Central nose flange',(x,y-.065*size,z-.09*size),(.10*size,.10*size,.62*size),'Metal',.012*size))
    return parts


def key_motif(x, z, side, size=.2, y=-.12):
    points=[(0,0),(1,0),(1,1),(0,1),(0,.3),(.65,.3),(.65,.7),(.3,.7)]
    return g.cord('Geometric thunder scroll',[(x+side*(a-.5)*size,y,z+(b-.5)*size) for a,b in points],.011,'Metal')


def throne():
    reset(); parts=[]
    for x in (-.78,.78):
        parts.append(g.box('Dressed stone foot',(x,0,.16),(.34,1.38,.32),'Stone',.035))
        parts.append(g.box('Timber seat bearer',(x,0,.43),(.18,1.25,.35),'Wood',.022))
    parts.append(g.box('Joined timber seat',(0,-.03,.60),(1.9,1.4,.16),'Wood',.025))
    parts.append(g.box('Solid high timber back',(0,.60,1.45),(1.98,.16,1.78),'Wood',.026))
    for x in (-1.02,1.02):
        parts.append(g.box('Stone arm support',(x,0,.67),(.21,1.22,1.12),'Stone',.028))
        parts.append(g.box('Bronze capped armrest',(x,-.05,1.03),(.26,1.35,.12),'Metal',.025))
        parts+=face_motif(x,.85,.26,-.72)
    for x in (-.97,.97):parts.append(g.box('Bronze back upright',(x,.49,1.46),(.065,.075,1.72),'Metal',.014))
    for z in (.65,2.28):parts.append(g.box('Back casting border',(0,.49,z),(2.05,.075,.08),'Metal',.014))
    parts+=face_motif(0,1.65,.68,.46)
    for side in (-1,1):
        for i in range(5):parts.append(key_motif(side*.76,.92+i*.25,side,.18,.46))
    # Sculpted, thick hide draped over the seat rather than a flat colored cube.
    nx,ny=28,20; vertices=[];faces=[]
    for i in range(nx+1):
        x=(i/nx-.5)*1.72
        for j in range(ny+1):
            y=(j/ny-.5)*1.23-.03
            z=.72+.012*math.sin(x*15+y*8)+.009*math.sin(x*27-y*13)
            if j<3:z-=.22*(1-j/3)
            vertices.append((x,y,z))
            if i<nx and j<ny:
                a=i*(ny+1)+j;faces.append((a,a+ny+1,a+ny+2,a+1))
    mesh=bpy.data.meshes.new('Folded hide cushion');mesh.from_pydata(vertices,[],faces);mesh.update()
    hide=bpy.data.objects.new('Folded hide cushion',mesh);bpy.context.collection.objects.link(hide)
    g.finish(hide,hide.name,'Leather',smooth=True)
    bpy.context.view_layer.objects.active=hide
    thick=hide.modifiers.new('Hide thickness','SOLIDIFY');thick.thickness=.045
    bpy.ops.object.modifier_apply(modifier=thick.name);parts.append(hide)
    g.save('RoyalSeat',parts)


def relief():
    reset(); parts=[g.box('Cast bronze panel',(0,.025,2),(2.8,.10,4),'Metal',.025)]
    for x in (-1.34,1.34):parts.append(g.box('Raised outer border',(x,-.055,2),(.055,.09,3.87),'Metal',.012))
    for z in (.07,3.93):parts.append(g.box('Raised outer border',(0,-.055,z),(2.68,.09,.055),'Metal',.012))
    parts+=face_motif(0,2.0,1.5,-.06)
    for side in (-1,1):
        for i in range(11):parts.append(key_motif(side*1.17,.31+i*.338,side,.23,-.055))
        for z in (.34,3.66):
            for i in range(5):parts.append(key_motif(side*(.15+i*.20),z,side,.16,-.055))
        for z in (.15,1.25,2.75,3.85):parts.append(ball('Casting fixing stud',(side*1.30,-.12,z),.025,(1,.5,1)))
    g.save('RoyalRelief',parts)


def table():
    reset();parts=[]
    for x in (-1.04,1.04):
        for y in (-.42,.42):parts.append(g.box('Mortise timber leg',(x,y,.40),(.12,.12,.80),'Wood',.016))
        parts.append(g.box('Short stretcher',(x,0,.23),(.08,.91,.09),'Wood',.012))
    for y in (-.43,.43):parts.append(g.box('Long stretcher',(0,y,.36),(2.2,.075,.085),'Wood',.012))
    for i in range(6):parts.append(g.box('Separate desktop board',(0,(i-2.5)*.185,.845),(2.6,.18,.10),'Wood',.012))
    for side in (-1,1):
        for z in (.16,.65):parts.append(g.box('Bronze table join cap',(side*1.107,-.42,z),(.025,.16,.10),'Metal',.008))
    g.save('CalendarTable',parts)


def vessel(identifier,gu=False):
    reset();parts=[]
    if gu:
        profile=[(.066,0),(.076,.012),(.048,.055),(.034,.13),(.045,.19),(.081,.28)]
    else:
        profile=[(.045,.07),(.066,.09),(.061,.16),(.072,.18)]
    n=80;v=[];faces=[]
    for i,(radius,z) in enumerate(profile):
        for k in range(n):
            a=k*math.tau/n;v.append((math.cos(a)*radius,math.sin(a)*radius,z))
            if i:
                a0=(i-1)*n+k;b0=(i-1)*n+(k+1)%n;faces.append((a0,b0,b0+n,a0+n))
    # Hollow shell with a separate thick lip; open mouth remains visible.
    mesh=bpy.data.meshes.new(identifier);mesh.from_pydata(v,[],faces);mesh.update()
    obj=bpy.data.objects.new(identifier,mesh);bpy.context.collection.objects.link(obj)
    g.finish(obj,identifier,'Metal',smooth=True)
    bpy.context.view_layer.objects.active=obj
    shell=obj.modifiers.new('Hollow bronze casting','SOLIDIFY');shell.thickness=.003
    bpy.ops.object.modifier_apply(modifier=shell.name);parts.append(obj)
    r,z=profile[-1]
    parts.append(g.cord('Cast mouth rim',[(math.cos(i*math.tau/80)*r,math.sin(i*math.tau/80)*r,z) for i in range(81)],.003,'Metal'))
    if not gu:
        for i in range(3):
            a=i*math.tau/3
            leg=g.cylinder('Three separate casting legs',(math.cos(a)*.041,math.sin(a)*.041,.04),.007,.08,'Metal',24)
            parts.append(leg)
        for x in (-.04,.04):parts.append(g.cylinder('Paired mouth pillars',(x,0,.21),.004,.06,'Metal',20))
        parts.append(g.blade('Long pouring lip',[(-.072,.175),(-.14,.195),(-.075,.19)],.012))
        parts.append(g.blade('Rear pointed tail',[(.072,.175),(.12,.185),(.073,.19)],.012))
        parts.append(g.cord('Single side handle',[(0,.06,.15),(0,.105,.15),(0,.108,.10),(0,.06,.10)],.005,'Metal'))
    for i in range(14):
        a=i*math.tau/14;r=.035 if gu else .063;z=.13
        parts.append(g.cord('Fine vertical casting ribs',[(math.cos(a)*r,math.sin(a)*r,z-.025),(math.cos(a)*r,math.sin(a)*r,z+.025)],.0017,'Metal'))
    g.save(identifier,parts)


if __name__=='__main__':
    throne();relief();table();vessel('BronzeJue');vessel('BronzeGu',True)
    for metadata in g.OUT.glob('*/processing.json'):
        value=json.loads(metadata.read_text())
        value['scope']='Original static palace furnishing; metre-scale coordinate conversion and three distance LODs. Placement and contact verified in Unity.'
        metadata.write_text(json.dumps(value,indent=2)+'\n')
    (g.OUT/'provenance.json').write_text(json.dumps({'authoredLocally':True,'date':'2026-11-01','sourceScript':'Tools/prepare_palace_interior.py','style':'Original Shang-inspired game furnishings, not a copy of TV production art.','materials':['Stone','Wood','Metal','Leather']},indent=2)+'\n')
