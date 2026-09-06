"""3D View sidebar for the WuWa manifest importer."""

from __future__ import annotations

import bpy
from bpy.types import Panel


class WUWA_PT_panel(Panel):
    bl_label = "WuWa Model Tools"
    bl_idname = "WUWA_PT_panel"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "WuWa"

    def draw(self, context):
        layout = self.layout
        layout.operator("wuwa.import_manifest", text="Import manifest.json")


classes = (WUWA_PT_panel,)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
