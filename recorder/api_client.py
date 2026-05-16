"""
HTTP client for the Smart Meeting Notes API.
Handles meeting creation, chunk upload, finalization, and status polling.
"""

import time
from pathlib import Path

import httpx


DEFAULT_BASE_URL = "http://localhost:5117"
MAX_RETRIES = 3
RETRY_DELAY = 2  # seconds


class ApiError(Exception):
    """Raised when an API call fails after all retries."""

    def __init__(self, message: str, status_code: int | None = None):
        super().__init__(message)
        self.status_code = status_code


class MeetingApiClient:
    """Client for the Smart Meeting Notes chunked upload API."""

    def __init__(self, base_url: str = DEFAULT_BASE_URL, timeout: float = 60.0):
        self.base_url = base_url.rstrip("/")
        self.timeout = timeout

    def _request(self, method: str, path: str, **kwargs) -> httpx.Response:
        """Make an HTTP request with retries and exponential backoff."""
        url = f"{self.base_url}{path}"
        kwargs.setdefault("timeout", self.timeout)

        last_exc = None
        for attempt in range(MAX_RETRIES):
            try:
                with httpx.Client() as client:
                    response = client.request(method, url, **kwargs)

                if response.status_code >= 500:
                    raise ApiError(
                        f"Server error {response.status_code}: {response.text}",
                        response.status_code,
                    )
                return response

            except (httpx.ConnectError, httpx.TimeoutException, ApiError) as e:
                last_exc = e
                if attempt < MAX_RETRIES - 1:
                    delay = RETRY_DELAY * (2 ** attempt)
                    time.sleep(delay)

        raise ApiError(f"Request failed after {MAX_RETRIES} retries: {last_exc}")

    def create_meeting(self, title: str = "Untitled Meeting") -> str:
        """Create a new meeting for chunked upload.

        Returns the meeting ID (GUID string).
        """
        response = self._request("POST", "/api/meetings", json={"title": title})

        if response.status_code not in (200, 201):
            raise ApiError(
                f"Failed to create meeting: {response.text}",
                response.status_code,
            )

        data = response.json()
        return data["meetingId"]

    def upload_chunk(self, meeting_id: str, chunk_index: int, wav_path: str) -> dict:
        """Upload an audio chunk for a meeting.

        Args:
            meeting_id: The meeting GUID.
            chunk_index: Zero-based index of the chunk.
            wav_path: Path to the WAV file to upload.

        Returns:
            Response dict with meetingId, chunkIndex, status.
        """
        path = Path(wav_path)
        if not path.exists():
            raise FileNotFoundError(f"Chunk file not found: {wav_path}")

        with open(wav_path, "rb") as f:
            files = {"audio": (path.name, f, "audio/wav")}
            data = {"chunkIndex": str(chunk_index)}
            response = self._request(
                "POST",
                f"/api/meetings/{meeting_id}/chunks",
                files=files,
                data=data,
            )

        if response.status_code not in (200, 202):
            raise ApiError(
                f"Failed to upload chunk {chunk_index}: {response.text}",
                response.status_code,
            )

        return response.json()

    def finalize_meeting(self, meeting_id: str) -> dict:
        """Finalize a meeting after all chunks are uploaded and transcribed.

        Returns the response dict.
        """
        response = self._request("POST", f"/api/meetings/{meeting_id}/finalize")

        if response.status_code not in (200, 202):
            raise ApiError(
                f"Failed to finalize meeting: {response.text}",
                response.status_code,
            )

        return response.json()

    def get_meeting(self, meeting_id: str) -> dict:
        """Get the current state of a meeting.

        Returns the full meeting dict including status, chunks, transcript, analysis.
        """
        response = self._request("GET", f"/api/meetings/{meeting_id}")

        if response.status_code == 404:
            raise ApiError(f"Meeting {meeting_id} not found", 404)

        if response.status_code != 200:
            raise ApiError(
                f"Failed to get meeting: {response.text}",
                response.status_code,
            )

        return response.json()

    def wait_for_transcription(
        self, meeting_id: str, poll_interval: float = 5.0, max_wait: float = 300.0
    ) -> bool:
        """Poll until all chunks are transcribed.

        Args:
            meeting_id: The meeting GUID.
            poll_interval: Seconds between polls.
            max_wait: Maximum seconds to wait.

        Returns:
            True if all chunks are transcribed, False if timed out.
        """
        start = time.time()

        while time.time() - start < max_wait:
            meeting = self.get_meeting(meeting_id)
            chunks = meeting.get("chunks", [])

            if not chunks:
                time.sleep(poll_interval)
                continue

            all_done = all(c.get("status") == 2 for c in chunks)  # Transcribed = 2
            any_failed = any(c.get("status") == 3 for c in chunks)  # Failed = 3

            if all_done:
                return True

            if any_failed:
                failed = [c for c in chunks if c.get("status") == 3]
                raise ApiError(
                    f"{len(failed)} chunk(s) failed transcription: "
                    + ", ".join(str(c.get("chunkIndex")) for c in failed)
                )

            time.sleep(poll_interval)

        return False
