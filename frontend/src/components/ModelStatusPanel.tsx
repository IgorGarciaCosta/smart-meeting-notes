import { useEffect, useState } from "react";
import { getModelsStatus, type ModelStatus } from "../api/meetings.ts";

const MODEL_DESCRIPTIONS: Record<string, string> = {
  Whisper:
    "Audio transcription model (speech-to-text). Converts meeting audio into text.",
  Qwen: "Text analysis model (LLM). Generates summary, actions, and decisions from the transcript.",
};

export default function ModelStatusPanel() {
  const [models, setModels] = useState<ModelStatus[] | null>(null);
  const [error, setError] = useState(false);
  const [showTooltip, setShowTooltip] = useState(false);

  useEffect(() => {
    getModelsStatus()
      .then(setModels)
      .catch(() => setError(true));
  }, []);

  if (error) {
    return (
      <div className="card models-panel">
        <div className="models-panel-header">
          <span className="card-title">AI Models</span>
        </div>
        <p className="models-error">
          Could not check models. Is the server running?
        </p>
      </div>
    );
  }

  if (!models) {
    return (
      <div className="card models-panel">
        <div className="models-panel-header">
          <span className="card-title">AI Models</span>
        </div>
        <p style={{ color: "var(--text-muted)", fontSize: 13 }}>
          Checking models...
        </p>
      </div>
    );
  }

  return (
    <div className="card models-panel">
      <div className="models-panel-header">
        <span className="card-title">AI Models</span>
        <div
          className="models-info-icon"
          onMouseEnter={() => setShowTooltip(true)}
          onMouseLeave={() => setShowTooltip(false)}
        >
          i
          {showTooltip && (
            <div className="models-tooltip">
              {Object.entries(MODEL_DESCRIPTIONS).map(([name, desc]) => (
                <div key={name} className="models-tooltip-item">
                  <strong>{name}:</strong> {desc}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      <div className="models-list">
        {models.map((m) => (
          <div key={m.name} className="models-item">
            <span
              className={`models-dot ${m.available ? "models-dot--ok" : "models-dot--error"}`}
            />
            <div className="models-item-info">
              <span className="models-item-name">{m.name}</span>
              <span className="models-item-model">{m.model}</span>
            </div>
            {!m.available && (
              <span className="models-item-warning">{m.reason}</span>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
