"""Build a baseline Principled BSDF graph from the material profile.

Advanced WuWa NPR / Goo Engine parity is out of scope for P4.
"""

from __future__ import annotations

from pathlib import Path
from typing import Any

import bpy

from .manifest_io import kind_for_name, role_for_parameter, snapshot_for_material, texture_file_map


def apply_materials(
    manifest: dict[str, Any],
    staging: Path,
    profile: dict[str, Any],
    warnings: list[str],
) -> dict[str, bpy.types.Image]:
    images = _load_texture_images(manifest, staging, warnings)
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        for slot in obj.material_slots:
            material = slot.material
            if material is None:
                continue
            snapshot = snapshot_for_material(manifest, material.name)
            if snapshot is None:
                warnings.append(f"No manifest snapshot for material {material.name}")
                continue
            kind = kind_for_name(profile, material.name)
            if kind.get("skip"):
                continue
            _build_pbr(material, snapshot, images, profile, kind, warnings)
    return images


def _load_texture_images(
    manifest: dict[str, Any],
    staging: Path,
    warnings: list[str],
) -> dict[str, bpy.types.Image]:
    images: dict[str, bpy.types.Image] = {}
    for object_path, file_path in texture_file_map(manifest, staging).items():
        if not file_path.is_file():
            warnings.append(f"Texture file missing: {file_path}")
            continue
        image = bpy.data.images.load(str(file_path), check_existing=True)
        image.name = file_path.stem
        image.use_fake_user = True
        images[object_path] = image
        images[object_path.casefold()] = image
    return images


def _build_pbr(
    material: bpy.types.Material,
    snapshot: dict[str, Any],
    images: dict[str, bpy.types.Image],
    profile: dict[str, Any],
    kind: dict[str, Any],
    warnings: list[str],
) -> None:
    material.use_nodes = True
    tree = material.node_tree
    if tree is None:
        warnings.append(f"{material.name}: material has no node tree")
        return
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    output.location = (720, 0)
    bsdf = tree.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (360, 0)
    tree.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])

    roles: dict[str, tuple[str, bpy.types.Image]] = {}
    for parameter, object_path in (snapshot.get("textures") or {}).items():
        image = images.get(object_path) or images.get(str(object_path).casefold())
        if image is None:
            continue
        role = role_for_parameter(profile, parameter)
        if role and role not in roles:
            roles[role] = (parameter, image)

    x = -480
    y = 280
    if "baseColor" in roles:
        _, image = roles["baseColor"]
        tex = _image_node(tree, image, x, y, non_color=False)
        tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if kind.get("useBaseColorAlpha"):
            alpha_input = bsdf.inputs.get("Alpha")
            if alpha_input is not None:
                tree.links.new(tex.outputs["Alpha"], alpha_input)
        y -= 280

    if "normal" in roles:
        _, image = roles["normal"]
        tex = _image_node(tree, image, x, y, non_color=True)
        normal_map = tree.nodes.new("ShaderNodeNormalMap")
        normal_map.location = (80, y)
        if profile.get("normalYFlip", True):
            flipped = _flip_green(tree, tex, x + 220, y)
            tree.links.new(flipped.outputs["Color"], normal_map.inputs["Color"])
        else:
            tree.links.new(tex.outputs["Color"], normal_map.inputs["Color"])
        tree.links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
        y -= 280

    if "emission" in roles:
        _, image = roles["emission"]
        tex = _image_node(tree, image, x, y, non_color=False)
        emission_color = bsdf.inputs.get("Emission Color") or bsdf.inputs.get("Emission")
        if emission_color is not None:
            tree.links.new(tex.outputs["Color"], emission_color)
        strength = bsdf.inputs.get("Emission Strength")
        if strength is not None:
            strength.default_value = 1.0

    _apply_alpha_mode(material, kind.get("alpha") or "OPAQUE")


def _image_node(tree, image, x, y, *, non_color: bool):
    node = tree.nodes.new("ShaderNodeTexImage")
    node.location = (x, y)
    node.image = image
    if non_color:
        try:
            node.image.colorspace_settings.name = "Non-Color"
        except Exception:
            pass
    return node


def _flip_green(tree, tex_node, x, y):
    separate = tree.nodes.new("ShaderNodeSeparateColor")
    separate.location = (x, y)
    invert = tree.nodes.new("ShaderNodeMath")
    invert.operation = "SUBTRACT"
    invert.location = (x + 140, y - 40)
    invert.inputs[0].default_value = 1.0
    combine = tree.nodes.new("ShaderNodeCombineColor")
    combine.location = (x + 280, y)
    tree.links.new(tex_node.outputs["Color"], separate.inputs["Color"])
    tree.links.new(separate.outputs["Red"], combine.inputs["Red"])
    tree.links.new(separate.outputs["Green"], invert.inputs[1])
    tree.links.new(invert.outputs["Value"], combine.inputs["Green"])
    tree.links.new(separate.outputs["Blue"], combine.inputs["Blue"])
    return combine


def _apply_alpha_mode(material: bpy.types.Material, mode: str) -> None:
    mode = (mode or "OPAQUE").upper()
    blend = {
        "OPAQUE": "OPAQUE",
        "CLIP": "CLIP",
        "HASHED": "HASHED",
        "DITHERED": "HASHED",
        "BLEND": "BLEND",
    }.get(mode, "OPAQUE")
    if hasattr(material, "blend_method"):
        material.blend_method = blend
    if hasattr(material, "surface_render_method"):
        try:
            material.surface_render_method = "BLENDED" if mode == "BLEND" else "DITHERED"
        except TypeError:
            pass
