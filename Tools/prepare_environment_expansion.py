"""Repair the supplementary environment library, retaining immutable source archives.

Run with Blender --background --python Tools/prepare_environment_expansion.py -- [IDs].
Inputs are the metre-scale TBM1 exports of saved Unity candidate prefabs.
"""
import importlib.util
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix
from mathutils import Vector, noise

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / 'ArtSource/Environment/Prepared/2026-10-31'
spec = importlib.util.spec_from_file_location('base_pipeline', ROOT/'Tools/prepare_environment_meshes.py')
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)

SURFACES = base.MATERIALS
BUDGETS = {
    'NaturalRidgeV2': (100000,24000,5000), 'ShadeOverhangV2': (75000,18000,4000),
    'WindCarvedPillar': (48000,12000,2400), 'SplitBoulder': (32000,8000,1800),
    'ScreePatch': (24000,6000,1400), 'DeadTamariskV2': (28000,7000,1600),
    'DesertDryScrubV2': (20000,5500,1200), 'CharredBush': (18000,5000,1100),
    'ScorchedTree': (25000,6500,1500), 'ShangTimberGate': (85000,22000,5000),
    'PalaceStoneColumn': (65000,16000,3500), 'ShangBronzeDing': (34000,9000,2000),
    'ShangBronzeBrazier': (32000,8500,1900), 'ShangMarketGoods': (38000,10000,2400),
    'MedievalStoneArch': (75000,18000,4000), 'MedievalPew': (28000,7000,1500),
    'MedievalLectern': (24000,6000,1300), 'MedievalIronBrazier': (30000,7500,1700),
    'ShangStraightEarthWall': (45000,10000,2000), 'GobiShadeCanopyV3': (85000,20000,4500),
    'PyramidEntryPassage': (85000,20000,4500), 'PalaceStoneStairs': (65000,16000,3500),
    'BronzeWallTorchBracket': (18000,5000,1000), 'ShangOracleBone': (18000,4500,1000),
    'TravellerWaterskinV2': (26000,6500,1400),
    'TravellerLeatherFlask': (26000,6500,1400),
}
SUPERSEDED = {'ShangRammedWall': 'ShangStraightEarthWall', 'TravellerWaterskin':'TravellerWaterskinV2'}
DERIVED = {'CharredBush':'DesertDryScrubV2', 'ScorchedTree':'DeadTamariskV2'}
SOURCE_OVERRIDES = {'TravellerLeatherFlask':'TravellerWaterskin'}
REJECTED_CANDIDATES = {
    'TravellerWaterskinV2': 'Visible modern zipper; use the rebuilt TravellerLeatherFlask instead.',
    'GobiShadeCanopyV3': 'Reverse-side art review exposes folded paper-thin roof faces and major holes. Retain the source archive and use ShadeOverhangV2.',
}
ROCK = {'NaturalRidgeV2','ShadeOverhangV2','WindCarvedPillar','SplitBoulder','ScreePatch','GobiShadeCanopyV3'}
VEGETATION = {'DeadTamariskV2','DesertDryScrubV2','CharredBush','ScorchedTree'}
OPENINGS = {'ShangTimberGate':(2.1,2.65),'MedievalStoneArch':(1.65,2.35),'PyramidEntryPassage':(3.4,4.4)}


def clear():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    for data in list(bpy.data.meshes):
        if data.users==0:bpy.data.meshes.remove(data)


def assign(obj, surface):
    for face in obj.data.polygons:face.material_index=base.SLOTS[surface]


def timber_gate(entry):
    parts=[]
    def block(name,location,dimensions,surface,bevel=.035):
        bpy.ops.mesh.primitive_cube_add(size=1,location=location)
        p=bpy.context.object;p.name=name;p.dimensions=dimensions
        base.activate(p);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        p.data.materials.append(bpy.data.materials[surface])
        mod=p.modifiers.new('Worn dressed edges','BEVEL');mod.width=bevel;mod.segments=4
        bpy.ops.object.modifier_apply(modifier=mod.name);parts.append(p)
    for x in (-1.68,1.68):
        for y in (-.52,.52):
            block('Stone footing',(x,y,.14),(.72,.72,.28),'Masonry')
            block('Heavy timber post',(x,y,1.79),(.43,.43,3.22),'Wood')
            block('Mortised post cap',(x,y,3.30),(.72,.64,.20),'Wood')
    for y in (-.58,.58):
        block('Through lintel',(0,y,3.18),(4.45,.40,.46),'Wood')
        block('Upper lintel',(0,y,3.51),(4.8,.32,.20),'Wood')
    for x in (-1.68,1.68):block('Cross tie',(x,0,3.40),(.30,2.05,.30),'Wood')
    frame=base.join(parts,'Rebuilt mortised timber gate')
    roof_entry=dict(entry);roof_entry['height']=4.65
    return base.roof_for(frame,roof_entry)


def hollow_palace(obj):
    """A large stone envelope with thick walls, an entrance and a real chamber."""
    base.normalize(obj,width=48)
    # Seal cracks before subtracting walkable space; original UVs are replaced by
    # photographic triplanar masonry, so remeshing does not smear a source atlas.
    base.activate(obj);obj.data.remesh_voxel_size=.14
    bpy.ops.object.voxel_remesh()
    base.normalize(obj,width=48)
    for name,location,dimensions in (
        ('Entrance passage',(0,-14,1.8),(3.4,32,4.4)),
        ('Royal chamber',(0,0,3.9),(14,18,8.2)),
    ):
        bpy.ops.mesh.primitive_cube_add(size=1,location=location)
        cutter=bpy.context.object;cutter.dimensions=dimensions
        base.activate(cutter);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        base.activate(obj);mod=obj.modifiers.new(name,'BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
        bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)
    assign(obj,'Masonry')
    parts=[obj]
    for name,location,dimensions in (
        ('Stone chamber floor',(0,0,.125),(14,18,.25)),
        ('Passage floor',(0,-15,.125),(3.4,30,.25)),
        ('Royal dais',(0,5,.50),(4.4,3.2,.50)),
        ('Dais first step',(0,3.1,.375),(4.8,.65,.25)),
    ):
        bpy.ops.mesh.primitive_cube_add(size=1,location=location)
        p=bpy.context.object;p.name=name;p.dimensions=dimensions
        base.activate(p);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        p.data.materials.append(bpy.data.materials['Masonry'])
        bevel=p.modifiers.new('Worn stone edges','BEVEL');bevel.width=.025;bevel.segments=3
        bpy.ops.object.modifier_apply(modifier=bevel.name);parts.append(p)
    return base.join(parts,'Enterable thick-wall pyramid palace')


def clean_vegetation(obj, source_id):
    base.weld_and_crop(obj,bottom=.065 if source_id=='DeadTamariskV2' else .035)
    lo,hi=base.bounds(obj);w,d,h=hi-lo
    if source_id=='DeadTamariskV2':
        # The conspicuous round bird and central perch are removed together.
        cx=(lo[0]+hi[0])/2
        base.remove_region(obj,lambda p:abs(p.x-cx)<w*.16 and p.z>lo[2]+h*.69)
    assign(obj,'Bark')


def leather_flask():
    """Rebuild a closed leather pouch and tied neck without generated hardware."""
    parts=[]
    bpy.ops.mesh.primitive_uv_sphere_add(segments=128,ring_count=64,location=(0,0,.15))
    body=bpy.context.object;body.name='Wrinkled leather pouch';body.scale=(.13,.085,.15)
    base.activate(body);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    for v in body.data.vertices:
        p=v.co; radial=1+.014*noise.noise(Vector((p.x*70,p.y*70,p.z*45)))
        p.x*=radial;p.y*=radial
    body.data.materials.append(bpy.data.materials['OriginalSupplies']);parts.append(body)
    bpy.ops.mesh.primitive_cylinder_add(vertices=128,radius=.028,depth=.095,location=(0,0,.323))
    neck=bpy.context.object;neck.name='Closed folded leather neck';neck.data.materials.append(bpy.data.materials['OriginalSupplies'])
    bevel=neck.modifiers.new('Soft folded rim','BEVEL');bevel.width=.006;bevel.segments=5
    base.activate(neck);bpy.ops.object.modifier_apply(modifier=bevel.name);parts.append(neck)
    for i in range(5):
        bpy.ops.mesh.primitive_torus_add(major_segments=96,minor_segments=10,location=(0,0,.302+i*.006),major_radius=.032,minor_radius=.0035)
        rope=bpy.context.object;rope.name='Hemp neck binding';rope.data.materials.append(bpy.data.materials['Thatch']);parts.append(rope)
    for a,b in (((.025,-.018,.313),(.055,-.035,.244)),((-.026,-.014,.31),(-.047,-.037,.266))):
        parts.append(base.cylinder_between(a,b,.003,'Thatch',sides=12))
    result=base.join(parts,'Tied ancient leather flask')
    for f in result.data.polygons:f.use_smooth=True
    return result


def repair_canopy(obj):
    # Remove the complete staging/figure band, independent of FBX front direction.
    # The tall back wall remains attached to the overhang and is grounded again.
    base.weld_and_crop(obj,bottom=.30)
    assign(obj,'Rock')


def process(identifier, sources):
    if identifier in REJECTED_CANDIDATES:raise ValueError(REJECTED_CANDIDATES[identifier])
    clear()
    source_id=SOURCE_OVERRIDES.get(identifier,DERIVED.get(identifier,identifier))
    entry=dict(sources[source_id]);entry['id']=identifier
    obj=base.read_mesh(WORK/'Input'/f'{source_id}.meshbin')
    base.normalize(obj);base.weld_and_crop(obj)
    changes=[];surface='OriginalSupplies';usage='Static environment prop'
    if identifier=='TravellerLeatherFlask':
        bpy.data.objects.remove(obj,do_unlink=True);obj=leather_flask()
        usage='Carryable tied leather flask; reconstructed without modern zipper or metal hardware'
        changes.append('Body and tied neck rebuilt; original source used as a size/silhouette reference only. Clean leather-only atlas region is remapped; generated zipper geometry and imagery are excluded.')
    elif identifier in VEGETATION:
        clean_vegetation(obj,source_id);surface='Bark'
        changes.append('Removed display ground; vegetation uses photographic bark.')
        if source_id=='DeadTamariskV2':changes.append('Removed bird-like central object and its perch.')
        if identifier in DERIVED:
            changes.append('Derived from the cleaned matching unburnt plant; rejected baked fire/person source is not used.')
            if identifier=='ScorchedTree':
                lo,hi=base.bounds(obj)
                base.weld_and_crop(obj,top=.91)
            usage='Charred vegetation state; no baked flames or people'
    elif identifier in {'ShadeOverhangV2','GobiShadeCanopyV3'}:
        repair_canopy(obj);surface='Rock';usage='Natural shelter with open front; verify capsule clearance in the yard'
        changes.append('Complete lower staging band, person and sphere removed; back wall cut capped and regrounded.')
    elif identifier in ROCK:
        base.weld_and_crop(obj,bottom=.12 if identifier in {'WindCarvedPillar','SplitBoulder'} else .06 if identifier!='NaturalRidgeV2' else .02)
        if identifier=='NaturalRidgeV2':
            for v in obj.data.vertices:v.co.y*=1.7
            usage='Distant ridge backdrop; not a foreground climbable mountain'
            changes.append('Thin ridge deepened; reserved for distant terrain composition.')
        if identifier=='ScreePatch':
            for v in obj.data.vertices:v.co.z*=.6
            usage='Low rock pile; combine with existing scattered-stone assets'
        base.weather_rock(obj,81+list(sources).index(source_id)*3);assign(obj,'Rock');surface='Rock'
        changes.append('Display plinth cropped and capped; silhouette weathered; photographic rock surface.')
    elif identifier=='ShangTimberGate':
        bpy.data.objects.remove(obj,do_unlink=True);obj=timber_gate(entry);surface='Mixed'
        changes.append('Rebuilt symmetrical mortised timber frame, dressed stone feet and straight thatch roof; broken wall and late-style trim excluded.')
    elif identifier=='PalaceStoneColumn':
        lo,hi=base.bounds(obj);w,d,h=hi-lo;cx=(lo[0]+hi[0])/2;cy=(lo[1]+hi[1])/2
        base.remove_region(obj,lambda p:p.z<lo[2]+h*.18 and (abs(p.x-cx)>w*.30 or abs(p.y-cy)>d*.30))
        base.weld_and_crop(obj,bottom=.10);assign(obj,'Masonry');surface='Masonry'
        changes.append('Loose display debris removed; structural column base retained; photographic masonry.')
    elif identifier=='ShangStraightEarthWall':
        base.weld_and_crop(obj,bottom=.04,top=.70)
        base.normalize(obj,width=8)
        lo,hi=base.bounds(obj);d=hi[1]-lo[1];h=hi[2]-lo[2]
        for v in obj.data.vertices:
            v.co.y*=1.5/max(d,.0001);v.co.z*=3/max(h,.0001)
        assign(obj,'Earth');surface='Earth'
        changes.append('Unwanted roof-like timber trim and display footing cropped; capped solid earth wall set to 8 by 3 by 1.5 metres with a photographic earth surface.')
    elif identifier=='PyramidEntryPassage':
        obj=hollow_palace(obj);surface='Masonry'
        usage='Enterable pyramid palace: 48 m exterior, 14 by 18 m chamber, 8 m headroom, thick stone walls and a raised dais'
        changes.append('Entire generated pyramid repurposed as a large palace; gold finish replaced by masonry; real chamber and entrance subtracted; floor and dais rebuilt.')
    elif identifier in {'MedievalStoneArch','PalaceStoneStairs'}:
        base.weld_and_crop(obj,bottom=.012 if identifier=='PalaceStoneStairs' else .025)
        if identifier=='PalaceStoneStairs':
            lo,hi=base.bounds(obj)
            for v in obj.data.vertices:
                v.co.y*=6/(hi[1]-lo[1]);v.co.z*=3/(hi[2]-lo[2])
            changes.append('Rescaled over-tall generated staircase to 4 m width, 6 m run and 3 m rise for first-person traversal.')
        assign(obj,'Masonry');surface='Masonry'
        changes.append('Photographic masonry surface; display ground trimmed.')
    elif identifier=='BronzeWallTorchBracket':
        base.weld_and_crop(obj,top=.78)
        assign(obj,'OriginalSupplies')
        changes.append('Generated flame geometry cropped; live torch is supplied as a separate Unity attachment.')
        usage='Wall-mounted torch holder'
    else:
        assign(obj,'OriginalSupplies')
        changes.append('Original PBR atlas retained; UV-preserving QEM LODs.')
    if identifier in OPENINGS and identifier!='PyramidEntryPassage':
        width,height=OPENINGS[identifier]
        base.carve_portal(obj,width,height)
        changes.append(f'Clear through aperture carved: {width} m wide by {height} m high.')
        usage='Traversable architectural opening'
    if identifier!='PyramidEntryPassage':base.normalize(obj)
    base.triangulate(obj)
    folder=WORK/'Output'/identifier;folder.mkdir(parents=True,exist_ok=True)
    high=obj;counts=[]
    for level,budget in enumerate(BUDGETS[identifier]):
        current=high if level==0 else high.copy()
        if level:current.data=high.data.copy();bpy.context.collection.objects.link(current)
        base.activate(current);current.name=f'{identifier}_LOD{level}'
        count=len(current.data.polygons)
        if level:budget=min(budget,max(64,int(len(high.data.polygons)*(.35**level))))
        if count>budget:
            mod=current.modifiers.new('UV-preserving QEM','DECIMATE');mod.ratio=budget/count;mod.use_collapse_triangulate=True
            bpy.ops.object.modifier_apply(modifier=mod.name)
        if identifier=='PyramidEntryPassage':
            lo,hi=base.bounds(current)
            for vertex in current.data.vertices:vertex.co.z-=lo[2]
        else:base.normalize(current)
        counts.append(base.write_mesh(current,folder/f'LOD{level}.meshbin'))
        if level:current.hide_render=True;current.hide_set(True)
    lo,hi=base.bounds(high)
    report=dict(id=identifier,sourceId=source_id,sourcePrefab=entry['sourcePrefab'],modifications=changes,
        materials=SURFACES,trianglesByLod=counts,sizeBlender=(hi-lo).tolist(),usage=usage,
        charred=identifier in DERIVED,openingWidth=OPENINGS.get(identifier,(0,0))[0],
        openingHeight=OPENINGS.get(identifier,(0,0))[1],hasInterior=identifier=='PyramidEntryPassage')
    (folder/'processing.json').write_text(json.dumps(report,indent=2)+'\n')
    bpy.ops.wm.save_as_mainfile(filepath=str(folder/f'{identifier}.blend'))
    print('EXPANSION_READY '+json.dumps(report),flush=True)


if __name__=='__main__':
    sources={e['id']:e for e in json.loads((WORK/'sources.json').read_text())['assets']}
    wanted=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [i for i in BUDGETS if SOURCE_OVERRIDES.get(i,DERIVED.get(i,i)) in sources and i not in REJECTED_CANDIDATES]
    for identifier in wanted:
        if identifier in SUPERSEDED:continue
        process(identifier,sources)
