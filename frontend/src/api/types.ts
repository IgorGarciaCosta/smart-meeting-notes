export enum MeetingStatus {
  AwaitingChunks = "AwaitingChunks",
  Finalizing = "Finalizing",
  Analyzing = "Analyzing",
  Completed = "Completed",
  Failed = "Failed",
}

export enum ChunkStatus {
  Uploaded = "Uploaded",
  Transcribing = "Transcribing",
  Transcribed = "Transcribed",
  Failed = "Failed",
}

export interface TranscriptionSegment {
  start: number;
  end: number;
  text: string;
}

export interface TranscriptionResult {
  text: string;
  language: string;
  segments: TranscriptionSegment[];
}

export interface MeetingAnalysis {
  summary: string;
  actionItems: string[];
  decisions: string[];
  pendingQuestions: string[];
}

export interface AudioChunk {
  chunkIndex: number;
  filePath: string;
  status: ChunkStatus;
  transcript: TranscriptionResult | null;
  errorMessage: string | null;
}

export interface Meeting {
  id: string;
  title: string;
  uploadedAt: string;
  status: MeetingStatus;
  chunks: AudioChunk[];
  transcript: TranscriptionResult | null;
  analysis: MeetingAnalysis | null;
  errorMessage: string | null;
  isChunked: boolean;
}

export interface MeetingUploadResponse {
  meetingId: string;
  status: MeetingStatus;
  message: string;
}
