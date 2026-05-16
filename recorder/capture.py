"""
Audio capture engine.
Records audio from a device in configurable chunks and signals when each chunk is ready.
"""

import io
import queue
import threading
import tempfile
from pathlib import Path

import numpy as np
import sounddevice as sd
from scipy.io import wavfile
from scipy.signal import resample_poly
from math import gcd


# Target sample rate for Whisper compatibility
TARGET_SAMPLERATE = 16000


class AudioCapture:
    """Captures audio from a device and produces WAV chunks of a fixed duration.

    Usage:
        capture = AudioCapture(device_index=0, device_samplerate=48000,
                               channels=1, chunk_duration=30)
        capture.start()
        # In another thread:
        for wav_path in capture.chunks():
            upload(wav_path)
        # When done:
        capture.stop()  # flushes final partial chunk
    """

    def __init__(
        self,
        device_index: int,
        device_samplerate: int,
        channels: int = 1,
        chunk_duration: int = 30,
        is_loopback: bool = False,
    ):
        self.device_index = device_index
        self.device_samplerate = device_samplerate
        self.channels = channels
        self.chunk_duration = chunk_duration
        self.is_loopback = is_loopback

        # Samples needed per chunk at device samplerate
        self._samples_per_chunk = self.device_samplerate * self.chunk_duration

        # Buffer to accumulate audio samples
        self._buffer: list[np.ndarray] = []
        self._buffer_samples = 0
        self._lock = threading.Lock()

        # Queue of completed chunk file paths
        self._chunk_queue: queue.Queue[str | None] = queue.Queue()

        # Stream handle
        self._stream: sd.InputStream | None = None
        self._running = False
        self._temp_dir = Path(tempfile.mkdtemp(prefix="smn_audio_"))

        self._chunk_counter = 0

    def _audio_callback(self, indata: np.ndarray, frames: int, time_info, status):
        """Called by sounddevice for each block of audio data."""
        if status:
            pass  # Ignore xrun warnings during capture

        # Copy data to avoid reference issues
        data = indata.copy()

        with self._lock:
            self._buffer.append(data)
            self._buffer_samples += frames

            if self._buffer_samples >= self._samples_per_chunk:
                self._flush_chunk()

    def _flush_chunk(self):
        """Save accumulated buffer as a WAV file and put path on the queue.
        Must be called while holding self._lock.
        """
        if self._buffer_samples == 0:
            return

        # Concatenate all buffered arrays
        audio_data = np.concatenate(self._buffer, axis=0)

        # If we have more samples than needed for one chunk, keep the remainder
        if audio_data.shape[0] > self._samples_per_chunk:
            chunk_data = audio_data[:self._samples_per_chunk]
            remainder = audio_data[self._samples_per_chunk:]
            self._buffer = [remainder]
            self._buffer_samples = remainder.shape[0]
        else:
            chunk_data = audio_data
            self._buffer = []
            self._buffer_samples = 0

        # Convert to mono if multi-channel
        if chunk_data.ndim > 1 and chunk_data.shape[1] > 1:
            chunk_data = chunk_data.mean(axis=1)
        elif chunk_data.ndim > 1:
            chunk_data = chunk_data.squeeze()

        # Resample to 16kHz if needed
        if self.device_samplerate != TARGET_SAMPLERATE:
            chunk_data = self._resample(chunk_data, self.device_samplerate, TARGET_SAMPLERATE)

        # Convert float32 [-1, 1] to int16
        chunk_data = np.clip(chunk_data, -1.0, 1.0)
        audio_int16 = (chunk_data * 32767).astype(np.int16)

        # Save to WAV file
        wav_path = str(self._temp_dir / f"chunk_{self._chunk_counter:04d}.wav")
        wavfile.write(wav_path, TARGET_SAMPLERATE, audio_int16)
        self._chunk_counter += 1

        self._chunk_queue.put(wav_path)

    @staticmethod
    def _resample(data: np.ndarray, src_rate: int, dst_rate: int) -> np.ndarray:
        """Resample audio using polyphase filtering."""
        if src_rate == dst_rate:
            return data
        divisor = gcd(src_rate, dst_rate)
        up = dst_rate // divisor
        down = src_rate // divisor
        return resample_poly(data, up, down).astype(np.float32)

    def start(self):
        """Start recording audio from the device."""
        if self._running:
            return

        self._running = True

        # For WASAPI loopback, open as loopback capture
        extra_settings = None
        if self.is_loopback:
            try:
                extra_settings = sd.WasapiSettings(exclusive=False)
            except AttributeError:
                pass  # Non-Windows or old sounddevice version

        self._stream = sd.InputStream(
            device=self.device_index,
            samplerate=self.device_samplerate,
            channels=self.channels,
            dtype="float32",
            callback=self._audio_callback,
            blocksize=1024,
            extra_settings=extra_settings,
        )
        self._stream.start()

    def stop(self):
        """Stop recording and flush any remaining audio as a final chunk."""
        if not self._running:
            return

        self._running = False

        if self._stream is not None:
            self._stream.stop()
            self._stream.close()
            self._stream = None

        # Flush remaining buffer
        with self._lock:
            if self._buffer_samples > 0:
                self._flush_chunk()

        # Signal end of chunks
        self._chunk_queue.put(None)

    def chunks(self):
        """Generator that yields WAV file paths as chunks become ready.

        Yields paths until stop() is called and all chunks are consumed.
        Blocks waiting for the next chunk.
        """
        while True:
            path = self._chunk_queue.get()
            if path is None:
                break
            yield path

    @property
    def temp_dir(self) -> Path:
        """Directory where temporary chunk WAV files are stored."""
        return self._temp_dir
