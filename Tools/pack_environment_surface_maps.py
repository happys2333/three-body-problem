"""Run with Blender Python: pack linear roughness/AO into one lossless RGB map."""
import json
import hashlib
from pathlib import Path

import bpy
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/_Project/Art/EnvironmentReady/Textures"
SOURCES = {
    "Rock": "Assets/ThirdParty/EnvironmentReady/PolyHaven/rock_face",
    "Thatch": "Assets/ThirdParty/EnvironmentReady/PolyHaven/thatch_roof_angled",
    "Bark": "Assets/ThirdParty/EnvironmentReady/PolyHaven/pine_bark",
    "Masonry": "Assets/ThirdParty/PolyHaven/sandstone_blocks_05",
    "Earth": "Assets/ThirdParty/PolyHaven/clay_plaster",
    "Wood": "Assets/ThirdParty/PolyHaven/wood_planks",
    "RoofTile": "Assets/ThirdParty/PolyHaven/grey_roof_tiles",
    "YardGround": "Assets/ThirdParty/Environment/PolyHaven/brown_mud_dry",
}


def channel(folder, prefix, kind, size=None):
    matches = sorted(p for p in folder.rglob(prefix + "_" + kind + "_*") if p.suffix != ".meta")
    if not matches:
        return None
    image = bpy.data.images.load(str(matches[0]), check_existing=False)
    image.colorspace_settings.name = "Non-Color"
    if size and tuple(image.size) != size:
        image.scale(*size)
    size = tuple(image.size)
    pixels = np.empty(size[0] * size[1] * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    result = pixels.reshape(size[1], size[0], 4)[:, :, 0].copy()
    bpy.data.images.remove(image)
    return result


OUT.mkdir(parents=True, exist_ok=True)
STAGING = ROOT / "ArtSource/Environment/Prepared/2026-10-28/PackedSurfaces"
STAGING.mkdir(parents=True, exist_ok=True)
checks = []
for name, location in SOURCES.items():
    folder = ROOT / location
    rough = channel(folder, folder.name, "rough")
    if rough is None:
        raise RuntimeError("Missing roughness for " + name)
    height, width = rough.shape
    ao = channel(folder, folder.name, "ao", (width, height))
    pixels = np.ones((height, width, 4), dtype=np.float32)
    pixels[:, :, 0] = rough
    pixels[:, :, 1] = ao if ao is not None else 1
    image = bpy.data.images.new(name + "_Surface", width=width, height=height, alpha=False)
    image.colorspace_settings.name = "Non-Color"
    image.pixels.foreach_set(pixels.ravel())
    image.file_format = "PNG"
    temporary = STAGING / (name + "_Surface.png")
    destination = OUT / temporary.name
    image.filepath_raw = str(temporary)
    image.save()
    bpy.data.images.remove(image)
    temporary.replace(destination)
    saved = bpy.data.images.load(str(destination), check_existing=False)
    saved.colorspace_settings.name = "Non-Color"
    check = np.empty(width * height * 4, dtype=np.float32)
    saved.pixels.foreach_get(check)
    error = float(np.max(np.abs(check.reshape(height, width, 4)[:, :, :2] - pixels[:, :, :2])))
    bpy.data.images.remove(saved)
    if error > 1 / 255 + 0.00001:
        raise RuntimeError("Packed linear channel conversion changed values: " + name)
    checks.append({"material": name, "width": width, "height": height, "maxLinearError": error,
                   "path": str(destination.relative_to(ROOT)), "sha256": hashlib.sha256(destination.read_bytes()).hexdigest()})
    print(name, width, height, "roughness=R ao=G", error)
report = ROOT / "Documentation/EnvironmentProduction/Ready/packed-surfaces.json"
report.write_text(json.dumps({"passed": True, "channels": {"R": "roughness", "G": "occlusion"}, "textures": checks}, indent=2) + "\n")
