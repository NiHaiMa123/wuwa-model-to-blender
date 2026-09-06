"""Headless and GUI entry for: manifest → UEFormat import → PBR → validate → save."""

from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Any

import bpy

from . import importer, materials, rigging, validation
from .importer import UeFormatMissingError
from .manifest_io import load_json, resolve_staging_file, staging_root

TOOL_VERSION = "0.1.0-p7"


def run(
    manifest_path: str | Path,
    save_path: str | Path,
    profile_path: str | Path,
    report_path: str | Path,
    pack_images: bool = True,
) -> dict[str, Any]:
    manifest_path = Path(manifest_path).resolve()
    save_path = Path(save_path).resolve()
    profile_path = Path(profile_path).resolve()
    report_path = Path(report_path).resolve()
    errors: list[str] = []
    warnings: list[str] = []

    if not manifest_path.is_file():
        raise FileNotFoundError(f"manifest not found: {manifest_path}")
    if not profile_path.is_file():
        raise FileNotFoundError(f"material profile not found: {profile_path}")

    manifest = load_json(manifest_path)
    profile = load_json(profile_path)
    staging = staging_root(manifest_path)
    job_id = str(manifest.get("jobId") or save_path.stem)
    uemodel = resolve_staging_file(staging, (manifest.get("mesh") or {}).get("ueModel"))
    if uemodel is None or not uemodel.is_file():
        raise FileNotFoundError(f"uemodel missing under {staging}")

    for relative in manifest.get("files") or []:
        candidate = resolve_staging_file(staging, relative)
        if candidate is None or not candidate.is_file():
            warnings.append(f"manifest file missing: {relative}")

    _clear_default_scene()
    print(f"WUWA import {uemodel}", flush=True)
    try:
        importer.import_uemodel(uemodel, profile)
    except UeFormatMissingError:
        raise
    except Exception as exc:
        errors.append(f"UEFormat import failed: {type(exc).__name__}: {exc}")
        report = _write_report(
            manifest_path,
            save_path,
            profile,
            report_path,
            saved=False,
            reopened_clean=False,
            scene=validation.collect_scene_stats(),
            errors=errors,
            warnings=warnings,
        )
        return report

    print("WUWA materials", flush=True)
    materials.apply_materials(manifest, staging, profile, warnings)
    rigging.organize(job_id)

    if pack_images:
        _pack_images(warnings)

    scene = validation.collect_scene_stats()
    check_errors, check_warnings = validation.validate_against_manifest(manifest, scene)
    errors.extend(check_errors)
    warnings.extend(check_warnings)

    save_path.parent.mkdir(parents=True, exist_ok=True)
    print(f"WUWA save {save_path}", flush=True)
    blend_path = str(save_path).replace("\\", "/")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    saved = save_path.is_file()
    if not saved:
        errors.append(f"Blender did not write {save_path}")

    reopened_clean = False
    if saved:
        bpy.ops.wm.open_mainfile(filepath=blend_path, load_ui=False)
        missing = validation.missing_image_names()
        reopened_clean = len(missing) == 0
        if missing:
            errors.append("reopen missing files: " + ", ".join(missing[:12]))
        scene = validation.collect_scene_stats()

    return _write_report(
        manifest_path,
        save_path,
        profile,
        report_path,
        saved=saved,
        reopened_clean=reopened_clean,
        scene=scene,
        errors=errors,
        warnings=warnings,
    )


def _clear_default_scene() -> None:
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)


def _pack_images(warnings: list[str]) -> None:
    for image in bpy.data.images:
        if image.packed_file:
            continue
        if not image.filepath:
            continue
        abs_path = bpy.path.abspath(image.filepath)
        if abs_path and os.path.isfile(abs_path):
            try:
                image.use_fake_user = True
                image.pack()
            except Exception as exc:
                warnings.append(f"failed to pack {image.name}: {exc}")


def _write_report(
    manifest_path: Path,
    save_path: Path,
    profile: dict[str, Any],
    report_path: Path,
    *,
    saved: bool,
    reopened_clean: bool,
    scene: dict[str, Any],
    errors: list[str],
    warnings: list[str],
) -> dict[str, Any]:
    report = {
        "schemaVersion": "1",
        "toolVersion": TOOL_VERSION,
        "manifestPath": str(manifest_path),
        "blendPath": str(save_path),
        "profileId": profile.get("profileId") or "",
        "saved": saved,
        "reopenedClean": reopened_clean,
        "scene": scene,
        "errors": errors,
        "warnings": warnings,
    }
    report_path.parent.mkdir(parents=True, exist_ok=True)
    with report_path.open("w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=2, ensure_ascii=False)
    print(f"WUWA_BLEND_REPORT {report_path}", flush=True)
    return report
