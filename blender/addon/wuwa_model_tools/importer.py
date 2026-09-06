"""Call the installed UEFormat add-on. Do not reimplement .uemodel parsing."""

from __future__ import annotations

import os
import sys
from pathlib import Path
from typing import Any


class UeFormatMissingError(RuntimeError):
    pass


def _enable_ueformat() -> None:
    try:
        import addon_utils

        try:
            addon_utils.enable(
                "bl_ext.user_default.io_scene_ueformat",
                default_set=False,
                persistent=False,
            )
        except Exception:
            pass
    except Exception:
        pass

    appdata = os.environ.get("APPDATA")
    if not appdata:
        return
    root = Path(appdata) / "Blender Foundation" / "Blender"
    if not root.is_dir():
        return
    for version_dir in root.iterdir():
        package = version_dir / "extensions" / "user_default" / "io_scene_ueformat"
        if package.is_dir():
            parent = str(package.parent)
            if parent not in sys.path:
                sys.path.insert(0, parent)


def _load_ueformat():
    _enable_ueformat()
    try:
        from io_scene_ueformat.importer.import_context import UEFormatImport
        from io_scene_ueformat.options import UEModelOptions

        return UEFormatImport, UEModelOptions
    except ImportError:
        pass

    try:
        from bl_ext.user_default.io_scene_ueformat.importer.import_context import (
            UEFormatImport,
        )
        from bl_ext.user_default.io_scene_ueformat.options import UEModelOptions

        return UEFormatImport, UEModelOptions
    except ImportError as exc:
        raise UeFormatMissingError(
            "UEFormat Blender add-on is not available. Install "
            "https://github.com/h4lfheart/UEFormat and re-run doctor."
        ) from exc


def import_uemodel(uemodel_path: Path, profile: dict[str, Any]):
    import_opts = profile.get("import") or {}
    UEFormatImport, UEModelOptions = _load_ueformat()
    options = UEModelOptions(
        link=True,
        scale_factor=float(import_opts.get("scaleFactor", 0.01)),
        bone_length=float(import_opts.get("boneLength", 4.0)),
        reorient_bones=bool(import_opts.get("reorientBones", False)),
        import_collision=False,
        import_sockets=bool(import_opts.get("importSockets", True)),
        import_morph_targets=bool(import_opts.get("importMorphTargets", True)),
        import_virtual_bones=False,
        target_lod=int(import_opts.get("targetLod", 0)),
    )
    created = UEFormatImport(options).import_file(uemodel_path)
    return created
