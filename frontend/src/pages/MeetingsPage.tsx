import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getAllMeetings } from "../api/meetings.ts";
import type { Meeting } from "../api/types.ts";
import { MeetingStatus } from "../api/types.ts";

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

export default function MeetingsPage() {
  const [meetings, setMeetings] = useState<Meeting[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getAllMeetings()
      .then(setMeetings)
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="loading">Carregando reuniões...</div>;
  if (error) return <div className="page"><div className="alert alert--error"><span>✕</span><span>{error}</span></div></div>;

  return (
    <div className="page">
      <div className="page-header">
        <h1>Reuniões</h1>
        <p>{meetings.length} reunião(ões) registrada(s)</p>
      </div>

      {meetings.length === 0 ? (
        <div className="empty-state">
          <p style={{ fontSize: 32 }}>📭</p>
          <p>Nenhuma reunião gravada ainda.</p>
        </div>
      ) : (
        <div className="card" style={{ padding: 0, overflow: "hidden" }}>
          <div className="table-wrapper">
            <table className="table">
              <thead>
                <tr>
                  <th>Título</th>
                  <th>Status</th>
                  <th>Data</th>
                </tr>
              </thead>
              <tbody>
                {meetings.map((m) => (
                  <tr key={m.id}>
                    <td>
                      <Link to={`/meetings/${m.id}`} className="table-link">
                        {m.title}
                      </Link>
                    </td>
                    <td>{statusBadge(m.status)}</td>
                    <td className="table-date">
                      {new Date(m.uploadedAt).toLocaleString("pt-BR")}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
