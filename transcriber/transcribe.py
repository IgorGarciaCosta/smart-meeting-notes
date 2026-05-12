"""
Smart Meeting Notes — Transcription module
Uses Faster Whisper to transcribe audio files locally.
"""

import sys
import time
from pathlib import Path
from faster_whisper import WhisperModel


def transcribe(audio_path: str, model_size: str = "medium", device: str = "cpu"):
    """Transcribe an audio file and print timestamped segments."""
    path = Path(audio_path)
    if not path.exists():
        print(f"Erro: arquivo '{audio_path}' não encontrado.")
        sys.exit(1)

    print(f"Carregando modelo '{model_size}' no device '{device}'...")
    t0 = time.time()
    model = WhisperModel(model_size, device=device, compute_type="int8")
    print(f"Modelo carregado em {time.time() - t0:.1f}s\n")

    print(f"Transcrevendo: {path.name}")
    print("-" * 60)

    t0 = time.time()
    segments, info = model.transcribe(str(path), beam_size=5, language="pt")

    print(
        f"Idioma detectado: {info.language} (prob: {info.language_probability:.2f})\n")

    full_text = []
    for segment in segments:
        line = f"[{segment.start:7.2f}s -> {segment.end:7.2f}s] {segment.text.strip()}"
        print(line)
        full_text.append(segment.text.strip())

    elapsed = time.time() - t0
    print("-" * 60)
    print(f"Transcrição completa em {elapsed:.1f}s")
    print(f"\n--- TEXTO COMPLETO ---\n")
    print(" ".join(full_text))


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
