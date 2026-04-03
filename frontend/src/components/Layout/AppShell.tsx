import React from "react";
import { useAppStore } from "../../store/appStore";
import type { AppState } from "../../store/appStore";

const NAV_ITEMS: Array<{
  key: AppState["activeScreen"];
  label: string;
  icon: React.ReactNode;
}> = [
  { key: "templates", label: "Templates", icon: <TemplateIcon /> },
  { key: "ai", label: "AI Generator", icon: <AiIcon /> },
  { key: "mapping", label: "Template Mapping", icon: <MappingIcon /> },
  { key: "preview", label: "Preview & Export", icon: <PreviewIcon /> },
  { key: "history", label: "History", icon: <HistoryIcon /> },
];

export function AppShell({ children }: { children: React.ReactNode }) {
  const {
    activeScreen,
    setActiveScreen,
    sidebarOpen,
    toggleSidebar,
    notifications,
    removeNotification,
  } = useAppStore();

  return (
    <div className="app-shell">
      {/* Sidebar */}
      <aside className={`sidebar ${sidebarOpen ? "open" : "collapsed"}`}>
        <div className="sidebar-header">
          <div className="logo">
            <div className="logo-icon">
              <svg viewBox="0 0 24 24" width="28" height="28" fill="none">
                <rect
                  x="3"
                  y="4"
                  width="12"
                  height="16"
                  rx="2"
                  stroke="#E4761B"
                  stroke-width="2"
                ></rect>
                <circle
                  cx="16"
                  cy="12"
                  r="8"
                  fill="#E4761B"
                  opacity="0.2"
                ></circle>
                <path
                  d="M6 8h3a2.2 2.2 0 0 1 0 4.4H6z"
                  stroke="#E4761B"
                  stroke-width="2"
                  fill="none"
                ></path>
              </svg>
            </div>
            {sidebarOpen && (
              <div>
                <div className="logo-name">SmartPPT</div>
                <div className="logo-tagline">AI Presentation Platform</div>
              </div>
            )}
          </div>
          <button
            className="icon-btn"
            onClick={toggleSidebar}
            title="Toggle sidebar"
          >
            <svg
              viewBox="0 0 16 16"
              width="16"
              height="16"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.3"
            >
              {sidebarOpen ? (
                <path
                  d="M10 4L6 8l4 4"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              ) : (
                <path
                  d="M6 4l4 4-4 4"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              )}
            </svg>
          </button>
        </div>

        <nav className="sidebar-nav">
          <div className="nav-section-label">{sidebarOpen && "Workspace"}</div>
          {NAV_ITEMS.map((item) => (
            <button
              key={item.key}
              className={`nav-item ${activeScreen === item.key ? "active" : ""}`}
              onClick={() => setActiveScreen(item.key)}
              title={!sidebarOpen ? item.label : undefined}
            >
              <span className="nav-icon">{item.icon}</span>
              {sidebarOpen && <span className="nav-label">{item.label}</span>}
            </button>
          ))}
        </nav>

        <div className="sidebar-footer">
          <div className="user-pill">
            <div className="avatar">AK</div>
            {sidebarOpen && (
              <div className="user-info">
                <div className="user-name">Shekhar Dalvi</div>
                <div className="user-role">Admin</div>
              </div>
            )}
          </div>
        </div>
      </aside>

      {/* Main content */}
      <main className="main-content">
        <header className="topbar">
          <h1 className="page-title">
            {NAV_ITEMS.find((i) => i.key === activeScreen)?.label}
          </h1>
          <div className="topbar-actions">
            <button
              className="btn btn-ghost btn-sm"
              onClick={() => setActiveScreen("templates")}
            >
              + New Presentation
            </button>
          </div>
        </header>
        <div className="page-content">{children}</div>
      </main>

      {/* Notifications */}
      <div className="notifications">
        {notifications.map((n) => (
          <div key={n.id} className={`notification notification-${n.type}`}>
            <span>{n.message}</span>
            <button
              className="notif-close"
              onClick={() => removeNotification(n.id)}
            >
              ×
            </button>
          </div>
        ))}
      </div>
    </div>
  );
}

// Icons
function TemplateIcon() {
  return (
    <svg
      viewBox="0 0 16 16"
      width="15"
      height="15"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.3"
    >
      <rect x="2" y="3" width="12" height="10" rx="1.5" />
      <path d="M5 7h6M5 9.5h4" strokeLinecap="round" />
    </svg>
  );
}
function AiIcon() {
  return (
    <svg
      viewBox="0 0 16 16"
      width="15"
      height="15"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.3"
    >
      <circle cx="8" cy="8" r="6" />
      <path
        d="M6 6.5c0-1 .9-1.5 2-1.5s2 .7 2 1.5c0 1.5-2 1.5-2 2.5M8 11v.5"
        strokeLinecap="round"
      />
    </svg>
  );
}
function MappingIcon() {
  return (
    <svg
      viewBox="0 0 16 16"
      width="15"
      height="15"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.3"
    >
      <path
        d="M2 4h5M9 4h5M4 8h4M10 8h2M2 12h6M10 12h4"
        strokeLinecap="round"
      />
    </svg>
  );
}
function PreviewIcon() {
  return (
    <svg
      viewBox="0 0 16 16"
      width="15"
      height="15"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.3"
    >
      <path d="M2 8s2-4 6-4 6 4 6 4-2 4-6 4-6-4-6-4z" />
      <circle cx="8" cy="8" r="1.5" />
    </svg>
  );
}
function HistoryIcon() {
  return (
    <svg
      viewBox="0 0 16 16"
      width="15"
      height="15"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.3"
    >
      <path d="M8 2v4l2 2M3 8a5 5 0 1 0 10 0A5 5 0 0 0 3 8z" />
    </svg>
  );
}
