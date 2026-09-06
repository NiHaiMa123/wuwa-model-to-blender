"""P4 keeps the imported UEFormat skeleton. No Rigify, no auto mesh edits."""

from __future__ import annotations

import bpy


def organize(job_id: str) -> None:
    name = f"WuWa_{job_id or 'character'}"
    collection = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if collection.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(collection)
    for obj in list(bpy.context.scene.objects):
        if obj.type not in {"MESH", "ARMATURE"}:
            continue
        if obj.name not in collection.objects:
            collection.objects.link(obj)
