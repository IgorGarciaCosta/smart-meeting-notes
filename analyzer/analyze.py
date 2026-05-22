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


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(
            "Usage: python -m analyzer.analyze --json <transcript_file> [repo] [filename]", file=sys.stderr)
        print(
            "       python -m analyzer.analyze --json - [repo] [filename]  (read from stdin)", file=sys.stderr)
        sys.exit(0)

    if sys.argv[1] == "--json":
        if len(sys.argv) < 3:
            print(
                "Error: --json requires <transcript_file> or '-' for stdin", file=sys.stderr)
            sys.exit(1)

        source = sys.argv[2]
        repo = sys.argv[3] if len(sys.argv) > 3 else DEFAULT_REPO
        filename = sys.argv[4] if len(sys.argv) > 4 else DEFAULT_FILE

        if source == "-":
            transcript_text = sys.stdin.read()
        else:
            transcript_path = Path(source)
            if not transcript_path.exists():
                print(json.dumps(
                    {"error": f"File not found: {source}"}), file=sys.stderr)
                sys.exit(1)
            transcript_text = transcript_path.read_text(encoding="utf-8")

        try:
            result = analyze_transcript(transcript_text, repo, filename)
            print(json.dumps(result, ensure_ascii=False))
        except Exception as e:
            print(json.dumps({"error": str(e)}), file=sys.stderr)
            sys.exit(1)
    else:
        print("Unknown flag. Use --json", file=sys.stderr)
        sys.exit(1)
