import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { getMeeting } from "../api/meetings.ts";
import type { Meeting } from "../api/types.ts";
import { MeetingStatus, ChunkStatus } from "../api/types.ts";

function statusBadge(status: MeetingStatus) {
  const map: Record<string, { cls: string; label: string }> = {
    [MeetingStatus.Uploaded]: { cls: "badge--neutral", label: "Uploaded" },
    [MeetingStatus.AwaitingChunks]: { cls: "badge--info", label: "Aguardando" },
    [MeetingStatus.Transcribing]: { cls: "badge--warning", label: "Transcrevendo" },
    [MeetingStatus.Finalizing]: { cls: "badge--warning", label: "Finalizando" },
    [MeetingStatus.Analyzing]: { cls: "badge--warning", label: "Analisando" },
    [MeetingStatus.Completed]: { cls: "badge--success", label: "Concluída" },
    [MeetingStatus.Failed]: { cls: "badge--danger", label: "Falhou" },
  };
  const info = map[status] || { cls: "badge--neutral", label: status };
  return (
    <span className={`badge ${info.cls}`}>
      <span className="badge-dot" />
      {info.label}
    </span>
  );
}

function chunkBadge(status: ChunkStatus) {
  const map: Record<string, string> = {
    [ChunkStatus.Uploaded]: "badge--neutral",
    [ChunkStatus.Transcribing]: "badge--warning",
    [ChunkStatus.Transcribed]: "badge--success",
    [ChunkStatus.Failed]: "badge--danger",
  };
  return <span className={`badge ${map[status] || "badge--neutral"}`}><span className="badge-dot" />{status}</span>;
}

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

  if (loading) return <div className="loading">Carregando...</div>;
  if (error) return <div className="page"><div className="alert alert--error"><span>✕</span><span>{error}</span></div></div>;
  if (!meeting) return <div className="page"><div className="empty-state"><p>Reunião não encontrada.</p></div></div>;

  return (
    <div className="page">
      <Link to="/meetings" className="back-link">
        ← Voltar para reuniões
      </Link>

      <div className="page-header">
        <h1>{meeting.title}</h1>
      </div>

      <div className="detail-meta">
        {statusBadge(meeting.status)}
        <span className="detail-meta-item">
          {new Date(meeting.uploadedAt).toLocaleString("pt-BR")}
        </span>
        {meeting.chunks.length > 0 && (
          <span className="detail-meta-item">
            {meeting.chunks.length} chunk(s)
          </span>
        )}
      </div>

      {meeting.errorMessage && (
        <div className="alert alert--error">
          <span>✕</span>
          <span><strong>Erro:</strong> {meeting.errorMessage}</span>
        </div>
      )}

      {meeting.chunks.length > 0 && (
        <div className="detail-section">
          <h2>Chunks</h2>
          <div className="chunk-list">
            {meeting.chunks.map((c) => (
              <div key={c.chunkIndex} className="chunk-item">
                <span className="chunk-index">#{c.chunkIndex}</span>
                {chunkBadge(c.status)}
                {c.errorMessage && (
                  <span style={{ color: "var(--danger)", fontSize: 12, marginLeft: "auto" }}>
                    {c.errorMessage}
                  </span>
                )}
              </div>
            ))}
          </div>
        </div>
      )}

      {meeting.transcript && (
        <div className="detail-section">
          <h2>Transcrição</h2>
          <p style={{ fontSize: 12, color: "var(--text-muted)", marginBottom: 12 }}>
            Idioma detectado: {meeting.transcript.language}
          </p>
          <div className="transcript-box">
            {meeting.transcript.text}
          </div>
        </div>
      )}

      {meeting.analysis && (
        <div className="detail-section">
          <h2>Análise</h2>

          <h3>Resumo</h3>
          <p style={{ color: "var(--text-primary)", lineHeight: 1.7 }}>{meeting.analysis.summary}</p>

          {meeting.analysis.actionItems.length > 0 && (
            <>
              <h3>Ações</h3>
              <ul className="analysis-list">
                {meeting.analysis.actionItems.map((item, i) => (
                  <li key={i}>{item}</li>
                ))}
              </ul>
            </>
          )}

          {meeting.analysis.decisions.length > 0 && (
            <>
              <h3>Decisões</h3>
              <ul className="analysis-list">
                {meeting.analysis.decisions.map((item, i) => (
                  <li key={i}>{item}</li>
                ))}
              </ul>
            </>
          )}

          {meeting.analysis.pendingQuestions.length > 0 && (
            <>
              <h3>Questões Pendentes</h3>
              <ul className="analysis-list">
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
