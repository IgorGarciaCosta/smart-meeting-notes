"""
Smart Meeting Notes — Whisper FastAPI Service
Exposes transcription as an HTTP endpoint for the ASP.NET Core backend.

Run: uvicorn transcriber.api:app --host 0.0.0.0 --port 8001
"""

import tempfile
import shutil
from pathlib import Path

from fastapi import FastAPI, UploadFile, File, HTTPException

from transcriber.transcribe import transcribe_to_dict

app = FastAPI(title="Whisper Transcription Service", version="1.0.0")


@app.get("/health")
def health():
    return {"status": "ok"}


@app.post("/transcribe")
async def transcribe_endpoint(
    file: UploadFile = File(...),
    model: str = "medium",
    device: str = "cpu",
):
    """Receive an audio file and return the transcription as JSON."""
    suffix = Path(file.filename).suffix if file.filename else ".wav"

    with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as tmp:
        shutil.copyfileobj(file.file, tmp)
        tmp_path = tmp.name

    try:
        result = transcribe_to_dict(tmp_path, model_size=model, device=device)
        return result
    except FileNotFoundError as exc:
        raise HTTPException(status_code=400, detail=str(exc))
    except Exception as exc:
        raise HTTPException(
            status_code=500, detail=f"Transcription failed: {exc}")
    finally:
        Path(tmp_path).unlink(missing_ok=True)
