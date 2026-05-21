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
        setStatus(`Erro no upload do chunk ${index}: ${e}`);
      }
    },
    [meetingId],
  );

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
    setStatus("Aguardando transcrição dos chunks para finalizar...");

    const pollAndFinalize = async () => {
      const maxAttempts = 60;
      for (let i = 0; i < maxAttempts; i++) {
        await new Promise((r) => setTimeout(r, 5000));
        try {
          const result = await finalizeMeeting(meetingId);
          if (result.status === "pending") {
            setStatus(`Aguardando transcrição... (${i + 1}/${maxAttempts})`);
            continue;
          }
          setStatus(`Reunião finalizada! ${result.message}`);
          setFinalizing(false);
          return;
        } catch (e) {
          const msg = String(e);
          if (
            msg.includes("cannot finalize") ||
            msg.includes("Failed") ||
            msg.includes("cannot upload") ||
            msg.includes("failed transcription")
          ) {
            setStatus(`Erro: ${msg}`);
            setFinalizing(false);
            return;
          }
          setStatus(`Erro ao finalizar: ${msg}`);
          setFinalizing(false);
          return;
        }
      }
      setStatus("Timeout — verifique o status da reunião manualmente.");
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
        <h1>Nova Gravação</h1>
        <p>Configure o dispositivo e inicie a captura de áudio da reunião.</p>
      </div>

      {!recorder.permissionGranted && (
        <div className="alert alert--warning">
          <span>⚠️</span>
          <div>
            <strong>Permissão necessária</strong>
            <p style={{ marginTop: 4 }}>
              Autorize o acesso ao microfone para listar os dispositivos
              disponíveis.
            </p>
            <button
              className="btn btn--primary"
              style={{ marginTop: 12 }}
              onClick={recorder.requestPermission}
            >
              Autorizar Microfone
            </button>
          </div>
        </div>
      )}

      {recorder.error && (
        <div className="alert alert--error">
          <span>✕</span>
          <span>{recorder.error}</span>
        </div>
      )}

      <div className="card">
        <div className="card-header">
          <span className="card-title">Configurações</span>
        </div>

        <div className="form-group">
          <label className="form-label">Dispositivo de Áudio</label>
          <select
            className="form-select"
            value={selectedDevice}
            onChange={(e) => setSelectedDevice(e.target.value)}
            disabled={recorder.isRecording}
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

        <div className="form-row">
          <div className="form-group">
            <label className="form-label">Título da Reunião</label>
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
              Capturar áudio do sistema (mic + som da máquina)
            </label>
            {captureSystemAudio && (
              <p
                style={{
                  fontSize: "0.85em",
                  opacity: 0.7,
                  margin: "4px 0 0 26px",
                }}
              >
                O navegador pedirá permissão para compartilhar o áudio do
                sistema.
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
            {recorder.isRecording ? "⏹  Parar Gravação" : "●  Iniciar Gravação"}
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
              <div className="recording-stat-label">Chunks enviados</div>
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
