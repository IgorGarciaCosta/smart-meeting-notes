import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getAllMeetings, deleteMeeting } from "../api/meetings.ts";
import type { Meeting } from "../api/types.ts";
import { MeetingStatus } from "../api/types.ts";

function statusBadge(status: MeetingStatus) {
  const map: Record<string, { cls: string; label: string }> = {
    [MeetingStatus.AwaitingChunks]: { cls: "badge--info", label: "Waiting" },
    [MeetingStatus.Finalizing]: { cls: "badge--warning", label: "Finalizing" },
    [MeetingStatus.Analyzing]: { cls: "badge--warning", label: "Analyzing" },
    [MeetingStatus.Completed]: { cls: "badge--success", label: "Completed" },
    [MeetingStatus.Failed]: { cls: "badge--danger", label: "Failed" },
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
  const [deleteTarget, setDeleteTarget] = useState<Meeting | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [showDeleteAll, setShowDeleteAll] = useState(false);
  const [deletingAll, setDeletingAll] = useState(false);

  useEffect(() => {
    getAllMeetings()
      .then(setMeetings)
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false));
  }, []);

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await deleteMeeting(deleteTarget.id);
      setMeetings((prev) => prev.filter((m) => m.id !== deleteTarget.id));
      setDeleteTarget(null);
    } catch (e) {
      setError(String(e));
    } finally {
      setDeleting(false);
    }
  };

  const handleDeleteAll = async () => {
    setDeletingAll(true);
    try {
      await Promise.all(meetings.map((m) => deleteMeeting(m.id)));
      setMeetings([]);
      setShowDeleteAll(false);
    } catch (e) {
      setError(String(e));
    } finally {
      setDeletingAll(false);
    }
  };

  if (loading) return <div className="loading">Loading meetings...</div>;
  if (error)
    return (
      <div className="page">
        <div className="alert alert--error">
          <span>{error}</span>
        </div>
      </div>
    );

  return (
    <div className="page">
      <div className="page-header">
        <h1>Meetings</h1>
        <p>{meetings.length} meeting(s) recorded</p>
      </div>

      {meetings.length === 0 ? (
        <div className="empty-state">
          <p>No meetings recorded yet.</p>
        </div>
      ) : (
        <>
          <div
            style={{
              display: "flex",
              justifyContent: "flex-end",
              marginBottom: 12,
            }}
          >
            <button
              className="btn btn--danger"
              style={{ padding: "8px 16px", fontSize: 13 }}
              onClick={() => setShowDeleteAll(true)}
            >
              Delete All
            </button>
          </div>
          <div className="card" style={{ padding: 0, overflow: "hidden" }}>
            <div className="table-wrapper">
              <table className="table">
                <thead>
                  <tr>
                    <th>Title</th>
                    <th>Status</th>
                    <th>Date</th>
                    <th></th>
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
                        {new Date(m.uploadedAt).toLocaleString("en-US")}
                      </td>
                      <td>
                        <button
                          className="btn-icon btn-icon--danger"
                          title="Delete meeting"
                          onClick={() => setDeleteTarget(m)}
                        >
                          <svg
                            width="16"
                            height="16"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                          >
                            <polyline points="3 6 5 6 21 6" />
                            <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                            <line x1="10" y1="11" x2="10" y2="17" />
                            <line x1="14" y1="11" x2="14" y2="17" />
                          </svg>
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}

      {/* Delete All confirmation modal */}
      {showDeleteAll && (
        <div
          className="modal-overlay"
          onClick={() => !deletingAll && setShowDeleteAll(false)}
        >
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Delete All Meetings</h2>
            <p>
              Are you sure you want to delete all{" "}
              <strong>{meetings.length}</strong> meetings? This action cannot be
              undone.
            </p>
            <div className="modal-actions">
              <button
                className="btn btn--secondary"
                onClick={() => setShowDeleteAll(false)}
                disabled={deletingAll}
              >
                Cancel
              </button>
              <button
                className="btn btn--danger"
                onClick={handleDeleteAll}
                disabled={deletingAll}
              >
                {deletingAll ? "Deleting..." : "Delete All"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Delete confirmation modal */}
      {deleteTarget && (
        <div
          className="modal-overlay"
          onClick={() => !deleting && setDeleteTarget(null)}
        >
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Delete Meeting</h2>
            <p>
              Are you sure you want to delete{" "}
              <strong>{deleteTarget.title}</strong>? This action cannot be
              undone.
            </p>
            <div className="modal-actions">
              <button
                className="btn btn--secondary"
                onClick={() => setDeleteTarget(null)}
                disabled={deleting}
              >
                Cancel
              </button>
              <button
                className="btn btn--danger"
                onClick={handleDelete}
                disabled={deleting}
              >
                {deleting ? "Deleting..." : "Delete"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
