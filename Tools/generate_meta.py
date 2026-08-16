#!/usr/bin/env python3
"""Generate Unity .meta files for everything under Assets/.

Unity creates these on first import, but committing them means GUIDs are stable
across clones, so scene and asset references never break. GUIDs are derived from
the asset path, which makes the whole set reproducible: regenerating never
changes an existing id.

Usage:  python3 Tools/generate_meta.py [project_root]
"""

import hashlib
import os
import sys

SKIP_DIRS = {".git", "Library", "Temp", "Obj", "obj", "bin", "Build", "Logs", "UserSettings"}

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:{trailing}
  assetBundleName:{trailing}
  assetBundleVariant:{trailing}
"""

SCRIPT_META = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:{trailing}
  assetBundleName:{trailing}
  assetBundleVariant:{trailing}
"""

ASMDEF_META = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData:{trailing}
  assetBundleName:{trailing}
  assetBundleVariant:{trailing}
"""

DEFAULT_META = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData:{trailing}
  assetBundleName:{trailing}
  assetBundleVariant:{trailing}
"""

TEMPLATES = {
    ".cs": SCRIPT_META,
    ".asmdef": ASMDEF_META,
    ".asmref": ASMDEF_META,
}

# Unity writes a trailing space after these keys; matching it avoids a diff on
# every reimport.
TRAILING = " "


def guid_for(rel_path: str) -> str:
    """Stable 32-hex GUID derived from the asset's project-relative path."""
    digest = hashlib.md5(("glowpulse:" + rel_path.replace(os.sep, "/")).encode("utf-8"))
    return digest.hexdigest()


def write_meta(path: str, rel: str, template: str) -> bool:
    meta_path = path + ".meta"
    if os.path.exists(meta_path):
        return False
    with open(meta_path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(template.format(guid=guid_for(rel), trailing=TRAILING))
    return True


def main() -> int:
    root = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else ".")
    assets = os.path.join(root, "Assets")
    if not os.path.isdir(assets):
        print(f"No Assets folder at {assets}", file=sys.stderr)
        return 1

    created = 0
    for dirpath, dirnames, filenames in os.walk(assets):
        dirnames[:] = sorted(d for d in dirnames if d not in SKIP_DIRS)

        for name in dirnames:
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, root)
            created += write_meta(full, rel, FOLDER_META)

        for name in sorted(filenames):
            if name.endswith(".meta"):
                continue
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, root)
            ext = os.path.splitext(name)[1].lower()
            created += write_meta(full, rel, TEMPLATES.get(ext, DEFAULT_META))

    # Drop .meta files whose asset is gone, which is what Unity would do.
    removed = 0
    for dirpath, dirnames, filenames in os.walk(assets):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for name in filenames:
            if not name.endswith(".meta"):
                continue
            asset = os.path.join(dirpath, name[:-5])
            if not os.path.exists(asset):
                os.remove(os.path.join(dirpath, name))
                removed += 1

    print(f"meta files: {created} created, {removed} removed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
