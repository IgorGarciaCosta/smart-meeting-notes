"""
Smart Meeting Notes — Transcription module
Uses Faster Whisper to transcribe audio files locally.
"""

import sys
import time
from pathlib import Path
from faster_whisper import WhisperModel


def transcribe_to_dict(audio_path: str, model_size: str = "medium", device: str = "cpu") -> dict:
    """Transcribe an audio file and return structured data.

    Returns:
        dict with keys: text, segments, language, language_probability, duration_seconds
    """
    path = Path(audio_path)
    if not path.exists():
        raise FileNotFoundError(f"Arquivo '{audio_path}' não encontrado.")

    model = WhisperModel(model_size, device=device, compute_type="int8")
    segments_gen, info = model.transcribe(
        str(path), beam_size=5, language="pt")

    t0 = time.time()
    segments_list = []
    full_text = []
    for segment in segments_gen:
        text = segment.text.strip()
        segments_list.append({
            "start": round(segment.start, 2),
            "end": round(segment.end, 2),
            "text": text,
        })
        full_text.append(text)

    elapsed = time.time() - t0

    return {
        "text": " ".join(full_text),
        "segments": segments_list,
        "language": info.language,
        "language_probability": round(info.language_probability, 2),
        "duration_seconds": round(elapsed, 1),
    }


def transcribe(audio_path: str, model_size: str = "medium", device: str = "cpu"):
    """Transcribe an audio file and print timestamped segments (CLI)."""
    path = Path(audio_path)
    if not path.exists():
        print(f"Erro: arquivo '{audio_path}' não encontrado.")
        sys.exit(1)

    print(f"Carregando modelo '{model_size}' no device '{device}'...")
    t0 = time.time()
    result = transcribe_to_dict(audio_path, model_size, device)
    load_time = time.time() - t0

    print(f"Modelo carregado + transcrito em {load_time:.1f}s\n")
    print(
        f"Idioma detectado: {result['language']} (prob: {result['language_probability']})\n")

    print("-" * 60)
    for seg in result["segments"]:
        print(f"[{seg['start']:7.2f}s -> {seg['end']:7.2f}s] {seg['text']}")

    print("-" * 60)
    print(f"Transcrição completa em {result['duration_seconds']:.1f}s")
    print(f"\n--- TEXTO COMPLETO ---\n")
    print(result["text"])


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Uso: python transcribe.py <arquivo_de_audio> [modelo] [device]")
        print("  modelo: tiny | small | medium | large-v3  (default: medium)")
        print("  device: cpu | cuda                        (default: cpu)")
        print("\nExemplo: python transcribe.py reuniao.mp3 medium cpu")
        sys.exit(0)

    audio = sys.argv[1]
    model_name = sys.argv[2] if len(sys.argv) > 2 else "medium"
    dev = sys.argv[3] if len(sys.argv) > 3 else "cpu"

    transcribe(audio, model_name, dev)
