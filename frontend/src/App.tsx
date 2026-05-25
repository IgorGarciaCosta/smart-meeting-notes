import {
  BrowserRouter,
  Routes,
  Route,
  NavLink,
  useLocation,
} from "react-router-dom";
import RecordPage from "./pages/RecordPage.tsx";
import MeetingsPage from "./pages/MeetingsPage.tsx";
import MeetingDetailPage from "./pages/MeetingDetailPage.tsx";

function AppRoutes() {
  const location = useLocation();
  const isRecordPage = location.pathname === "/";

  return (
    <>
      {/* RecordPage is always mounted to preserve state; hidden via CSS when not active */}
      <div style={{ display: isRecordPage ? "block" : "none" }}>
        <RecordPage />
      </div>

      {!isRecordPage && (
        <Routes location={location}>
          <Route path="/meetings" element={<MeetingsPage />} />
          <Route path="/meetings/:id" element={<MeetingDetailPage />} />
        </Routes>
      )}
    </>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <nav className="nav">
        <a href="/" className="nav-brand">
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z" />
            <path d="M19 10v2a7 7 0 0 1-14 0v-2" />
            <line x1="12" y1="19" x2="12" y2="23" />
            <line x1="8" y1="23" x2="16" y2="23" />
          </svg>
          Smart Meeting Notes
        </a>
        <NavLink
          to="/"
          end
          className={({ isActive }) => `nav-link ${isActive ? "active" : ""}`}
        >
          Gravar
        </NavLink>
        <NavLink
          to="/meetings"
          className={({ isActive }) => `nav-link ${isActive ? "active" : ""}`}
        >
          Reuniões
        </NavLink>
      </nav>

      <AppRoutes />
    </BrowserRouter>
  );
}
