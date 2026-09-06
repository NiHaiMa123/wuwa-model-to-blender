"""Load export manifests and material profiles. No bpy import."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any


def default_material_profile_path(start: Path | None = None) -> Path | None:
    """Resolve 3x.json from a packed add-on, a repo checkout, or a parent of *start*."""
    candidates: list[Path] = []
    here = Path(__file__).resolve().parent
    candidates.append(here / "profiles" / "3x.json")
    roots: list[Path] = []
    if start is not None:
        resolved = start.resolve()
        roots.append(resolved)
        roots.extend(resolved.parents)
    roots.extend(here.parents)
    for root in roots:
        candidates.append(root / "config" / "material-profiles" / "3x.json")
    seen: set[Path] = set()
    for path in candidates:
        if path in seen:
            continue
        seen.add(path)
        if path.is_file():
            return path
    return None


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        data = json.load(handle)
    if not isinstance(data, dict):
        raise ValueError(f"{path} did not contain a JSON object")
    return data


def staging_root(manifest_path: Path) -> Path:
    return manifest_path.parent


def resolve_staging_file(staging: Path, relative: str | None) -> Path | None:
    if not relative:
        return None
    return staging / relative.replace("\\", "/")


def object_name(object_path: str | None) -> str:
    if not object_path:
        return ""
    trimmed = object_path.replace("\\", "/").rstrip("/")
    leaf = trimmed.split("/")[-1]
    if "." in leaf:
        return leaf.split(".")[-1]
    return leaf


def texture_file_map(manifest: dict[str, Any], staging: Path) -> dict[str, Path]:
    mapping: dict[str, Path] = {}
    for texture in manifest.get("textures") or []:
        object_path = texture.get("objectPath") or ""
        file_path = resolve_staging_file(staging, texture.get("file"))
        if object_path and file_path is not None:
            mapping[object_path] = file_path
    return mapping


def snapshot_for_material(
    manifest: dict[str, Any],
    material_name: str,
) -> dict[str, Any] | None:
    materials = manifest.get("materials") or []
    snapshots = manifest.get("materialParameters") or []
    matched = None
    for info in materials:
        name = object_name(info.get("objectPath"))
        slot = info.get("slotName") or ""
        if material_name == name or material_name == slot:
            matched = info
            break
        if name and (material_name.endswith(name) or name.endswith(material_name)):
            matched = info
            break
    if matched is None:
        return None
    object_path = matched.get("objectPath") or ""
    for snapshot in snapshots:
        if (snapshot.get("materialObjectPath") or "") == object_path:
            return snapshot
        slot = snapshot.get("slotName")
        if slot and slot == matched.get("slotName"):
            return snapshot
    return {
        "materialObjectPath": object_path,
        "slotName": matched.get("slotName"),
        "textures": {},
        "scalars": {},
        "vectors": {},
    }


def role_for_parameter(profile: dict[str, Any], parameter_name: str) -> str | None:
    roles = profile.get("textureRoles") or {}
    needle = parameter_name.casefold()
    for role, names in roles.items():
        for name in names:
            if str(name).casefold() == needle:
                return role
    return None


def kind_for_name(profile: dict[str, Any], material_or_slot: str) -> dict[str, Any]:
    for rule in profile.get("slotKinds") or []:
        for token in rule.get("match") or []:
            if token and token.casefold() in material_or_slot.casefold():
                return rule
    return profile.get("defaultSlotKind") or {
        "kind": "body",
        "alpha": "OPAQUE",
        "useBaseColorAlpha": False,
        "skip": False,
    }
