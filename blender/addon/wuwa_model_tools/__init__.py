bl_info = {
    "name": "WuWa Model Tools",
    "author": "NiHaiMa123",
    "version": (0, 1, 0),
    "blender": (4, 5, 0),
    "location": "3D View > Sidebar > WuWa",
    "description": "WuWa-specific post-import setup and validation for UEFormat assets",
    "category": "Import-Export",
}

try:
    from . import operators, ui
except ImportError:
    operators = None
    ui = None


def register():
    if operators is None or ui is None:
        raise ImportError("wuwa_model_tools requires Blender's bpy to register")
    operators.register()
    ui.register()


def unregister():
    if operators is None or ui is None:
        return
    ui.unregister()
    operators.unregister()
