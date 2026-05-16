import { useCallback, useEffect, useRef, useState } from "react";

export interface AudioDevice {
  deviceId: string;
  label: string;
}

interface UseAudioRecorderOptions {
  chunkDuration?: number; // seconds
}

interface UseAudioRecorderReturn {
  devices: AudioDevice[];
  permissionGranted: boolean;
  requestPermission: () => Promise<void>;
  isRecording: boolean;
  start: (deviceId: string) => void;
  stop: () => void;
  /** Callback invoked each time a chunk blob is ready */
  onChunk: React.MutableRefObject<((blob: Blob, index: number) => void) | null>;
  error: string | null;
}

export function useAudioRecorder(opts: UseAudioRecorderOptions = {}): UseAudioRecorderReturn {
  const { chunkDuration = 30 } = opts;

  const [devices, setDevices] = useState<AudioDevice[]>([]);
  const [permissionGranted, setPermissionGranted] = useState(false);
  const [isRecording, setIsRecording] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const chunkIndexRef = useRef(0);
  const onChunk = useRef<((blob: Blob, index: number) => void) | null>(null);

  const enumerateDevices = useCallback(async () => {
    try {
      const allDevices = await navigator.mediaDevices.enumerateDevices();
      const audioInputs = allDevices
        .filter((d) => d.kind === "audioinput" && d.deviceId)
        .map((d) => ({
          deviceId: d.deviceId,
          label: d.label || `Microfone (${d.deviceId.slice(0, 8)}...)`,
        }));
      setDevices(audioInputs);
    } catch (e) {
      setError(`Erro ao listar dispositivos: ${e}`);
    }
  }, []);

  const requestPermission = useCallback(async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      stream.getTracks().forEach((t) => t.stop());
      setPermissionGranted(true);
      setError(null);
      await enumerateDevices();
    } catch (e) {
      setError(`Permissão de áudio negada: ${e}`);
      setPermissionGranted(false);
    }
  }, [enumerateDevices]);

  useEffect(() => {
    // Try to enumerate on mount (labels may be empty without permission)
    enumerateDevices();
  }, [enumerateDevices]);

  const start = useCallback(
    (deviceId: string) => {
      setError(null);
      chunkIndexRef.current = 0;

      navigator.mediaDevices
        .getUserMedia({
          audio: { deviceId: { exact: deviceId } },
        })
        .then((stream) => {
          streamRef.current = stream;

          // Prefer webm (widely supported) — API accepts .webm
          const mimeType = MediaRecorder.isTypeSupported("audio/webm;codecs=opus")
            ? "audio/webm;codecs=opus"
            : "audio/webm";

          const recorder = new MediaRecorder(stream, { mimeType });
          mediaRecorderRef.current = recorder;

          recorder.ondataavailable = (e) => {
            if (e.data.size > 0 && onChunk.current) {
              onChunk.current(e.data, chunkIndexRef.current);
              chunkIndexRef.current++;
            }
          };

          recorder.onerror = () => {
            setError("Erro durante gravação");
            setIsRecording(false);
          };

          recorder.start(chunkDuration * 1000); // timeslice in ms
          setIsRecording(true);
        })
        .catch((e) => {
          setError(`Não foi possível iniciar gravação: ${e}`);
        });
    },
    [chunkDuration]
  );

  const stop = useCallback(() => {
    if (mediaRecorderRef.current && mediaRecorderRef.current.state !== "inactive") {
      mediaRecorderRef.current.stop();
    }
    if (streamRef.current) {
      streamRef.current.getTracks().forEach((t) => t.stop());
      streamRef.current = null;
    }
    mediaRecorderRef.current = null;
    setIsRecording(false);
  }, []);

  return {
    devices,
    permissionGranted,
    requestPermission,
    isRecording,
    start,
    stop,
    onChunk,
    error,
  };
}
