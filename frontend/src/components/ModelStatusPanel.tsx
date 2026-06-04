import { useEffect, useState } from "react";
import { getModelsStatus, type ModelStatus } from "../api/meetings.ts";

export default function ModelStatusPanel() {
  const [models, setModels] = useState<ModelStatus[] | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    getModelsStatus()
      .then(setModels)
      .catch(() => setError(true));
  }, []);

  if (error) {
    return (
      <div className="card models-panel">
        <div className="models-panel-header">
          <span className="card-title">Services</span>
        </div>
        <p className="models-error">
          Unable to reach processing services.
        </p>
      </div>
    );
  }

  if (!models) {
    return (
      <div className="card models-panel">
        <div className="models-panel-header">
          <span className="card-title">Services</span>
        </div>
        <p style={{ color: "var(--text-muted)", fontSize: 12 }}>
          Loading...
        </p>
      </div>
    );
  }

  const hasIssues = models.some((m) => !m.available);

  if (!hasIssues) {
    return (
      <div className="models-panel models-panel--inline">
        {models.map((m) => (
          <span key={m.name} className="models-inline-item">
            <span className="models-dot models-dot--ok" />
            <span className="models-inline-label">{m.name}</span>
          </span>
        ))}
      </div>
    );
  }

  return (
    <div className="card models-panel">
      <div className="models-panel-header">
        <span className="card-title">Services</span>
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
