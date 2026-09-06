"""Operators for importing a wuwa2blender export manifest."""

from __future__ import annotations

import bpy
from bpy.props import BoolProperty, StringProperty
from bpy.types import Operator
from bpy_extras.io_utils import ImportHelper

from . import pipeline
from .manifest_io import default_material_profile_path


class WUWA_OT_import_manifest(Operator, ImportHelper):
    bl_idname = "wuwa.import_manifest"
    bl_label = "Import WuWa Manifest"
    bl_description = "Import a wuwa2blender export manifest via UEFormat, then apply WuWa PBR setup"
    bl_options = {"REGISTER", "UNDO"}

    filename_ext = ".json"
    filter_glob: StringProperty(default="*.json", options={"HIDDEN"})
    save_path: StringProperty(name="Save .blend", subtype="FILE_PATH", default="")
    profile_path: StringProperty(name="Material profile", subtype="FILE_PATH", default="")
    pack_images: BoolProperty(name="Pack images", default=True)

    def execute(self, context):
        manifest = self.filepath
        if not manifest:
            self.report({"ERROR"}, "No manifest selected")
            return {"CANCELLED"}
        from pathlib import Path

        manifest_path = Path(manifest)
        save_path = self.save_path or str(manifest_path.with_name(manifest_path.stem + ".blend"))
        profile_path = self.profile_path
        if not profile_path:
            found = default_material_profile_path(manifest_path)
            if found is None:
                self.report({"ERROR"}, "Material profile 3x.json not found")
                return {"CANCELLED"}
            profile_path = str(found)
        report_path = str(Path(save_path).with_suffix(".validation.json"))
        try:
            report = pipeline.run(
                manifest,
                save_path,
                profile_path,
                report_path,
                pack_images=self.pack_images,
            )
        except Exception as exc:
            self.report({"ERROR"}, str(exc))
            return {"CANCELLED"}
        errors = report.get("errors") or []
        if errors:
            self.report({"WARNING"}, errors[0])
        else:
            self.report({"INFO"}, f"Imported and saved {save_path}")
        return {"FINISHED"}


classes = (WUWA_OT_import_manifest,)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
