"""Compare the imported Blender scene to the export manifest.

Never auto-fix mesh topology. Non-manifold and zero-weight vertices are warnings.
"""

from __future__ import annotations

from typing import Any

import bpy


def collect_scene_stats() -> dict[str, Any]:
    meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    mesh = _primary_mesh(meshes)
    armature = _primary_armature(armatures, mesh)
    if mesh is None:
        return {
            "meshName": None,
            "armatureName": armature.name if armature else None,
            "vertices": 0,
            "faces": 0,
            "loops": 0,
            "materialSlots": 0,
            "morphTargets": 0,
            "bones": len(armature.data.bones) if armature else 0,
            "vertexGroups": 0,
            "uvChannels": 0,
            "hasVertexColors": False,
            "hasArmatureModifier": False,
            "boundImages": _bound_image_count(),
            "missingImages": len(_missing_images()),
            "materialNames": [],
        }

    data = mesh.data
    shape_keys = data.shape_keys
    morphs = 0
    if shape_keys is not None:
        morphs = len([block for block in shape_keys.key_blocks if block.name != "Basis"])
    color_attrs = list(getattr(data, "color_attributes", []))
    return {
        "meshName": mesh.name,
        "armatureName": armature.name if armature else (mesh.parent.name if mesh.parent else None),
        "vertices": len(data.vertices),
        "faces": len(data.polygons),
        "loops": len(data.loops),
        "materialSlots": len(mesh.material_slots),
        "morphTargets": morphs,
        "bones": len(armature.data.bones) if armature else 0,
        "vertexGroups": len(mesh.vertex_groups),
        "uvChannels": len(data.uv_layers),
        "hasVertexColors": len(color_attrs) > 0,
        "hasArmatureModifier": any(mod.type == "ARMATURE" for mod in mesh.modifiers),
        "boundImages": _bound_image_count(),
        "missingImages": len(_missing_images()),
        "materialNames": [
            slot.material.name if slot.material else "" for slot in mesh.material_slots
        ],
    }


def validate_against_manifest(
    manifest: dict[str, Any],
    stats: dict[str, Any],
) -> tuple[list[str], list[str]]:
    errors: list[str] = []
    warnings: list[str] = []
    mesh = manifest.get("mesh") or {}
    lod0 = _lod0(mesh)
    if lod0 is None:
        errors.append("manifest mesh.lods is missing LOD0")
        return errors, warnings

    _expect(errors, "lod0.vertices", lod0.get("vertices"), stats["vertices"])
    _expect(errors, "lod0.triangles", lod0.get("triangles"), stats["faces"])
    _expect(errors, "lod0.indices", lod0.get("indices"), stats["loops"])
    _expect(errors, "lod0.sections", lod0.get("sections"), stats["materialSlots"])
    _expect(errors, "morphTargets", mesh.get("morphTargetCount"), stats["morphTargets"])
    if int(mesh.get("uvChannels") or 0) > int(stats["uvChannels"]):
        errors.append(
            f"uvChannels: expected at least {mesh.get('uvChannels')}, got {stats['uvChannels']}"
        )
    if mesh.get("hasVertexColors") and not stats["hasVertexColors"]:
        errors.append("hasVertexColors: expected true, got false")
    if not stats["hasArmatureModifier"]:
        errors.append("armature modifier is missing")

    skeleton = manifest.get("skeleton") or {}
    cooked = skeleton.get("boneCount")
    if cooked is not None and int(cooked) != int(stats["bones"]):
        warnings.append(
            f"blender bones {stats['bones']} vs cooked {cooked} "
            "(UEFormat import may add bones; P0 Jinhsi armature is 204)"
        )

    asset_slots = mesh.get("materialSlotCount")
    if asset_slots is not None and int(asset_slots) != int(stats["materialSlots"]):
        warnings.append(
            f"LOD0 mesh slots {stats['materialSlots']} vs asset materialSlots {asset_slots}"
        )

    unique_textures = len({
        (texture.get("objectPath") or "").casefold()
        for texture in manifest.get("textures") or []
        if texture.get("objectPath")
    })
    if unique_textures and int(stats["boundImages"]) < unique_textures:
        warnings.append(
            f"{stats['boundImages']} of {unique_textures} staging textures are bound in the .blend; "
            "unassigned textures are still packed when present"
        )

    missing = _missing_images()
    if missing:
        errors.append("missing images: " + ", ".join(missing[:12]))

    zero_weight = _zero_weight_vertex_count()
    if zero_weight:
        warnings.append(f"{zero_weight} vertices have no vertex-group weights")

    non_manifold = _non_manifold_edge_count()
    if non_manifold:
        warnings.append(f"{non_manifold} non-manifold edges (warning only; mesh was not modified)")

    return errors, warnings


def missing_image_names() -> list[str]:
    return _missing_images()


def _primary_mesh(meshes: list) -> Any | None:
    if not meshes:
        return None
    for obj in meshes:
        if obj.name.endswith("_LOD0") or "_LOD0" in obj.name:
            return obj
    return meshes[0]


def _primary_armature(armatures: list, mesh) -> Any | None:
    if mesh is not None:
        for modifier in mesh.modifiers:
            if modifier.type == "ARMATURE" and modifier.object is not None:
                return modifier.object
        if mesh.parent is not None and mesh.parent.type == "ARMATURE":
            return mesh.parent
    return armatures[0] if armatures else None


def _lod0(mesh: dict[str, Any]) -> dict[str, Any] | None:
    for lod in mesh.get("lods") or []:
        if int(lod.get("index", -1)) == 0:
            return lod
    lods = mesh.get("lods") or []
    return lods[0] if lods else None


def _expect(errors: list[str], name: str, expected, actual) -> None:
    if expected is None:
        return
    if int(expected) != int(actual):
        errors.append(f"{name}: expected {expected}, got {actual}")


def _bound_image_count() -> int:
    count = 0
    for image in bpy.data.images:
        if _is_generated(image):
            continue
        if image.packed_file or (image.filepath and _abspath_exists(image)):
            count += 1
    return count


def _missing_images() -> list[str]:
    missing: list[str] = []
    for image in bpy.data.images:
        if _is_generated(image):
            continue
        if image.packed_file:
            continue
        if image.filepath and _abspath_exists(image):
            continue
        missing.append(image.name)
    return missing


def _is_generated(image) -> bool:
    source = getattr(image, "source", "")
    if source in {"VIEWER", "GENERATED"}:
        return True
    return image.name in {"Render Result", "Viewer Node"}


def _abspath_exists(image) -> bool:
    import os

    try:
        path = bpy.path.abspath(image.filepath)
    except Exception:
        return False
    return bool(path) and os.path.isfile(path)


def _zero_weight_vertex_count() -> int:
    total = 0
    for obj in bpy.data.objects:
        if obj.type != "MESH" or not obj.vertex_groups:
            continue
        mesh = obj.data
        for vertex in mesh.vertices:
            if not vertex.groups:
                total += 1
    return total


def _non_manifold_edge_count() -> int:
    try:
        import bmesh
    except ImportError:
        return 0
    total = 0
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        bm = bmesh.new()
        try:
            bm.from_mesh(obj.data)
            bm.edges.ensure_lookup_table()
            total += sum(1 for edge in bm.edges if not edge.is_manifold)
        finally:
            bm.free()
    return total
