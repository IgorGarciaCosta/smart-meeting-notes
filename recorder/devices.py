"""
Audio device listing and selection utilities.
Supports microphone input and WASAPI loopback (system audio) on Windows.
"""

import sys
import sounddevice as sd


def get_input_devices() -> list[dict]:
    """Return a list of available input (microphone) devices."""
    devices = sd.query_devices()
    input_devices = []
    for i, dev in enumerate(devices):
        if dev["max_input_channels"] > 0:
            input_devices.append({
                "index": i,
                "name": dev["name"],
                "channels": dev["max_input_channels"],
                "samplerate": int(dev["default_samplerate"]),
                "hostapi": sd.query_hostapis(dev["hostapi"])["name"],
            })
    return input_devices


def get_loopback_devices() -> list[dict]:
    """Return WASAPI loopback devices (Windows only).

    These capture system audio output (what you hear from speakers/headphones).
    """
    if sys.platform != "win32":
        return []

    devices = sd.query_devices()
    hostapis = sd.query_hostapis()

    # Find WASAPI host API index
    wasapi_index = None
    for i, api in enumerate(hostapis):
        if "WASAPI" in api["name"]:
            wasapi_index = i
            break

    if wasapi_index is None:
        return []

    loopback_devices = []
    for i, dev in enumerate(devices):
        # WASAPI loopback devices appear as output devices that can be opened as input
        if dev["hostapi"] == wasapi_index and dev["max_output_channels"] > 0:
            loopback_devices.append({
                "index": i,
                "name": f"[Loopback] {dev['name']}",
                "channels": dev["max_output_channels"],
                "samplerate": int(dev["default_samplerate"]),
                "hostapi": "Windows WASAPI",
                "is_loopback": True,
            })
    return loopback_devices


def list_all_devices() -> list[dict]:
    """List all available audio capture devices (mic + loopback)."""
    devices = get_input_devices() + get_loopback_devices()
    return devices


def print_devices(devices: list[dict]) -> None:
    """Print devices in a numbered list for user selection."""
    if not devices:
        print("  (nenhum dispositivo encontrado)")
        return

    for i, dev in enumerate(devices):
        print(f"  [{i}] {dev['name']}")
        print(
            f"      Canais: {dev['channels']} | Taxa: {dev['samplerate']} Hz | API: {dev['hostapi']}")


def select_device(devices: list[dict] | None = None) -> dict:
    """Interactive prompt to select an audio device.

    Returns the selected device dict with keys: index, name, channels, samplerate, hostapi.
    """
    if devices is None:
        devices = list_all_devices()

    if not devices:
        print("Nenhum dispositivo de áudio encontrado!")
        sys.exit(1)

    print("\n=== Dispositivos de áudio disponíveis ===\n")
    print_devices(devices)
    print()

    if len(devices) == 1:
        print(f"Usando único dispositivo disponível: {devices[0]['name']}")
        return devices[0]

    while True:
        try:
            choice = input(
                f"Selecione o dispositivo [0-{len(devices)-1}] (Enter = 0): ").strip()
            if choice == "":
                choice = 0
            else:
                choice = int(choice)
            if 0 <= choice < len(devices):
                return devices[choice]
            print(f"  Opção inválida. Escolha entre 0 e {len(devices)-1}.")
        except ValueError:
            print("  Digite um número válido.")
        except (KeyboardInterrupt, EOFError):
            print("\nCancelado.")
            sys.exit(0)


if __name__ == "__main__":
    print("Listando dispositivos de áudio...\n")
    all_devices = list_all_devices()
    print_devices(all_devices)
