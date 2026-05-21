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
            chunk_data = self._resample(
                chunk_data, self.device_samplerate, TARGET_SAMPLERATE)

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


class MixedAudioCapture:
    """Captures audio from TWO devices simultaneously (e.g. mic + loopback) and mixes them.

    Both streams are independently resampled to 16kHz, then summed together.
    Produces WAV chunks identical to AudioCapture output.

    Usage:
        capture = MixedAudioCapture(
            mic_device_index=0, mic_samplerate=44100,
            loopback_device_index=5, loopback_samplerate=48000,
            chunk_duration=30)
        capture.start()
        for wav_path in capture.chunks():
            upload(wav_path)
        capture.stop()
    """

    def __init__(
        self,
        mic_device_index: int,
        mic_samplerate: int,
        loopback_device_index: int,
        loopback_samplerate: int,
        mic_channels: int = 1,
        loopback_channels: int = 2,
        chunk_duration: int = 30,
    ):
        self.mic_device_index = mic_device_index
        self.mic_samplerate = mic_samplerate
        self.loopback_device_index = loopback_device_index
        self.loopback_samplerate = loopback_samplerate
        self.mic_channels = mic_channels
        self.loopback_channels = loopback_channels
        self.chunk_duration = chunk_duration

        # Samples per chunk at TARGET rate (16kHz)
        self._samples_per_chunk = TARGET_SAMPLERATE * self.chunk_duration

        # Separate buffers for each stream (stored as resampled mono 16kHz)
        self._mic_buffer: list[np.ndarray] = []
        self._mic_samples = 0
        self._loopback_buffer: list[np.ndarray] = []
        self._loopback_samples = 0
        self._lock = threading.Lock()

        # Queue of completed chunk file paths
        self._chunk_queue: queue.Queue[str | None] = queue.Queue()

        # Stream handles
        self._mic_stream: sd.InputStream | None = None
        self._loopback_stream: sd.InputStream | None = None
        self._running = False
        self._temp_dir = Path(tempfile.mkdtemp(prefix="smn_mix_"))
        self._chunk_counter = 0

    def _to_mono_16k(self, data: np.ndarray, src_rate: int) -> np.ndarray:
        """Convert multi-channel audio to mono and resample to 16kHz."""
        # To mono
        if data.ndim > 1 and data.shape[1] > 1:
            data = data.mean(axis=1)
        elif data.ndim > 1:
            data = data.squeeze()

        # Resample
        if src_rate != TARGET_SAMPLERATE:
            divisor = gcd(src_rate, TARGET_SAMPLERATE)
            up = TARGET_SAMPLERATE // divisor
            down = src_rate // divisor
            data = resample_poly(data, up, down).astype(np.float32)

        return data

    def _mic_callback(self, indata: np.ndarray, frames: int, time_info, status):
        """Callback for microphone stream."""
        data = self._to_mono_16k(indata.copy(), self.mic_samplerate)

        with self._lock:
            self._mic_buffer.append(data)
            self._mic_samples += len(data)
            self._try_flush()

    def _loopback_callback(self, indata: np.ndarray, frames: int, time_info, status):
        """Callback for loopback stream."""
        data = self._to_mono_16k(indata.copy(), self.loopback_samplerate)

        with self._lock:
            self._loopback_buffer.append(data)
            self._loopback_samples += len(data)
            self._try_flush()

    def _try_flush(self):
        """Check if both buffers have enough samples for a chunk; if so, mix and flush.
        Must be called while holding self._lock.
        """
        # Only flush when BOTH buffers have enough samples for a full chunk
        if self._mic_samples >= self._samples_per_chunk and self._loopback_samples >= self._samples_per_chunk:
            self._flush_mixed_chunk()

    def _flush_mixed_chunk(self):
        """Mix mic and loopback buffers into a single chunk and save as WAV.
        Must be called while holding self._lock.
        """
        # Concatenate mic buffer
        mic_data = np.concatenate(self._mic_buffer, axis=0)
        if len(mic_data) > self._samples_per_chunk:
            mic_chunk = mic_data[:self._samples_per_chunk]
            self._mic_buffer = [mic_data[self._samples_per_chunk:]]
            self._mic_samples = len(self._mic_buffer[0])
        else:
            mic_chunk = mic_data
            self._mic_buffer = []
            self._mic_samples = 0

        # Concatenate loopback buffer
        loopback_data = np.concatenate(self._loopback_buffer, axis=0)
        if len(loopback_data) > self._samples_per_chunk:
            loopback_chunk = loopback_data[:self._samples_per_chunk]
            self._loopback_buffer = [loopback_data[self._samples_per_chunk:]]
            self._loopback_samples = len(self._loopback_buffer[0])
        else:
            loopback_chunk = loopback_data
            self._loopback_buffer = []
            self._loopback_samples = 0

        # Ensure same length (trim to shorter)
        min_len = min(len(mic_chunk), len(loopback_chunk))
        mic_chunk = mic_chunk[:min_len]
        loopback_chunk = loopback_chunk[:min_len]

        # Mix: average the two signals
        mixed = (mic_chunk + loopback_chunk) * 0.5
        mixed = np.clip(mixed, -1.0, 1.0)

        # Convert to int16 and save
        audio_int16 = (mixed * 32767).astype(np.int16)
        wav_path = str(self._temp_dir / f"chunk_{self._chunk_counter:04d}.wav")
        wavfile.write(wav_path, TARGET_SAMPLERATE, audio_int16)
        self._chunk_counter += 1

        self._chunk_queue.put(wav_path)

    def start(self):
        """Start recording from both devices simultaneously."""
        if self._running:
            return

        self._running = True

        # Loopback stream (WASAPI)
        try:
            loopback_settings = sd.WasapiSettings(exclusive=False)
        except AttributeError:
            loopback_settings = None

        self._loopback_stream = sd.InputStream(
            device=self.loopback_device_index,
            samplerate=self.loopback_samplerate,
            channels=self.loopback_channels,
            dtype="float32",
            callback=self._loopback_callback,
            blocksize=1024,
            extra_settings=loopback_settings,
        )

        # Mic stream (standard input)
        self._mic_stream = sd.InputStream(
            device=self.mic_device_index,
            samplerate=self.mic_samplerate,
            channels=self.mic_channels,
            dtype="float32",
            callback=self._mic_callback,
            blocksize=1024,
        )

        self._loopback_stream.start()
        self._mic_stream.start()

    def stop(self):
        """Stop both streams and flush remaining audio as a final mixed chunk."""
        if not self._running:
            return

        self._running = False

        for stream in (self._mic_stream, self._loopback_stream):
            if stream is not None:
                stream.stop()
                stream.close()

        self._mic_stream = None
        self._loopback_stream = None

        # Flush remaining buffers — mix whatever is available
        with self._lock:
            if self._mic_samples > 0 or self._loopback_samples > 0:
                self._flush_final_chunk()

        self._chunk_queue.put(None)

    def _flush_final_chunk(self):
        """Flush remaining audio from both buffers, padding the shorter one with silence.
        Must be called while holding self._lock.
        """
        mic_data = np.concatenate(
            self._mic_buffer, axis=0) if self._mic_buffer else np.array([], dtype=np.float32)
        loopback_data = np.concatenate(
            self._loopback_buffer, axis=0) if self._loopback_buffer else np.array([], dtype=np.float32)

        self._mic_buffer = []
        self._mic_samples = 0
        self._loopback_buffer = []
        self._loopback_samples = 0

        # Pad shorter buffer with silence
        max_len = max(len(mic_data), len(loopback_data))
        if max_len == 0:
            return

        if len(mic_data) < max_len:
            mic_data = np.pad(mic_data, (0, max_len - len(mic_data)))
        if len(loopback_data) < max_len:
            loopback_data = np.pad(
                loopback_data, (0, max_len - len(loopback_data)))

        # Mix
        mixed = (mic_data + loopback_data) * 0.5
        mixed = np.clip(mixed, -1.0, 1.0)

        audio_int16 = (mixed * 32767).astype(np.int16)
        wav_path = str(self._temp_dir / f"chunk_{self._chunk_counter:04d}.wav")
        wavfile.write(wav_path, TARGET_SAMPLERATE, audio_int16)
        self._chunk_counter += 1

        self._chunk_queue.put(wav_path)

    def chunks(self):
        """Generator that yields WAV file paths as mixed chunks become ready."""
        while True:
            path = self._chunk_queue.get()
            if path is None:
                break
            yield path

    @property
    def temp_dir(self) -> Path:
        """Directory where temporary chunk WAV files are stored."""
        return self._temp_dir
