"""
Check availability of AI models used by Smart Meeting Notes.
Outputs JSON with status of each model.
"""

import json
import sys
from pathlib import Path


def check_whisper(model_size: str) -> dict:
    """Check if the faster-whisper model is available."""
    try:
        import faster_whisper  # noqa: F401
    except ImportError:
        return {"name": "Whisper", "model": model_size, "available": False, "reason": "faster-whisper não instalado"}

    # Check if model is cached (huggingface hub cache)
    try:
        from huggingface_hub import try_to_load_from_cache
        # faster-whisper models are stored as ctranslate2 repos
        repo_map = {
            "large-v3": "Systran/faster-whisper-large-v3",
            "distil-large-v3": "Systran/faster-distil-whisper-large-v3",
            "medium": "Systran/faster-whisper-medium",
            "small": "Systran/faster-whisper-small",
            "base": "Systran/faster-whisper-base",
            "tiny": "Systran/faster-whisper-tiny",
        }
        repo = repo_map.get(model_size, f"Systran/faster-whisper-{model_size}")
        result = try_to_load_from_cache(repo, "model.bin")
        if result is not None:
            return {"name": "Whisper", "model": model_size, "available": True}
        else:
            return {"name": "Whisper", "model": model_size, "available": False, "reason": f"Modelo '{model_size}' não encontrado no cache (será baixado no primeiro uso)"}
    except Exception:
        # If we can't check cache, assume available since package is installed
        return {"name": "Whisper", "model": model_size, "available": True}


def check_qwen(repo: str, filename: str) -> dict:
    """Check if the Qwen GGUF model is available."""
    try:
        import llama_cpp  # noqa: F401
    except ImportError:
        return {"name": "Qwen", "model": filename, "available": False, "reason": "llama-cpp-python não instalado"}

    try:
        from huggingface_hub import try_to_load_from_cache
        result = try_to_load_from_cache(repo, filename)
        if result is not None:
            return {"name": "Qwen", "model": filename, "available": True}
        else:
            return {"name": "Qwen", "model": filename, "available": False, "reason": f"Modelo '{filename}' não encontrado no cache (será baixado no primeiro uso)"}
    except Exception:
        return {"name": "Qwen", "model": filename, "available": True}


def main():
    whisper_model = sys.argv[1] if len(sys.argv) > 1 else "large-v3"
    qwen_repo = sys.argv[2] if len(sys.argv) > 2 else "Qwen/Qwen2.5-7B-Instruct-GGUF"
    qwen_file = sys.argv[3] if len(sys.argv) > 3 else "qwen2.5-7b-instruct-q3_k_m.gguf"

    results = [
        check_whisper(whisper_model),
        check_qwen(qwen_repo, qwen_file),
    ]

    print(json.dumps(results))


if __name__ == "__main__":
    main()
