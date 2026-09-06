"""Validate committed JSON against schemas. No bpy, no game assets.

Run: python tests/python/test_schemas.py
Full draft 2020-12 checks require the jsonschema package (CI installs it).
Without jsonschema, required-key checks still run.
"""

from __future__ import annotations

import json
import unittest
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[2]
SCHEMAS = ROOT / "schemas"
CONFIG = ROOT / "config"
SMOKE_MANIFEST = ROOT / "tests" / "fixtures" / "ueformat-smoke" / "manifest.json"

try:
    import jsonschema
    from jsonschema.validators import Draft202012Validator
except ImportError:  # pragma: no cover - CI installs jsonschema
    jsonschema = None
    Draft202012Validator = None


def _load(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


class SchemaTests(unittest.TestCase):
    def _validate(self, instance: Any, schema: dict[str, Any], label: str) -> None:
        required = schema.get("required") or []
        if isinstance(instance, dict):
            for key in required:
                self.assertIn(key, instance, f"{label} missing required {key}")
        if jsonschema is None or Draft202012Validator is None:
            return
        Draft202012Validator.check_schema(schema)
        errors = sorted(
            Draft202012Validator(schema).iter_errors(instance),
            key=lambda err: list(err.absolute_path),
        )
        messages = [
            f"{'/'.join(str(part) for part in err.absolute_path) or '<root>'}: {err.message}"
            for err in errors
        ]
        self.assertEqual([], messages, f"{label} failed schema validation")

    def test_export_manifest_smoke_fixture(self) -> None:
        schema = _load(SCHEMAS / "export-manifest.schema.json")
        manifest = _load(SMOKE_MANIFEST)
        self._validate(manifest, schema, "ueformat-smoke/manifest.json")
        dumped = json.dumps(manifest)
        self.assertNotIn("0x", dumped.lower())
        self.assertNotIn("jinxi", dumped.lower())
        self.assertEqual("ueformat-smoke", manifest["jobId"])

    def test_material_profiles(self) -> None:
        schema = _load(SCHEMAS / "material-profile.schema.json")
        profiles = [
            CONFIG / "material-profiles" / "3x.json",
            CONFIG / "material-profiles" / "3x.example.json",
            CONFIG / "material-profiles" / "legacy.example.json",
        ]
        for path in profiles:
            self._validate(_load(path), schema, str(path.relative_to(ROOT)))

    def test_run_job_minimal_instance(self) -> None:
        schema = _load(SCHEMAS / "run-job.schema.json")
        instance = {
            "schemaVersion": "1",
            "toolVersion": "0.1.0-p7",
            "jobId": "schema-check",
            "assetInput": "/Game/WuwaSmoke/SmokeCube.SmokeCube",
            "overallStatus": "pass",
            "stages": [{"id": "ResolveConfig", "status": "pass"}],
        }
        self._validate(instance, schema, "synthetic run-job")
        invalid = dict(instance)
        invalid["overallStatus"] = "nope"
        if jsonschema is not None:
            errors = list(Draft202012Validator(schema).iter_errors(invalid))
            self.assertTrue(errors, "invalid overallStatus should fail the schema")


if __name__ == "__main__":
    unittest.main()
