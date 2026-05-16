"""
Smart Meeting Notes — Audio Recorder CLI

Captures audio from microphone or system audio (WASAPI loopback),
splits into chunks, and sends them to the Smart Meeting Notes API
for transcription and analysis.

Usage:
    python -m recorder.record
    python -m recorder.record --device 0 --title "Standup" --api http://localhost:5035
    python -m recorder.record --chunk-duration 30
"""

import argparse
import shutil
import sys
import threading
import time
from pathlib import Path

from recorder.api_client import ApiError, MeetingApiClient
from recorder.capture import AudioCapture
from recorder.devices import list_all_devices, print_devices, select_device


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Captura áudio e envia chunks para a API Smart Meeting Notes"
    )
    parser.add_argument(
        "--api",
        default="http://localhost:5117",
        help="URL base da API (default: http://localhost:5117)",
    )
    parser.add_argument(
        "--title",
        default=None,
        help="Título da reunião (se omitido, pergunta interativamente)",
    )
    parser.add_argument(
        "--device",
        type=int,
        default=None,
        help="Índice do dispositivo de áudio (se omitido, mostra lista para escolher)",
    )
    parser.add_argument(
        "--chunk-duration",
        type=int,
        default=30,
        help="Duração de cada chunk em segundos (default: 30)",
    )
    parser.add_argument(
        "--no-finalize",
        action="store_true",
        help="Não finalizar automaticamente (permite adicionar mais chunks depois)",
    )
    return parser.parse_args()


def upload_worker(
    capture: AudioCapture,
    client: MeetingApiClient,
    meeting_id: str,
    uploaded_chunks: list[int],
    error_event: threading.Event,
):
    """Background thread that uploads chunks as they become ready."""
    chunk_index = 0

    for wav_path in capture.chunks():
        try:
            print(f"  ⬆ Enviando chunk {chunk_index}...", end=" ", flush=True)
            client.upload_chunk(meeting_id, chunk_index, wav_path)
            uploaded_chunks.append(chunk_index)
            print(f"✓ ({Path(wav_path).stat().st_size // 1024} KB)")
            chunk_index += 1
        except ApiError as e:
            print(f"\n  ✗ Erro ao enviar chunk {chunk_index}: {e}")
            error_event.set()
            break
        except Exception as e:
            print(f"\n  ✗ Erro inesperado: {e}")
            error_event.set()
            break


def main():
    args = parse_args()

    print("\n╔══════════════════════════════════════════╗")
    print("║   Smart Meeting Notes — Audio Recorder   ║")
    print("╚══════════════════════════════════════════╝\n")

    # --- Device selection ---
    devices = list_all_devices()

    if args.device is not None:
        if args.device < 0 or args.device >= len(devices):
            print(f"Erro: dispositivo {args.device} não encontrado.")
            sys.exit(1)
        device = devices[args.device]
    else:
        device = select_device(devices)

    print(f"\n✓ Dispositivo: {device['name']}")
    print(f"  Taxa: {device['samplerate']} Hz | Canais: {device['channels']}")

    # --- Meeting title ---
    if args.title:
        title = args.title
    else:
        try:
            title = input(
                "\nTítulo da reunião (Enter = 'Untitled Meeting'): ").strip()
            if not title:
                title = "Untitled Meeting"
        except (KeyboardInterrupt, EOFError):
            print("\nCancelado.")
            sys.exit(0)

    # --- Create meeting via API ---
    client = MeetingApiClient(base_url=args.api)
    print(f"\nConectando à API ({args.api})...")

    try:
        meeting_id = client.create_meeting(title)
    except ApiError as e:
        print(f"Erro ao criar reunião: {e}")
        print("Verifique se a API está rodando.")
        sys.exit(1)

    print(f"✓ Reunião criada: {meeting_id}")
    print(f"  Título: {title}")
    print(f"  Chunk duration: {args.chunk_duration}s")

    # --- Setup capture ---
    is_loopback = device.get("is_loopback", False)
    capture = AudioCapture(
        device_index=device["index"],
        device_samplerate=device["samplerate"],
        # Max stereo, will be mixed to mono
        channels=min(device["channels"], 2),
        chunk_duration=args.chunk_duration,
        is_loopback=is_loopback,
    )

    # --- Wait for user to start ---
    print("\n" + "─" * 44)
    try:
        input("Pressione ENTER para iniciar gravação...")
    except (KeyboardInterrupt, EOFError):
        print("\nCancelado.")
        sys.exit(0)

    # --- Start recording ---
    capture.start()
    print("\n🔴 GRAVANDO... (pressione ENTER para parar)\n")

    uploaded_chunks: list[int] = []
    error_event = threading.Event()

    # Start upload worker thread
    upload_thread = threading.Thread(
        target=upload_worker,
        args=(capture, client, meeting_id, uploaded_chunks, error_event),
        daemon=True,
    )
    upload_thread.start()

    # --- Wait for user to stop ---
    try:
        input()
    except (KeyboardInterrupt, EOFError):
        pass

    # --- Stop recording ---
    print("\n⏹ Parando gravação...")
    capture.stop()

    # Wait for upload thread to finish
    upload_thread.join(timeout=60)

    if error_event.is_set():
        print("\n⚠ Ocorreram erros durante o upload. Verifique acima.")
        print(f"  Meeting ID: {meeting_id}")
        print(f"  Chunks enviados com sucesso: {len(uploaded_chunks)}")
        sys.exit(1)

    print(
        f"\n✓ Gravação finalizada. {len(uploaded_chunks)} chunk(s) enviado(s).")

    if len(uploaded_chunks) == 0:
        print("  Nenhum áudio foi capturado.")
        sys.exit(0)

    # --- Finalize ---
    if args.no_finalize:
        print(f"\n  Meeting ID: {meeting_id}")
        print("  (--no-finalize: reunião não foi finalizada automaticamente)")
    else:
        print("\nAguardando transcrição dos chunks...")
        try:
            all_transcribed = client.wait_for_transcription(
                meeting_id, poll_interval=5.0, max_wait=300.0
            )
            if all_transcribed:
                print("✓ Todos os chunks transcritos.")
                print("Finalizando reunião (análise com IA)...")
                client.finalize_meeting(meeting_id)
                print("✓ Reunião finalizada e em análise!")
            else:
                print("⚠ Timeout aguardando transcrição.")
                print("  Você pode finalizar manualmente depois:")
                print(f"  POST {args.api}/api/meetings/{meeting_id}/finalize")
        except ApiError as e:
            print(f"⚠ Erro: {e}")
            print(
                f"  Finalize manualmente: POST {args.api}/api/meetings/{meeting_id}/finalize")

    # --- Cleanup temp files ---
    try:
        shutil.rmtree(capture.temp_dir, ignore_errors=True)
    except Exception:
        pass

    print(f"\n{'─' * 44}")
    print(f"Meeting ID: {meeting_id}")
    print(f"Status:     GET {args.api}/api/meetings/{meeting_id}")
    print(f"{'─' * 44}\n")


if __name__ == "__main__":
    main()
