import { useEffect, useState } from "react";
import {
  getModelSettings,
  updateModelSettings,
  getWhisperModels,
  getAnalyzerModels,
  getOllamaModels,
  type ModelSettings,
  type WhisperModelOption,
  type LocalGgufModel,
  type OllamaModel,
} from "../api/meetings.ts";

export default function SettingsPage() {
  const [settings, setSettings] = useState<ModelSettings | null>(null);
  const [whisperOptions, setWhisperOptions] = useState<WhisperModelOption[]>([]);
  const [ggufModels, setGgufModels] = useState<LocalGgufModel[]>([]);
  const [ollamaModels, setOllamaModels] = useState<OllamaModel[]>([]);
  const [ollamaAvailable, setOllamaAvailable] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    loadAll();
  }, []);

  async function loadAll() {
    try {
      const [s, w, a, o] = await Promise.all([
        getModelSettings(),
        getWhisperModels(),
        getAnalyzerModels(),
        getOllamaModels(),
      ]);
      setSettings(s);
      setWhisperOptions(w);
      setGgufModels(a.models || []);
      setOllamaAvailable(o.available);
      if (o.available && o.models?.models) {
        setOllamaModels(o.models.models);
      }
    } catch (e) {
      setError("Failed to load settings. Is the API running?");
    }
  }

  async function handleSave() {
    if (!settings) return;
    setSaving(true);
    setSaved(false);
    setError("");
    try {
      const updated = await updateModelSettings(settings);
      setSettings(updated);
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch (e) {
      setError("Failed to save settings.");
    } finally {
      setSaving(false);
    }
  }

  if (error && !settings) {
    return (
      <div className="page">
        <h1>Settings</h1>
        <p className="models-error">{error}</p>
      </div>
    );
  }

  if (!settings) {
    return (
      <div className="page">
        <h1>Settings</h1>
        <p style={{ color: "var(--text-muted)" }}>Loading...</p>
      </div>
    );
  }

  return (
    <div className="page">
      <h1>Model Settings</h1>
      <p style={{ color: "var(--text-muted)", marginBottom: 24 }}>
        Change which AI models are used for transcription and analysis. Changes take effect on the next meeting processed.
      </p>

      {/* Whisper Section */}
      <section className="settings-section">
        <h2>Transcription (Whisper)</h2>

        <label className="settings-label">
          Model
          <select
            className="settings-select"
            value={settings.whisperModel}
            onChange={(e) => setSettings({ ...settings, whisperModel: e.target.value })}
          >
            {whisperOptions.map((m) => (
              <option key={m.id} value={m.id}>
                {m.name} — {m.quality} quality, {m.speed}
              </option>
            ))}
          </select>
        </label>

        <label className="settings-label">
          Device
          <select
            className="settings-select"
            value={settings.whisperDevice}
            onChange={(e) => setSettings({ ...settings, whisperDevice: e.target.value })}
          >
            <option value="cpu">CPU</option>
            <option value="cuda">CUDA (GPU)</option>
          </select>
        </label>
      </section>

      {/* Analyzer Section */}
      <section className="settings-section">
        <h2>Analysis (LLM)</h2>

        <label className="settings-label">
          Provider
          <select
            className="settings-select"
            value={settings.analyzerProvider}
            onChange={(e) => setSettings({ ...settings, analyzerProvider: e.target.value })}
          >
            <option value="builtin">Built-in (llama-cpp-python / GGUF)</option>
            <option value="ollama">Ollama</option>
            <option value="openai-compatible">OpenAI-Compatible Endpoint</option>
          </select>
        </label>

        {/* Builtin provider options */}
        {settings.analyzerProvider === "builtin" && (
          <>
            <label className="settings-label">
              Model Source
              <select
                className="settings-select"
                value={settings.analyzerLocalModelPath ? "local" : "huggingface"}
                onChange={(e) => {
                  if (e.target.value === "huggingface") {
                    setSettings({ ...settings, analyzerLocalModelPath: "" });
                  } else {
                    setSettings({ ...settings, analyzerLocalModelPath: ggufModels[0]?.path || "" });
                  }
                }}
              >
                <option value="huggingface">HuggingFace (download by repo)</option>
                <option value="local">Local GGUF file</option>
              </select>
            </label>

            {!settings.analyzerLocalModelPath && (
              <>
                <label className="settings-label">
                  HuggingFace Repo
                  <input
                    className="settings-input"
                    type="text"
                    value={settings.analyzerModelRepo}
                    onChange={(e) => setSettings({ ...settings, analyzerModelRepo: e.target.value })}
                    placeholder="Qwen/Qwen2.5-7B-Instruct-GGUF"
                  />
                </label>
                <label className="settings-label">
                  GGUF Filename
                  <input
                    className="settings-input"
                    type="text"
                    value={settings.analyzerModelFile}
                    onChange={(e) => setSettings({ ...settings, analyzerModelFile: e.target.value })}
                    placeholder="qwen2.5-7b-instruct-q3_k_m.gguf"
                  />
                </label>
              </>
            )}

            {settings.analyzerLocalModelPath !== undefined && settings.analyzerLocalModelPath !== "" && (
              <label className="settings-label">
                Local GGUF File
                {ggufModels.length > 0 ? (
                  <select
                    className="settings-select"
                    value={settings.analyzerLocalModelPath}
                    onChange={(e) => setSettings({ ...settings, analyzerLocalModelPath: e.target.value })}
                  >
                    {ggufModels.map((m) => (
                      <option key={m.path} value={m.path}>
                        {m.filename} ({m.sizeGb} GB){m.repo ? ` — ${m.repo}` : ""}
                      </option>
                    ))}
                  </select>
                ) : (
                  <input
                    className="settings-input"
                    type="text"
                    value={settings.analyzerLocalModelPath}
                    onChange={(e) => setSettings({ ...settings, analyzerLocalModelPath: e.target.value })}
                    placeholder="C:\Users\you\models\model.gguf"
                  />
                )}
              </label>
            )}
          </>
        )}

        {/* Ollama provider options */}
        {settings.analyzerProvider === "ollama" && (
          <>
            {!ollamaAvailable && (
              <p className="settings-warning">
                Ollama not detected on localhost:11434. Make sure it's running.
              </p>
            )}
            <label className="settings-label">
              Ollama Model
              {ollamaModels.length > 0 ? (
                <select
                  className="settings-select"
                  value={settings.ollamaModel}
                  onChange={(e) => setSettings({ ...settings, ollamaModel: e.target.value, analyzerEndpoint: "http://localhost:11434/v1" })}
                >
                  <option value="">Select a model...</option>
                  {ollamaModels.map((m) => (
                    <option key={m.name} value={m.name}>
                      {m.name} ({(m.size / 1e9).toFixed(1)} GB)
                    </option>
                  ))}
                </select>
              ) : (
                <input
                  className="settings-input"
                  type="text"
                  value={settings.ollamaModel}
                  onChange={(e) => setSettings({ ...settings, ollamaModel: e.target.value })}
                  placeholder="qwen2.5:7b"
                />
              )}
            </label>
          </>
        )}

        {/* OpenAI-compatible provider options */}
        {settings.analyzerProvider === "openai-compatible" && (
          <>
            <label className="settings-label">
              Endpoint URL
              <input
                className="settings-input"
                type="text"
                value={settings.analyzerEndpoint}
                onChange={(e) => setSettings({ ...settings, analyzerEndpoint: e.target.value })}
                placeholder="http://localhost:1234/v1"
              />
            </label>
            <label className="settings-label">
              Model Name
              <input
                className="settings-input"
                type="text"
                value={settings.ollamaModel}
                onChange={(e) => setSettings({ ...settings, ollamaModel: e.target.value })}
                placeholder="model-name"
              />
            </label>
          </>
        )}
      </section>

      {/* Save */}
      <div className="settings-actions">
        <button className="btn btn-primary" onClick={handleSave} disabled={saving}>
          {saving ? "Saving..." : "Save Settings"}
        </button>
        {saved && <span className="settings-saved">Settings saved!</span>}
        {error && <span className="settings-error">{error}</span>}
      </div>
    </div>
  );
}
