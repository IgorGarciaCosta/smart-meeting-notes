"""
Scan local machine for available GGUF model files.
Searches:
  1. HuggingFace hub cache (~/.cache/huggingface/hub)
  2. Custom directories specified via environment variable GGUF_MODELS_DIR

Outputs JSON list of found models.
"""

import json
import os
import sys
from pathlib import Path


def scan_huggingface_cache() -> list[dict]:
    """Scan the HuggingFace cache for downloaded GGUF files."""
    models = []

    # Standard HuggingFace cache locations
    hf_cache = os.environ.get("HF_HOME", "")
    if not hf_cache:
        hf_cache = os.environ.get("HUGGINGFACE_HUB_CACHE", "")
    if not hf_cache:
        hf_cache = str(Path.home() / ".cache" / "huggingface" / "hub")

    cache_path = Path(hf_cache)
    if not cache_path.exists():
        return models

    for gguf_file in cache_path.rglob("*.gguf"):
        try:
            size_bytes = gguf_file.stat().st_size
            size_gb = round(size_bytes / (1024 ** 3), 2)

            # Try to extract repo name from path
            # Typical path: .cache/huggingface/hub/models--Org--Repo/snapshots/hash/file.gguf
            parts = gguf_file.parts
            repo_name = ""
            for part in parts:
                if part.startswith("models--"):
                    repo_name = part.replace("models--", "").replace("--", "/")
                    break

            models.append({
                "path": str(gguf_file),
                "filename": gguf_file.name,
                "repo": repo_name,
                "sizeGb": size_gb,
                "source": "huggingface_cache",
            })
        except OSError:
            continue

    return models


def scan_custom_directory(directory: str) -> list[dict]:
    """Scan a custom directory for GGUF files."""
    models = []
    dir_path = Path(directory)

    if not dir_path.exists():
        return models

    for gguf_file in dir_path.rglob("*.gguf"):
        try:
            size_bytes = gguf_file.stat().st_size
            size_gb = round(size_bytes / (1024 ** 3), 2)

            models.append({
                "path": str(gguf_file),
                "filename": gguf_file.name,
                "repo": "",
                "sizeGb": size_gb,
                "source": "local_directory",
            })
        except OSError:
            continue

    return models


def scan_all() -> dict:
    """Scan all known locations for GGUF models."""
    models = []

    # 1. HuggingFace cache
    models.extend(scan_huggingface_cache())

    # 2. Custom directory from env var
    custom_dir = os.environ.get("GGUF_MODELS_DIR", "")
    if custom_dir:
        models.extend(scan_custom_directory(custom_dir))

    # 3. Common local model directories (Windows)
    common_dirs = [
        Path.home() / "models",
        Path.home() / "llm-models",
        Path.home() / ".local" / "share" / "models",
    ]
    for d in common_dirs:
        if d.exists():
            models.extend(scan_custom_directory(str(d)))

    # Deduplicate by path
    seen = set()
    unique_models = []
    for m in models:
        if m["path"] not in seen:
            seen.add(m["path"])
            unique_models.append(m)

    return {"models": unique_models, "count": len(unique_models)}


if __name__ == "__main__":
    result = scan_all()
    print(json.dumps(result, ensure_ascii=False))
