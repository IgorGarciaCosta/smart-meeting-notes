import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { getMeeting } from "../api/meetings.ts";
import type { Meeting } from "../api/types.ts";

export default function MeetingDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [meeting, setMeeting] = useState<Meeting | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    getMeeting(id)
      .then(setMeeting)
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false));
  }, [id]);

  if (loading) return <p style={{ padding: 24 }}>Carregando...</p>;
  if (error) return <p style={{ padding: 24, color: "red" }}>{error}</p>;
  if (!meeting) return <p style={{ padding: 24 }}>Reunião não encontrada.</p>;

  return (
    <div style={{ maxWidth: 700, margin: "0 auto", padding: 24 }}>
      <Link to="/meetings">← Voltar</Link>

      <h1>{meeting.title}</h1>
      <p>
        <strong>Status:</strong> {meeting.status} &nbsp;|&nbsp;
        <strong>Data:</strong> {new Date(meeting.uploadedAt).toLocaleString("pt-BR")}
      </p>

      {meeting.errorMessage && (
        <div style={{ padding: 12, background: "#f8d7da", borderRadius: 6, color: "#721c24", marginBottom: 16 }}>
          <strong>Erro:</strong> {meeting.errorMessage}
        </div>
      )}

      {meeting.chunks.length > 0 && (
        <div style={{ marginBottom: 24 }}>
          <h2>Chunks ({meeting.chunks.length})</h2>
          <ul>
            {meeting.chunks.map((c) => (
              <li key={c.chunkIndex}>
                Chunk {c.chunkIndex} — <em>{c.status}</em>
                {c.errorMessage && <span style={{ color: "red" }}> ({c.errorMessage})</span>}
              </li>
            ))}
          </ul>
        </div>
      )}

      {meeting.transcript && (
        <div style={{ marginBottom: 24 }}>
          <h2>Transcrição</h2>
          <p style={{ fontSize: 12, color: "#666" }}>Idioma: {meeting.transcript.language}</p>
          <div
            style={{
              background: "#f8f9fa",
              padding: 16,
              borderRadius: 8,
              maxHeight: 400,
              overflow: "auto",
              whiteSpace: "pre-wrap",
              lineHeight: 1.6,
            }}
          >
            {meeting.transcript.text}
          </div>
        </div>
      )}

      {meeting.analysis && (
        <div>
          <h2>Análise</h2>

          <h3>Resumo</h3>
          <p>{meeting.analysis.summary}</p>

          {meeting.analysis.actionItems.length > 0 && (
            <>
              <h3>Ações</h3>
              <ul>
                {meeting.analysis.actionItems.map((item, i) => (
                  <li key={i}>{item}</li>
                ))}
              </ul>
            </>
          )}

          {meeting.analysis.decisions.length > 0 && (
            <>
              <h3>Decisões</h3>
              <ul>
                {meeting.analysis.decisions.map((item, i) => (
                  <li key={i}>{item}</li>
                ))}
              </ul>
            </>
          )}

          {meeting.analysis.pendingQuestions.length > 0 && (
            <>
              <h3>Questões Pendentes</h3>
              <ul>
                {meeting.analysis.pendingQuestions.map((item, i) => (
                  <li key={i}>{item}</li>
                ))}
              </ul>
            </>
          )}
        </div>
      )}
    </div>
  );
}
