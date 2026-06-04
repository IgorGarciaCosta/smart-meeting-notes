"""
Smart Meeting Notes — Analysis module
Uses llama-cpp-python to run a Qwen model locally for transcript analysis.
"""

import sys
import json
import time
from pathlib import Path

from huggingface_hub import hf_hub_download
from llama_cpp import Llama

# Default model — quantized Qwen2.5-7B (single-file, works on CPU)
DEFAULT_REPO = "Qwen/Qwen2.5-7B-Instruct-GGUF"
DEFAULT_FILE = "qwen2.5-7b-instruct-q3_k_m.gguf"

ANALYSIS_PROMPT = """You are a meeting analysis assistant. Analyze the following meeting transcript and extract:

1. **Summary**: A concise technical summary of the meeting (2-4 paragraphs).
2. **Action Items**: A list of tasks/actions with responsible people if mentioned.
3. **Decisions**: Key decisions that were made during the meeting.
4. **Pending Questions**: Unresolved questions or topics that need follow-up.

Respond ONLY with valid JSON in this exact format (no markdown, no extra text):
{
  "summary": "...",
  "actionItems": ["..."],
  "decisions": ["..."],
  "pendingQuestions": ["..."]
}

If the transcript is in Portuguese, respond in Portuguese.

TRANSCRIPT:
"""

_model_cache: Llama | None = None


def _get_model(repo: str = DEFAULT_REPO, filename: str = DEFAULT_FILE, n_ctx: int = 8192) -> Llama:
    """Download (if needed) and load the GGUF model."""
    global _model_cache
    if _model_cache is not None:
        return _model_cache

    model_path = hf_hub_download(repo_id=repo, filename=filename)

    _model_cache = Llama(
        model_path=model_path,
        n_ctx=n_ctx,
        n_threads=4,
        verbose=False,
    )
    return _model_cache


def analyze_transcript(transcript: str, repo: str = DEFAULT_REPO, filename: str = DEFAULT_FILE) -> dict:
    """Analyze a meeting transcript and return structured data."""
    t0 = time.time()

    llm = _get_model(repo, filename)

    response = llm.create_chat_completion(
        messages=[
            {"role": "user", "content": ANALYSIS_PROMPT + transcript}
        ],
        temperature=0.3,
        response_format={"type": "json_object"},
    )

    content = response["choices"][0]["message"]["content"]
    elapsed = time.time() - t0

    result = json.loads(content)

    # Ensure all expected keys exist
    result.setdefault("summary", "")
    result.setdefault("actionItems", [])
    result.setdefault("decisions", [])
    result.setdefault("pendingQuestions", [])
    result["duration_seconds"] = round(elapsed, 1)

    return result


def analyze_transcript_local(transcript: str, model_path: str) -> dict:
    """Analyze using a local GGUF file path directly (no HuggingFace download)."""
    t0 = time.time()

    llm = Llama(
        model_path=model_path,
        n_ctx=8192,
        n_threads=4,
        verbose=False,
    )

    response = llm.create_chat_completion(
        messages=[
            {"role": "user", "content": ANALYSIS_PROMPT + transcript}
        ],
        temperature=0.3,
        response_format={"type": "json_object"},
    )

    content = response["choices"][0]["message"]["content"]
    elapsed = time.time() - t0

    result = json.loads(content)
    result.setdefault("summary", "")
    result.setdefault("actionItems", [])
    result.setdefault("decisions", [])
    result.setdefault("pendingQuestions", [])
    result["duration_seconds"] = round(elapsed, 1)

    return result


def analyze_transcript_openai(transcript: str, endpoint: str, model: str) -> dict:
    """Analyze using an OpenAI-compatible endpoint (Ollama, LM Studio, etc.)."""
    import urllib.request
    import urllib.error

    t0 = time.time()

    url = f"{endpoint.rstrip('/')}/chat/completions"
    payload = json.dumps({
        "model": model,
        "messages": [
            {"role": "user", "content": ANALYSIS_PROMPT + transcript}
        ],
        "temperature": 0.3,
        "response_format": {"type": "json_object"},
    }).encode("utf-8")

    req = urllib.request.Request(
        url,
        data=payload,
        headers={"Content-Type": "application/json"},
    )

    try:
        with urllib.request.urlopen(req, timeout=300) as resp:
            response_data = json.loads(resp.read().decode("utf-8"))
    except urllib.error.URLError as e:
        raise RuntimeError(f"Failed to reach endpoint {url}: {e}")

    content = response_data["choices"][0]["message"]["content"]
    elapsed = time.time() - t0

    result = json.loads(content)
    result.setdefault("summary", "")
    result.setdefault("actionItems", [])
    result.setdefault("decisions", [])
    result.setdefault("pendingQuestions", [])
    result["duration_seconds"] = round(elapsed, 1)

    return result


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Analyze meeting transcripts with local LLMs")
    parser.add_argument("--json", dest="json_mode", action="store_true", help="Output JSON")
    parser.add_argument("source", nargs="?", help="Transcript file path or '-' for stdin")
    parser.add_argument("repo", nargs="?", default=DEFAULT_REPO, help="HuggingFace repo ID")
    parser.add_argument("filename", nargs="?", default=DEFAULT_FILE, help="GGUF filename in repo")
    parser.add_argument("--local", dest="local_path", help="Path to a local GGUF model file")
    parser.add_argument("--openai-endpoint", dest="openai_endpoint", help="OpenAI-compatible endpoint URL")
    parser.add_argument("--openai-model", dest="openai_model", default="", help="Model name for OpenAI-compatible endpoint")

    args = parser.parse_args()

    if not args.json_mode:
        parser.print_help()
        sys.exit(0)

    if not args.source:
        print("Error: --json requires <transcript_file> or '-' for stdin", file=sys.stderr)
        sys.exit(1)

    # Read transcript
    if args.source == "-":
        transcript_text = sys.stdin.read()
    else:
        transcript_path = Path(args.source)
        if not transcript_path.exists():
            print(json.dumps({"error": f"File not found: {args.source}"}), file=sys.stderr)
            sys.exit(1)
        transcript_text = transcript_path.read_text(encoding="utf-8")

    try:
        if args.openai_endpoint:
            result = analyze_transcript_openai(transcript_text, args.openai_endpoint, args.openai_model)
        elif args.local_path:
            result = analyze_transcript_local(transcript_text, args.local_path)
        else:
            result = analyze_transcript(transcript_text, args.repo, args.filename)
        print(json.dumps(result, ensure_ascii=False))
    except Exception as e:
        print(json.dumps({"error": str(e)}), file=sys.stderr)
        sys.exit(1)
