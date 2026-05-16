import { useCallback, useEffect, useRef, useState } from "react";
import { useAudioRecorder } from "../hooks/useAudioRecorder.ts";
import {
  createMeeting,
  uploadChunk,
  finalizeMeeting,
} from "../api/meetings.ts";

export default function RecordPage() {
  const [title, setTitle] = useState("");
  const [chunkDuration, setChunkDuration] = useState(30);
  const [selectedDevice, setSelectedDevice] = useState("");
  const [meetingId, setMeetingId] = useState<string | null>(null);
  const [chunksUploaded, setChunksUploaded] = useState(0);
  const [elapsed, setElapsed] = useState(0);
  const [status, setStatus] = useState<string>("");
  const [finalizing, setFinalizing] = useState(false);

  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const recorder = useAudioRecorder({ chunkDuration });

  // Set default device when devices load
  useEffect(() => {
    if (recorder.devices.length > 0 && !selectedDevice) {
      setSelectedDevice(recorder.devices[0].deviceId);
    }
  }, [recorder.devices, selectedDevice]);

  // Chunk upload handler
  const handleChunk = useCallback(
    async (blob: Blob, index: number) => {
      if (!meetingId) return;
      try {
        await uploadChunk(meetingId, index, blob, `chunk_${index}.webm`);
        setChunksUploaded((c) => c + 1);
      } catch (e) {
        setStatus(`Erro no upload do chunk ${index}: ${e}`);
      }
    },
    [meetingId],
  );

  // Wire chunk callback
  useEffect(() => {
    recorder.onChunk.current = handleChunk;
  }, [handleChunk, recorder.onChunk]);

  const handleStart = async () => {
    if (!selectedDevice) {
      setStatus("Selecione um dispositivo de áudio");
      return;
    }

    setStatus("Criando reunião...");
    try {
      const res = await createMeeting(title || "Untitled Meeting");
      setMeetingId(res.meetingId);
      setChunksUploaded(0);
      setElapsed(0);
      setStatus("Gravando...");

      recorder.start(selectedDevice);

      timerRef.current = setInterval(() => {
        setElapsed((e) => e + 1);
      }, 1000);
    } catch (e) {
      setStatus(`Erro ao iniciar: ${e}`);
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
    setStatus(
      "Parando gravação... Aguardando transcrição dos chunks para finalizar.",
    );

    // Poll until all chunks are transcribed, then finalize
    const pollAndFinalize = async () => {
      const maxAttempts = 60; // up to 5 minutes
      for (let i = 0; i < maxAttempts; i++) {
        await new Promise((r) => setTimeout(r, 5000));
        try {
          const result = await finalizeMeeting(meetingId);
          setStatus(`Reunião finalizada! ${result.message}`);
          setFinalizing(false);
          return;
        } catch (e) {
          const msg = String(e);
          if (msg.includes("Not all chunks are transcribed")) {
            setStatus(`Aguardando transcrição... (tentativa ${i + 1})`);
            continue;
          }
          // If meeting is no longer in AwaitingChunks, it may have already been finalized
          setStatus(`Erro ao finalizar: ${msg}`);
          setFinalizing(false);
          return;
        }
      }
      setStatus(
        "Timeout aguardando transcrição. Verifique o status da reunião manualmente.",
      );
      setFinalizing(false);
    };

    pollAndFinalize();
  };

  const formatTime = (seconds: number) => {
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
  };

  return (
    <div style={{ maxWidth: 600, margin: "0 auto", padding: 24 }}>
      <h1>🎙️ Gravar Reunião</h1>

      {!recorder.permissionGranted && (
        <div
          style={{
            marginBottom: 16,
            padding: 12,
            background: "#fff3cd",
            borderRadius: 6,
          }}
        >
          <p>Permissão de microfone necessária para listar dispositivos.</p>
          <button onClick={recorder.requestPermission}>
            Permitir Microfone
          </button>
        </div>
      )}

      {recorder.error && (
        <div
          style={{
            marginBottom: 16,
            padding: 12,
            background: "#f8d7da",
            borderRadius: 6,
            color: "#721c24",
          }}
        >
          {recorder.error}
        </div>
      )}

      <div style={{ marginBottom: 16 }}>
        <label style={{ display: "block", marginBottom: 4, fontWeight: 600 }}>
          Dispositivo de Áudio
        </label>
        <select
          value={selectedDevice}
          onChange={(e) => setSelectedDevice(e.target.value)}
          disabled={recorder.isRecording}
          style={{ width: "100%", padding: 8 }}
        >
          {recorder.devices.length === 0 && (
            <option value="">Nenhum dispositivo encontrado</option>
          )}
          {recorder.devices.map((d) => (
            <option key={d.deviceId} value={d.deviceId}>
              {d.label}
            </option>
          ))}
        </select>
      </div>

      <div style={{ marginBottom: 16 }}>
        <label style={{ display: "block", marginBottom: 4, fontWeight: 600 }}>
          Título da Reunião
        </label>
        <input
          type="text"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="Untitled Meeting"
          disabled={recorder.isRecording}
          style={{ width: "100%", padding: 8 }}
        />
      </div>

      <div style={{ marginBottom: 16 }}>
        <label style={{ display: "block", marginBottom: 4, fontWeight: 600 }}>
          Duração do Chunk (segundos)
        </label>
        <input
          type="number"
          value={chunkDuration}
          onChange={(e) => setChunkDuration(Number(e.target.value) || 30)}
          min={10}
          max={120}
          disabled={recorder.isRecording}
          style={{ width: 100, padding: 8 }}
        />
      </div>

      <div style={{ marginBottom: 16 }}>
        <button
          onClick={recorder.isRecording ? handleStop : handleStart}
          disabled={finalizing}
          style={{
            padding: "12px 32px",
            fontSize: 18,
            fontWeight: 700,
            borderRadius: 8,
            border: "none",
            cursor: finalizing ? "not-allowed" : "pointer",
            background: recorder.isRecording ? "#dc3545" : "#28a745",
            color: "#fff",
          }}
        >
          {recorder.isRecording ? "⏹ Parar Gravação" : "🔴 Iniciar Gravação"}
        </button>
      </div>

      {(recorder.isRecording || meetingId) && (
        <div style={{ padding: 16, background: "#f0f0f0", borderRadius: 8 }}>
          {recorder.isRecording && (
            <p style={{ fontSize: 24, fontWeight: 700, color: "#dc3545" }}>
              🔴 Gravando — {formatTime(elapsed)}
            </p>
          )}
          <p>
            Chunks enviados: <strong>{chunksUploaded}</strong>
          </p>
          {meetingId && (
            <p style={{ fontSize: 12, color: "#666" }}>
              Meeting ID: <code>{meetingId}</code>
            </p>
          )}
        </div>
      )}

      {status && (
        <p style={{ marginTop: 16, fontStyle: "italic", color: "#555" }}>
          {status}
        </p>
      )}
    </div>
  );
}
