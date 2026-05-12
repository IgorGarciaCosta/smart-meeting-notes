# Smart Meeting Notes

AI-powered meeting assistant that listens to Discord/Teams calls and automatically generates technical summaries, action items, decisions, pending questions, and Jira/GitHub tickets — with **engineering context awareness**.

> "o problema era race condition no cache" → gera uma issue técnica decente automaticamente.

## Features

- **Audio Capture** — Connects to Discord (bot) or captures system audio for Teams
- **Speech-to-Text** — Transcription local via [Faster Whisper](https://github.com/SYSTRAN/faster-whisper) (gratuito, roda offline)
- **AI Analysis** — Processa o transcript com LLM para extrair:
  - Resumo técnico da reunião
  - Tarefas e responsáveis
  - Decisões tomadas
  - Dúvidas pendentes
- **Integrations** — Cria tickets automaticamente no Jira e/ou GitHub Issues
- **Engineering Context** — Entende termos técnicos e gera issues com contexto real

## Architecture

```
┌─────────────┐     ┌──────────────┐     ┌──────────────┐     ┌────────────────┐
│  Discord Bot │────▶│ Audio Capture │────▶│ Faster Whisper│────▶│  LLM Analysis  │
│  / System    │     │  (PCM/WAV)   │     │ (Transcribe) │     │ (Summary/Tasks)│
│  Audio       │     └──────────────┘     └──────────────┘     └───────┬────────┘
└─────────────┘                                                       │
                                                          ┌───────────▼──────────┐
                                                          │  Output Generation   │
                                                          │  - Markdown Report   │
                                                          │  - Jira Tickets      │
                                                          │  - GitHub Issues     │
                                                          └──────────────────────┘
```

## Tech Stack

| Component | Technology |
|---|---|
| Backend | ASP.NET (C#) |
| Transcription | Faster Whisper (Python) — called via subprocess or API |
| AI/LLM | OpenAI GPT / local model |
| Audio Capture | Discord.js bot / NAudio (system audio) |
| Integrations | Jira REST API, GitHub API |
| Storage | SQLite / PostgreSQL |

## Whisper Models

O projeto usa [Faster Whisper](https://github.com/SYSTRAN/faster-whisper) para transcrição. Os modelos são baixados automaticamente na primeira execução:

| Modelo | Qualidade PT-BR | Velocidade | Recomendação |
|---|---|---|---|
| `tiny` | Baixa | Muito rápido | Testes rápidos |
| `small` | Razoável | Rápido | Máquina limitada |
| `medium` | Boa | Médio | **Bom equilíbrio (CPU)** |
| `large-v3` | Melhor | Lento | **Melhor qualidade (GPU)** |

## Getting Started

### Prerequisites

- .NET 8 SDK
- Python 3.9+
- Node.js 18+ (para Discord bot)
- GPU com CUDA (opcional, mas recomendado para `large-v3`)

### 1. Install Faster Whisper

```bash
pip install faster-whisper
```

### 2. Test transcription

```python
from faster_whisper import WhisperModel

model = WhisperModel("medium", device="cpu", compute_type="int8")
segments, info = model.transcribe("audio_teste.mp3", beam_size=5)

for segment in segments:
    print(f"[{segment.start:.2f}s -> {segment.end:.2f}s] {segment.text}")
```

### 3. Clone and run

```bash
git clone https://github.com/IgorGarciaCosta/smart-meeting-notes.git
cd smart-meeting-notes
# setup steps TBD
```

## Project Status

🚧 **Em desenvolvimento** — Fase inicial de setup e prototipagem.

## Roadmap

- [ ] Setup do Faster Whisper + teste de transcrição PT-BR
- [ ] Discord bot para captura de áudio
- [ ] Pipeline de transcrição (áudio → texto)
- [ ] Integração com LLM para análise do transcript
- [ ] Geração de resumo técnico estruturado
- [ ] Criação automática de issues (GitHub/Jira)
- [ ] Interface web para visualização dos resumos
- [ ] Suporte a captura de áudio do Teams (system audio)

## License

MIT
