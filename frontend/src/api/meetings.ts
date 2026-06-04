import type { Meeting, MeetingUploadResponse } from "./types.ts";

const API_BASE = import.meta.env.VITE_API_URL || "";
const BASE = `${API_BASE}/api/meetings`;

export async function createMeeting(title: string): Promise<MeetingUploadResponse> {
  const res = await fetch(BASE, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ title }),
  });
  if (!res.ok) throw new Error(`Failed to create meeting: ${res.statusText}`);
  return res.json();
}

export async function uploadChunk(
  meetingId: string,
  chunkIndex: number,
  blob: Blob,
  filename: string
): Promise<void> {
  const form = new FormData();
  form.append("audio", blob, filename);
  form.append("chunkIndex", String(chunkIndex));

  const res = await fetch(`${BASE}/${meetingId}/chunks`, {
    method: "POST",
    body: form,
  });
  if (!res.ok) throw new Error(`Failed to upload chunk ${chunkIndex}: ${res.statusText}`);
}

export async function finalizeMeeting(meetingId: string): Promise<MeetingUploadResponse & { status?: string }> {
  const res = await fetch(`${BASE}/${meetingId}/finalize`, { method: "POST" });
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new Error(body.error || `Failed to finalize: ${res.statusText}`);
  }
  return res.json();
}

export async function getMeeting(meetingId: string): Promise<Meeting> {
  const res = await fetch(`${BASE}/${meetingId}`);
  if (!res.ok) throw new Error(`Failed to get meeting: ${res.statusText}`);
  return res.json();
}

export async function getAllMeetings(): Promise<Meeting[]> {
  const res = await fetch(BASE);
  if (!res.ok) throw new Error(`Failed to list meetings: ${res.statusText}`);
  return res.json();
}

export async function deleteMeeting(meetingId: string): Promise<void> {
  const res = await fetch(`${BASE}/${meetingId}`, { method: "DELETE" });
  if (!res.ok) throw new Error(`Failed to delete meeting: ${res.statusText}`);
}

export interface ModelStatus {
  name: string;
  model: string;
  available: boolean;
  reason?: string;
}

export async function getModelsStatus(): Promise<ModelStatus[]> {
  const res = await fetch(`${API_BASE}/api/models/status`);
  if (!res.ok) throw new Error(`Failed to check models: ${res.statusText}`);
  return res.json();
}

// --- Model Settings API ---

export interface ModelSettings {
  whisperModel: string;
  whisperDevice: string;
  analyzerProvider: string;
  analyzerEndpoint: string;
  analyzerModelRepo: string;
  analyzerModelFile: string;
  analyzerLocalModelPath: string;
  ollamaModel: string;
}

export async function getModelSettings(): Promise<ModelSettings> {
  const res = await fetch(`${API_BASE}/api/models/settings`);
  if (!res.ok) throw new Error(`Failed to get settings: ${res.statusText}`);
  return res.json();
}

export async function updateModelSettings(settings: Partial<ModelSettings>): Promise<ModelSettings> {
  const res = await fetch(`${API_BASE}/api/models/settings`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(settings),
  });
  if (!res.ok) throw new Error(`Failed to update settings: ${res.statusText}`);
  return res.json();
}

export interface WhisperModelOption {
  id: string;
  name: string;
  quality: string;
  speed: string;
  notes: string;
}

export async function getWhisperModels(): Promise<WhisperModelOption[]> {
  const res = await fetch(`${API_BASE}/api/models/whisper/available`);
  if (!res.ok) throw new Error(`Failed to get whisper models: ${res.statusText}`);
  return res.json();
}

export interface LocalGgufModel {
  path: string;
  filename: string;
  repo: string;
  sizeGb: number;
  source: string;
}

export async function getAnalyzerModels(): Promise<{ models: LocalGgufModel[]; count: number }> {
  const res = await fetch(`${API_BASE}/api/models/analyzer/available`);
  if (!res.ok) throw new Error(`Failed to get analyzer models: ${res.statusText}`);
  return res.json();
}

export interface OllamaModel {
  name: string;
  model: string;
  size: number;
}

export async function getOllamaModels(): Promise<{ available: boolean; models: { models?: OllamaModel[] }; reason?: string }> {
  const res = await fetch(`${API_BASE}/api/models/ollama/available`);
  if (!res.ok) throw new Error(`Failed to get ollama models: ${res.statusText}`);
  return res.json();
}
