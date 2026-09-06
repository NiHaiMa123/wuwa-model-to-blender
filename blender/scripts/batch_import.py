"""Headless Blender entry for P4.

Invocation:
  blender --background --python blender/scripts/batch_import.py -- `
    --manifest work/exports/.../manifest.json `
    --save work/blend/Character.blend `
    --profile config/material-profiles/3x.json `
    --report work/blend/Character.validation.json
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

SCRIPT_PATH = Path(__file__).resolve()
REPO_ROOT = SCRIPT_PATH.parents[2]
ADDON_DIR = REPO_ROOT / "blender" / "addon"
if str(ADDON_DIR) not in sys.path:
    sys.path.insert(0, str(ADDON_DIR))

from wuwa_model_tools.importer import UeFormatMissingError  # noqa: E402
from wuwa_model_tools.pipeline import run  # noqa: E402


def _argv_after_double_dash(argv: list[str]) -> list[str]:
    if "--" in argv:
        return argv[argv.index("--") + 1 :]
    return argv[1:]


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Import a wuwa2blender export manifest in Blender")
    parser.add_argument("--manifest", required=True)
    parser.add_argument("--save", required=True)
    parser.add_argument("--profile", required=True)
    parser.add_argument("--report", required=True)
    parser.add_argument("--pack", action=argparse.BooleanOptionalAction, default=True)
    args = parser.parse_args(_argv_after_double_dash(argv if argv is not None else sys.argv))

    try:
        report = run(
            args.manifest,
            args.save,
            args.profile,
            args.report,
            pack_images=bool(args.pack),
        )
    except UeFormatMissingError as exc:
        print(f"ERROR {exc}", file=sys.stderr, flush=True)
        return 2
    except FileNotFoundError as exc:
        print(f"ERROR {exc}", file=sys.stderr, flush=True)
        return 2
    except Exception as exc:
        print(f"ERROR {type(exc).__name__}: {exc}", file=sys.stderr, flush=True)
        return 1

    errors = report.get("errors") or []
    for warning in report.get("warnings") or []:
        print(f"WARN {warning}", flush=True)
    for error in errors:
        print(f"ERROR {error}", flush=True)
    scene = report.get("scene") or {}
    print(
        "WUWA scene "
        f"mesh={scene.get('meshName')} verts={scene.get('vertices')} "
        f"faces={scene.get('faces')} slots={scene.get('materialSlots')} "
        f"morphs={scene.get('morphTargets')} bones={scene.get('bones')} "
        f"images={scene.get('boundImages')} missing={scene.get('missingImages')} "
        f"saved={report.get('saved')} reopen={report.get('reopenedClean')}",
        flush=True,
    )
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
