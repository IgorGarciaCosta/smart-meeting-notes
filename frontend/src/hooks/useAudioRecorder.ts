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
  /** Start capturing mic + system audio simultaneously (mixed) */
  startWithSystemAudio: (deviceId: string) => void;
  stop: () => void;
  /** Callback invoked each time a chunk blob is ready */
  onChunk: React.MutableRefObject<((blob: Blob, index: number) => void) | null>;
  error: string | null;
  /** Whether the browser supports system audio capture */
  supportsSystemAudio: boolean;
}

export function useAudioRecorder(opts: UseAudioRecorderOptions = {}): UseAudioRecorderReturn {
  const { chunkDuration = 30 } = opts;

  const [devices, setDevices] = useState<AudioDevice[]>([]);
  const [permissionGranted, setPermissionGranted] = useState(false);
  const [isRecording, setIsRecording] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const streamRef = useRef<MediaStream | null>(null);
  const systemStreamRef = useRef<MediaStream | null>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const chunkIndexRef = useRef(0);
  const onChunk = useRef<((blob: Blob, index: number) => void) | null>(null);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const recorderRef = useRef<MediaRecorder | null>(null);
  const stoppedByUser = useRef(false);
  const deviceIdRef = useRef<string>("");

  const supportsSystemAudio = typeof navigator.mediaDevices?.getDisplayMedia === "function";

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
    enumerateDevices();
  }, [enumerateDevices]);

  // Start a single MediaRecorder session that produces ONE complete file when stopped
  const startRecorder = useCallback((stream: MediaStream) => {
    const mimeType = MediaRecorder.isTypeSupported("audio/webm;codecs=opus")
      ? "audio/webm;codecs=opus"
      : "audio/webm";

    const recorder = new MediaRecorder(stream, { mimeType });
    const chunks: Blob[] = [];

    recorder.ondataavailable = (e) => {
      if (e.data.size > 0) chunks.push(e.data);
    };

    recorder.onstop = () => {
      // Combine all fragments into one complete valid webm file
      if (chunks.length > 0 && onChunk.current) {
        const completeBlob = new Blob(chunks, { type: mimeType });
        onChunk.current(completeBlob, chunkIndexRef.current);
        chunkIndexRef.current++;
      }

      // If not stopped by user, start a new recorder for next chunk
      if (!stoppedByUser.current && streamRef.current?.active) {
        startRecorder(streamRef.current);
      }
    };

    recorder.onerror = () => {
      setError("Erro durante gravação");
      setIsRecording(false);
    };

    recorderRef.current = recorder;
    recorder.start(); // No timeslice! Record continuously until stopped
  }, []);

  const start = useCallback(
    (deviceId: string) => {
      setError(null);
      chunkIndexRef.current = 0;
      stoppedByUser.current = false;
      deviceIdRef.current = deviceId;

      navigator.mediaDevices
        .getUserMedia({
          audio: { deviceId: { exact: deviceId } },
        })
        .then((stream) => {
          streamRef.current = stream;
          startRecorder(stream);
          setIsRecording(true);

          // Cycle: stop recorder every chunkDuration to produce a complete file,
          // then onstop handler auto-starts a new one
          timerRef.current = setInterval(() => {
            if (recorderRef.current && recorderRef.current.state === "recording") {
              recorderRef.current.stop();
            }
          }, chunkDuration * 1000);
        })
        .catch((e) => {
          setError(`Não foi possível iniciar gravação: ${e}`);
        });
    },
    [chunkDuration, startRecorder]
  );

  const startWithSystemAudio = useCallback(
    (deviceId: string) => {
      setError(null);
      chunkIndexRef.current = 0;
      stoppedByUser.current = false;
      deviceIdRef.current = deviceId;

      // Request both mic and system audio
      const getMic = navigator.mediaDevices.getUserMedia({
        audio: { deviceId: { exact: deviceId } },
      });

      const getSystem = navigator.mediaDevices.getDisplayMedia({
        video: false,
        audio: true,
      } as DisplayMediaStreamOptions).catch(() =>
        // Fallback: some browsers require video:true, then we ignore the video track
        navigator.mediaDevices.getDisplayMedia({
          video: { width: 1, height: 1 },
          audio: true,
        })
      );

      Promise.all([getMic, getSystem])
        .then(([micStream, systemStream]) => {
          systemStreamRef.current = systemStream;

          // Mix both streams using Web Audio API
          const ctx = new AudioContext();
          audioContextRef.current = ctx;
          const destination = ctx.createMediaStreamDestination();

          const micSource = ctx.createMediaStreamSource(micStream);
          micSource.connect(destination);

          // Only connect audio tracks from system stream
          const systemAudioTracks = systemStream.getAudioTracks();
          if (systemAudioTracks.length > 0) {
            const systemAudioStream = new MediaStream(systemAudioTracks);
            const systemSource = ctx.createMediaStreamSource(systemAudioStream);
            systemSource.connect(destination);
          }

          // Stop any video tracks from getDisplayMedia (if present)
          systemStream.getVideoTracks().forEach((t) => t.stop());

          // Use the mixed stream for recording
          const mixedStream = destination.stream;
          streamRef.current = mixedStream;

          // Keep original mic stream ref for cleanup
          // (micStream tracks are routed through AudioContext, not directly)
          // We store systemStream separately for cleanup

          startRecorder(mixedStream);
          setIsRecording(true);

          timerRef.current = setInterval(() => {
            if (recorderRef.current && recorderRef.current.state === "recording") {
              recorderRef.current.stop();
            }
          }, chunkDuration * 1000);

          // If system stream ends (user stops sharing), auto-stop recording
          systemStream.getAudioTracks().forEach((track) => {
            track.onended = () => {
              if (!stoppedByUser.current) {
                stoppedByUser.current = true;
                if (timerRef.current) {
                  clearInterval(timerRef.current);
                  timerRef.current = null;
                }
                if (recorderRef.current && recorderRef.current.state !== "inactive") {
                  recorderRef.current.stop();
                }
                if (streamRef.current) {
                  streamRef.current.getTracks().forEach((t) => t.stop());
                  streamRef.current = null;
                }
                if (systemStreamRef.current) {
                  systemStreamRef.current.getTracks().forEach((t) => t.stop());
                  systemStreamRef.current = null;
                }
                if (audioContextRef.current) {
                  audioContextRef.current.close();
                  audioContextRef.current = null;
                }
                recorderRef.current = null;
                setIsRecording(false);
              }
            };
          });
        })
        .catch((e) => {
          setError(`Não foi possível capturar áudio do sistema: ${e}`);
        });
    },
    [chunkDuration, startRecorder]
  );

  const stop = useCallback(() => {
    stoppedByUser.current = true;

    if (timerRef.current) {
      clearInterval(timerRef.current);
      timerRef.current = null;
    }

    if (recorderRef.current && recorderRef.current.state !== "inactive") {
      recorderRef.current.stop(); // This triggers onstop → emits final chunk
    }

    if (streamRef.current) {
      streamRef.current.getTracks().forEach((t) => t.stop());
      streamRef.current = null;
    }

    if (systemStreamRef.current) {
      systemStreamRef.current.getTracks().forEach((t) => t.stop());
      systemStreamRef.current = null;
    }

    if (audioContextRef.current) {
      audioContextRef.current.close();
      audioContextRef.current = null;
    }

    recorderRef.current = null;
    setIsRecording(false);
  }, []);

  return {
    devices,
    permissionGranted,
    requestPermission,
    isRecording,
    start,
    startWithSystemAudio,
    stop,
    onChunk,
    error,
    supportsSystemAudio,
  };
}
