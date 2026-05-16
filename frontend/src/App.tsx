import { BrowserRouter, Routes, Route, Link } from "react-router-dom";
import RecordPage from "./pages/RecordPage.tsx";
import MeetingsPage from "./pages/MeetingsPage.tsx";
import MeetingDetailPage from "./pages/MeetingDetailPage.tsx";

export default function App() {
  return (
    <BrowserRouter>
      <nav
        style={{
          display: "flex",
          gap: 16,
          padding: "12px 24px",
          borderBottom: "1px solid #ddd",
          background: "#f8f9fa",
        }}
      >
        <Link to="/" style={{ fontWeight: 600, textDecoration: "none" }}>
          🎙️ Gravar
        </Link>
        <Link to="/meetings" style={{ fontWeight: 600, textDecoration: "none" }}>
          📋 Reuniões
        </Link>
      </nav>

      <Routes>
        <Route path="/" element={<RecordPage />} />
        <Route path="/meetings" element={<MeetingsPage />} />
        <Route path="/meetings/:id" element={<MeetingDetailPage />} />
      </Routes>
    </BrowserRouter>
  );
}
