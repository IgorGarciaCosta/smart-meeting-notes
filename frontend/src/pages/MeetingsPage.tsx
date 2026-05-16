import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getAllMeetings } from "../api/meetings.ts";
import type { Meeting } from "../api/types.ts";
import { MeetingStatus } from "../api/types.ts";

const statusColors: Record<string, string> = {
  [MeetingStatus.Uploaded]: "#6c757d",
  [MeetingStatus.AwaitingChunks]: "#17a2b8",
  [MeetingStatus.Transcribing]: "#ffc107",
  [MeetingStatus.Finalizing]: "#ffc107",
  [MeetingStatus.Analyzing]: "#fd7e14",
  [MeetingStatus.Completed]: "#28a745",
  [MeetingStatus.Failed]: "#dc3545",
};

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

  if (loading) return <p style={{ padding: 24 }}>Carregando...</p>;
  if (error) return <p style={{ padding: 24, color: "red" }}>{error}</p>;

  return (
    <div style={{ maxWidth: 700, margin: "0 auto", padding: 24 }}>
      <h1>📋 Reuniões</h1>

      {meetings.length === 0 ? (
        <p>Nenhuma reunião encontrada.</p>
      ) : (
        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <thead>
            <tr style={{ borderBottom: "2px solid #ccc", textAlign: "left" }}>
              <th style={{ padding: 8 }}>Título</th>
              <th style={{ padding: 8 }}>Status</th>
              <th style={{ padding: 8 }}>Data</th>
            </tr>
          </thead>
          <tbody>
            {meetings.map((m) => (
              <tr key={m.id} style={{ borderBottom: "1px solid #eee" }}>
                <td style={{ padding: 8 }}>
                  <Link to={`/meetings/${m.id}`}>{m.title}</Link>
                </td>
                <td style={{ padding: 8 }}>
                  <span
                    style={{
                      display: "inline-block",
                      padding: "2px 8px",
                      borderRadius: 4,
                      fontSize: 12,
                      fontWeight: 600,
                      color: "#fff",
                      background: statusColors[m.status] || "#999",
                    }}
                  >
                    {m.status}
                  </span>
                </td>
                <td style={{ padding: 8, fontSize: 13, color: "#666" }}>
                  {new Date(m.uploadedAt).toLocaleString("pt-BR")}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
