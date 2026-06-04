import { useCallback, useEffect, useRef, useState } from "react";
import { useAudioRecorder } from "../hooks/useAudioRecorder.ts";
import {
  createMeeting,
  uploadChunk,
  finalizeMeeting,
  getMeeting,
} from "../api/meetings.ts";
import { MeetingStatus } from "../api/types.ts";
import ModelStatusPanel from "../components/ModelStatusPanel.tsx";

export default function RecordPage() {
  const [title, setTitle] = useState("");
  const [chunkDuration, setChunkDuration] = useState(30);
  const [selectedDevice, setSelectedDevice] = useState("");
  const [captureSystemAudio, setCaptureSystemAudio] = useState(false);
  const [meetingId, setMeetingId] = useState<string | null>(null);
  const [chunksUploaded, setChunksUploaded] = useState(0);
  const [elapsed, setElapsed] = useState(0);
  const [status, setStatus] = useState<string>("");
  const [finalizing, setFinalizing] = useState(false);

  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const recorder = useAudioRecorder({ chunkDuration });

  useEffect(() => {
    if (recorder.devices.length > 0 && !selectedDevice) {
      setSelectedDevice(recorder.devices[0].deviceId);
    }
  }, [recorder.devices, selectedDevice]);

  const handleChunk = useCallback(
    async (blob: Blob, index: number) => {
      if (!meetingId) return;
      try {
        await uploadChunk(meetingId, index, blob, `chunk_${index}.webm`);
        setChunksUploaded((c) => c + 1);
      } catch (e) {
        setStatus(`Chunk ${index} upload error: ${e}`);
      }
    },
    [meetingId],
  );

  useEffect(() => {
    recorder.onChunk.current = handleChunk;
  }, [handleChunk, recorder.onChunk]);

  const handleStart = async () => {
    if (!selectedDevice) {
      setStatus("Select an audio device");
      return;
    }

    setStatus("Creating meeting...");
    try {
      const res = await createMeeting(title || "Untitled Meeting");
      setMeetingId(res.meetingId);
      setChunksUploaded(0);
      setElapsed(0);
      setStatus("");

      if (captureSystemAudio) {
        recorder.startWithSystemAudio(selectedDevice);
      } else {
        recorder.start(selectedDevice);
      }

      timerRef.current = setInterval(() => {
        setElapsed((e) => e + 1);
      }, 1000);
    } catch (e) {
      setStatus(`Error starting: ${e}`);
    }
  };

  const handleStop = async () => {
    recorder.stop();

    if (timerRef.current) {
      clearInterval(timerRef.current);
      timerRef.current = null;
    }

    if (!meetingId) return;

    setFinalizing(true);
    setStatus("Waiting for chunk transcription to finalize...");

    const pollAndFinalize = async () => {
      const maxAttempts = 60;
      for (let i = 0; i < maxAttempts; i++) {
        await new Promise((r) => setTimeout(r, 5000));
        try {
          const result = await finalizeMeeting(meetingId);
          if (
            result.status === MeetingStatus.Finalizing ||
            result.status === MeetingStatus.AwaitingChunks
          ) {
            setStatus(`Waiting for transcription... (${i + 1}/${maxAttempts})`);
            continue;
          }
          // Finalize accepted — now poll until analysis completes
          setStatus("Analysis in progress...");
          break;
        } catch (e) {
          const msg = String(e);
          if (
            msg.includes("cannot finalize") ||
            msg.includes("Failed") ||
            msg.includes("cannot upload") ||
            msg.includes("failed transcription")
          ) {
            // If already finalizing/analyzing, move to the status polling phase
            if (
              msg.includes("Finalizing") ||
              msg.includes("Analyzing") ||
              msg.includes("Completed")
            ) {
              setStatus("Analysis in progress...");
              break;
            }
            setStatus(`Error: ${msg}`);
            setFinalizing(false);
            return;
          }
          setStatus(`Erro ao finalizar: ${msg}`);
          setFinalizing(false);
          return;
        }
      }

      // Poll meeting status until Completed or Failed
      for (let i = 0; i < maxAttempts; i++) {
        await new Promise((r) => setTimeout(r, 5000));
        try {
          const meeting = await getMeeting(meetingId);
          if (meeting.status === "Completed") {
            setStatus("Meeting finalized and analysis complete!");
            setFinalizing(false);
            return;
          }
          if (meeting.status === "Failed") {
            setStatus(`Analysis error: ${meeting.errorMessage || "unknown"}`);
            setFinalizing(false);
            return;
          }
          setStatus(`Analysis in progress... (${i + 1}/${maxAttempts})`);
        } catch (e) {
          setStatus(`Error checking status: ${e}`);
          setFinalizing(false);
          return;
        }
      }
      setStatus("Timeout — check the meeting status manually.");
      setFinalizing(false);
    };

    pollAndFinalize();
  };

  const formatTime = (seconds: number) => {
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = seconds % 60;
    if (h > 0) {
      return `${h.toString().padStart(2, "0")}:${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
    }
    return `${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
  };

  return (
    <div className="page">
      <div className="page-header">
        <h1>New Recording</h1>
      </div>

      <ModelStatusPanel />

      {!recorder.permissionGranted && (
        <div className="alert alert--warning">
          <div>
            <strong>Microphone access required</strong>
            <p style={{ marginTop: 4 }}>
              Grant permission to list available audio devices.
            </p>
            <button
              className="btn btn--primary"
              style={{ marginTop: 8 }}
              onClick={recorder.requestPermission}
            >
              Allow
            </button>
          </div>
        </div>
      )}

      {recorder.error && (
        <div className="alert alert--error">
          <span>{recorder.error}</span>
        </div>
      )}

      <div className="card">
        <div className="card-header">
          <span className="card-title">Settings</span>
        </div>

        <div className="form-group">
          <label className="form-label">Audio Device</label>
          <select
            className="form-select"
            value={selectedDevice}
            onChange={(e) => setSelectedDevice(e.target.value)}
            disabled={recorder.isRecording}
          >
            {recorder.devices.length === 0 && (
              <option value="">No device found</option>
            )}
            {recorder.devices.map((d) => (
              <option key={d.deviceId} value={d.deviceId}>
                {d.label}
              </option>
            ))}
          </select>
        </div>

        <div className="form-row">
          <div className="form-group">
            <label className="form-label">Meeting Title</label>
            <input
              className="form-input"
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="Ex: Daily Standup, Sprint Review..."
              disabled={recorder.isRecording}
            />
          </div>

          <div className="form-group">
            <label className="form-label">Chunk (s)</label>
            <input
              className="form-input"
              type="number"
              value={chunkDuration}
              onChange={(e) => setChunkDuration(Number(e.target.value) || 30)}
              min={10}
              max={120}
              disabled={recorder.isRecording}
            />
          </div>
        </div>

        {recorder.supportsSystemAudio && (
          <div className="form-group" style={{ paddingTop: 4 }}>
            <label
              className="form-label"
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
                cursor: "pointer",
              }}
            >
              <input
                type="checkbox"
                checked={captureSystemAudio}
                onChange={(e) => setCaptureSystemAudio(e.target.checked)}
                disabled={recorder.isRecording}
              />
              Capture system audio (mic + system sound)
            </label>
            {captureSystemAudio && (
              <p
                style={{
                  fontSize: "0.85em",
                  opacity: 0.7,
                  margin: "4px 0 0 26px",
                }}
              >
                The browser will ask permission to share system audio.
              </p>
            )}
          </div>
        )}

        <div
          style={{ display: "flex", justifyContent: "center", paddingTop: 8 }}
        >
          <button
            className={`btn ${recorder.isRecording ? "btn--stop" : "btn--record"}`}
            onClick={recorder.isRecording ? handleStop : handleStart}
            disabled={finalizing}
          >
            {recorder.isRecording ? "Stop" : "Start Recording"}
          </button>
        </div>
      </div>

      {(recorder.isRecording || meetingId) && (
        <div className="recording-panel">
          {recorder.isRecording && (
            <div className="recording-timer">
              <span className="recording-dot" />
              {formatTime(elapsed)}
            </div>
          )}

          <div className="recording-stats">
            <div className="recording-stat">
              <div className="recording-stat-value">{chunksUploaded}</div>
              <div className="recording-stat-label">Chunks sent</div>
            </div>
            <div className="recording-stat">
              <div className="recording-stat-value">{chunkDuration}s</div>
              <div className="recording-stat-label">Por chunk</div>
            </div>
          </div>

          {meetingId && <div className="recording-id">ID: {meetingId}</div>}
        </div>
      )}

      {status && <p className="status-text">{status}</p>}
    </div>
  );
}
