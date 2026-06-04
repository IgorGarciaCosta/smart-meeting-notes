import type { Meeting, MeetingUploadResponse } from "./types.ts";

const BASE = "/api/meetings";

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

export interface ModelStatus {
  name: string;
  model: string;
  available: boolean;
  reason?: string;
}

export async function getModelsStatus(): Promise<ModelStatus[]> {
  const res = await fetch("/api/models/status");
  if (!res.ok) throw new Error(`Failed to check models: ${res.statusText}`);
  return res.json();
}
