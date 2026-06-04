# Smart Meeting Notes

AI-powered meeting assistant that captures audio from microphone or system audio (WASAPI loopback), automatically transcribes it using Faster Whisper, and generates technical summaries, action items, decisions, and pending questions — all running **100% locally and offline**.

> "o problema era race condition no cache" → gera uma issue técnica decente automaticamente.

## Features

- **Audio Capture** — Records from microphone, system audio (WASAPI loopback), or both mixed simultaneously
- **Chunked Streaming** — Audio is split into configurable chunks and sent to the API in real-time
- **Speech-to-Text** — Local transcription via [Faster Whisper](https://github.com/SYSTRAN/faster-whisper) (free, fully offline)
- **AI Analysis** — Processes transcripts with a local LLM to extract:
  - Technical meeting summary
  - Action items with responsible people
  - Key decisions made
  - Pending questions / follow-ups
- **Swappable Models** — Change Whisper and LLM models from the web UI at runtime. Supports:
  - Built-in GGUF models (via llama-cpp-python)
  - Ollama (auto-detects installed models)
  - Any OpenAI-compatible endpoint (LM Studio, vLLM, LocalAI, etc.)
- **Web Dashboard** — React SPA to record meetings, browse history, view analysis, and configure models
- **Fully Offline** — No external API calls; all processing happens on your machine

## Architecture

```
┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐
│  Python Recorder │────▶│  ASP.NET Core    │────▶│  Faster Whisper  │────▶│  Qwen LLM        │
│  (mic/loopback/  │     │  Web API         │     │  (transcribe.py) │     │  (analyze.py)    │
│   mixed audio)   │     │  (.NET 9)        │     │                  │     │                  │
└──────────────────┘     └────────┬─────────┘     └──────────────────┘     └──────────────────┘
                                  │
                         ┌────────▼─────────┐
                         │  React Frontend  │
                         │  (Vite + TS)     │
                         │  Record / Browse │
                         └──────────────────┘
```

### How It Works

1. **Record** — The Python recorder (or the web UI) captures audio and sends WAV chunks to the API
2. **Transcribe** — The API invokes Faster Whisper via subprocess to convert each chunk to text
3. **Analyze** — Once finalized, the full transcript is sent to a local Qwen2.5-7B GGUF model for structured analysis
4. **View** — Results are stored as JSON and displayed in the React dashboard

## Tech Stack

| Component      | Technology                                                            |
| -------------- | --------------------------------------------------------------------- |
| Backend API    | ASP.NET Core (.NET 9, C#)                                             |
| Transcription  | Faster Whisper (Python subprocess)                                    |
| AI Analysis    | Any local LLM — GGUF (llama-cpp-python), Ollama, or OpenAI-compatible |
| Audio Recorder | Python (sounddevice, WASAPI loopback)                                 |
| Frontend       | React 19 + TypeScript + Vite                                          |
| Storage        | JSON files (file-based store)                                         |
| API Docs       | Swagger / OpenAPI                                                     |

## Project Structure

```
smart-meeting-notes/
├── src/SmartMeetingNotes.Api/   # ASP.NET Core Web API
│   ├── Controllers/             # REST endpoints (Meetings, Models)
│   ├── Models/                  # DTOs and domain models
│   ├── Services/                # Business logic, processing queues
│   ├── Middleware/              # Request logging
│   └── data/                    # JSON meeting store + audio files
├── frontend/                    # React SPA (Vite)
│   └── src/
│       ├── pages/               # RecordPage, MeetingsPage, MeetingDetailPage
│       ├── components/          # ModelStatusPanel
│       ├── api/                 # API client + types
│       └── hooks/               # useAudioRecorder
├── recorder/                    # Python CLI audio recorder
│   ├── record.py                # Main CLI entry point
│   ├── capture.py               # Audio capture logic
│   ├── devices.py               # Device enumeration
│   └── api_client.py            # HTTP client for the API
├── transcriber/                 # Faster Whisper transcription module
│   └── transcribe.py
├── analyzer/                    # Qwen LLM analysis module
│   └── analyze.py
└── requirements.txt             # Python dependencies
```

## Whisper Models

The project uses [Faster Whisper](https://github.com/SYSTRAN/faster-whisper) for transcription. Models are downloaded automatically on first use:

| Model      | PT-BR Quality | Speed     | Recommendation         |
| ---------- | ------------- | --------- | ---------------------- |
| `tiny`     | Low           | Very fast | Quick testing          |
| `small`    | Fair          | Fast      | Limited hardware       |
| `medium`   | Good          | Medium    | **Best balance (CPU)** |
| `large-v3` | Best          | Slow      | **Best quality (GPU)** |

## Getting Started

### Prerequisites

- .NET 9 SDK
- Python 3.9+
- Node.js 18+
- GPU with CUDA (optional, recommended for `large-v3`)

### 1. Clone the repository

```bash
git clone https://github.com/IgorGarciaCosta/smart-meeting-notes.git
cd smart-meeting-notes
```

### 2. Setup Python environment

```bash
python -m venv venv
venv\Scripts\activate        # Windows
# source venv/bin/activate   # Linux/macOS

pip install -r requirements.txt
```

### 3. Run the API

```bash
cd src/SmartMeetingNotes.Api
dotnet run
```

The API will be available at `http://localhost:5117` with Swagger UI at `/swagger`.

### 4. Run the Frontend

```bash
cd frontend
npm install
npm run dev
```

The web app opens at `http://localhost:5173`.

### 5. Record a meeting (CLI)

```bash
python -m recorder.record --title "Daily Standup"
python -m recorder.record --mix --title "All Audio"   # mic + system audio
python -m recorder.record --chunk-duration 30          # 30s chunks
```

## API Endpoints

| Method   | Endpoint                         | Description                                 |
| -------- | -------------------------------- | ------------------------------------------- |
| `GET`    | `/api/meetings`                  | List all meetings                           |
| `GET`    | `/api/meetings/{id}`             | Get meeting details (transcript + analysis) |
| `POST`   | `/api/meetings`                  | Create a new meeting                        |
| `POST`   | `/api/meetings/{id}/chunks`      | Upload an audio chunk                       |
| `GET`    | `/api/meetings/{id}/chunks`      | Get chunk status for a meeting              |
| `POST`   | `/api/meetings/{id}/finalize`    | Finalize and trigger analysis               |
| `DELETE` | `/api/meetings/{id}`             | Delete a meeting                            |
| `GET`    | `/api/models/status`             | Check Whisper & LLM model availability      |
| `GET`    | `/api/models/settings`           | Get current model configuration             |
| `PUT`    | `/api/models/settings`           | Update models (takes effect immediately)    |
| `GET`    | `/api/models/whisper/available`  | List available Whisper model options        |
| `GET`    | `/api/models/analyzer/available` | Scan local GGUF models on disk              |
| `GET`    | `/api/models/ollama/available`   | List Ollama models (if running)             |
| `GET`    | `/health`                        | Health check                                |

## Project Status

🚧 **In active development** — Core pipeline is functional (record → transcribe → analyze → view).

## Roadmap

- [x] Faster Whisper transcription (PT-BR + multi-language)
- [x] ASP.NET Core API with chunked upload
- [x] Python audio recorder (mic + system loopback + mixed)
- [x] React frontend with recording and browsing
- [x] Local LLM analysis (Qwen2.5-7B GGUF)
- [x] Model status dashboard (Whisper + LLM availability check)
- [x] Runtime model switching from web UI (Whisper + LLM)
- [x] Multi-provider support (GGUF, Ollama, OpenAI-compatible)
- [ ] Speaker diarization (who said what)
- [ ] Real-time transcript streaming via WebSocket
- [ ] Export to Markdown / PDF
- [ ] Jira / GitHub Issues integration
- [ ] Multi-language UI

## License

MIT
