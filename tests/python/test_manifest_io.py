"""Blender-independent unit tests for wuwa_model_tools.manifest_io.

Run: python tests/python/test_manifest_io.py
Does not import bpy and does not read game assets.
"""

from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ADDON = ROOT / "blender" / "addon"
if str(ADDON) not in sys.path:
    sys.path.insert(0, str(ADDON))

from wuwa_model_tools.manifest_io import (  # noqa: E402
    default_material_profile_path,
    kind_for_name,
    load_json,
    object_name,
    resolve_staging_file,
    role_for_parameter,
    snapshot_for_material,
    texture_file_map,
)

PROFILE = ROOT / "config" / "material-profiles" / "3x.json"
SMOKE = ROOT / "tests" / "fixtures" / "ueformat-smoke" / "manifest.json"


class ManifestIoTests(unittest.TestCase):
    def setUp(self) -> None:
        self.profile = load_json(PROFILE)

    def test_profile_roles_and_kinds(self) -> None:
        self.assertEqual("wuwa-3x", self.profile["profileId"])
        self.assertEqual("baseColor", role_for_parameter(self.profile, "MainTex"))
        self.assertEqual("baseColor", role_for_parameter(self.profile, "PM_Diffuse"))
        self.assertEqual("normal", role_for_parameter(self.profile, "PM_Normals"))
        self.assertEqual("emission", role_for_parameter(self.profile, "EM"))
        self.assertIsNone(role_for_parameter(self.profile, "NotARealParameter"))
        self.assertEqual("hair", kind_for_name(self.profile, "MI_SmokeHair")["kind"])
        self.assertTrue(kind_for_name(self.profile, "MI_SmokeHair")["useBaseColorAlpha"])
        self.assertEqual("HASHED", kind_for_name(self.profile, "MI_R2T1JinxiMd10011Bangs")["alpha"])
        self.assertTrue(kind_for_name(self.profile, "MI_R2T1JinxiMd10011Hair_OL")["skip"])
        self.assertEqual("body", kind_for_name(self.profile, "MI_UnknownThing")["kind"])

    def test_object_name_and_staging_paths(self) -> None:
        self.assertEqual("SmokeCube", object_name("/Game/WuwaSmoke/SmokeCube.SmokeCube"))
        self.assertEqual("MI_SmokeHair", object_name("Client/Content/WuwaSmoke/MI_SmokeHair"))
        staging = Path("C:/staging")
        self.assertEqual(staging / "Game/a.png", resolve_staging_file(staging, "Game\\a.png"))
        self.assertIsNone(resolve_staging_file(staging, None))

    def test_snapshot_matching_by_slot_and_path(self) -> None:
        manifest = {
            "materials": [
                {"slotName": "Hair", "objectPath": "/Game/WuwaSmoke/MI_SmokeHair"},
                {"slotName": "Body", "objectPath": "/Game/WuwaSmoke/MI_SmokeBody"},
            ],
            "materialParameters": [
                {
                    "materialObjectPath": "/Game/WuwaSmoke/MI_SmokeHair",
                    "slotName": "Hair",
                    "textures": {"MainTex": "/Game/WuwaSmoke/T_SmokeDiffuse"},
                }
            ],
            "textures": [
                {"objectPath": "/Game/WuwaSmoke/T_SmokeDiffuse", "file": "T_SmokeDiffuse.png"}
            ],
        }
        hair = snapshot_for_material(manifest, "MI_SmokeHair")
        self.assertIsNotNone(hair)
        assert hair is not None
        self.assertEqual("/Game/WuwaSmoke/T_SmokeDiffuse", hair["textures"]["MainTex"])
        body = snapshot_for_material(manifest, "Body")
        self.assertIsNotNone(body)
        mapped = texture_file_map(manifest, Path("/tmp/stage"))
        self.assertEqual(Path("/tmp/stage") / "T_SmokeDiffuse.png", mapped["/Game/WuwaSmoke/T_SmokeDiffuse"])

    def test_default_material_profile_path_finds_repo_profile(self) -> None:
        found = default_material_profile_path(SMOKE)
        self.assertIsNotNone(found)
        assert found is not None
        self.assertEqual(PROFILE.resolve(), found.resolve())
        self.assertTrue(found.is_file())

    def test_smoke_manifest_if_present(self) -> None:
        if not SMOKE.is_file():
            self.skipTest("ueformat-smoke fixture not generated yet")
        manifest = load_json(SMOKE)
        self.assertEqual("ueformat-smoke", manifest["jobId"])
        self.assertNotIn("0x", json.dumps(manifest).lower())
        self.assertEqual("SmokeCube", object_name(manifest["sourceObjectPath"]))
        hair = snapshot_for_material(manifest, "MI_SmokeHair")
        self.assertIsNotNone(hair)


if __name__ == "__main__":
    unittest.main()
