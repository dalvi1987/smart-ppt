import { useState, useCallback } from "react";
import { templateApi } from "../services/api";
import type { Template } from "../types";

interface Props {
  template: Template;
  onClose: () => void;
  onScriptUpdated: (updated: Template) => void;
}

export function ScriptViewerModal({
  template,
  onClose,
  onScriptUpdated,
}: Props) {
  const [copied, setCopied] = useState(false);
  const [regenerating, setRegenerating] = useState(false);
  const [regenError, setRegenError] = useState<string | null>(null);

  const script = template.generatedScript;

  // ─── Copy ─────────────────────────────────────────────────────────────────
  const handleCopy = useCallback(async () => {
    if (!script) return;
    await navigator.clipboard.writeText(script);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }, [script]);

  // ─── Download ─────────────────────────────────────────────────────────────
  const handleDownload = useCallback(() => {
    if (!script) return;
    const blob = new Blob([script], { type: "application/javascript" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `${template.name.replace(/\s+/g, "_")}_scaffold.js`;
    a.click();
    URL.revokeObjectURL(url);
  }, [script, template.name]);

  // ─── Regenerate ───────────────────────────────────────────────────────────
  const handleRegenerate = useCallback(async () => {
    setRegenerating(true);
    setRegenError(null);
    try {
      const updated = await templateApi.regenerateScript(template.id);
      onScriptUpdated(updated);
    } catch (err: any) {
      setRegenError(err.message ?? "Regeneration failed");
    } finally {
      setRegenerating(false);
    }
  }, [template.id, onScriptUpdated]);

  const lineCount = script ? script.split("\n").length : 0;
  const sizeKb = script ? (new Blob([script]).size / 1024).toFixed(1) : "0";

  return (
    <div
      className="modal-overlay"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div
        className="modal"
        onClick={(e) => e.stopPropagation()}
        style={{
          maxWidth: 900,
          width: "calc(100vw - 48px)",
          height: "82vh",
          display: "flex",
          flexDirection: "column",
          padding: 0,
          overflow: "hidden",
        }}
      >
        {/* ── Header ── */}
        <div
          className="modal-header"
          style={{
            background: "#185FA5",
            padding: "14px 18px",
            borderBottom: "none",
            flexShrink: 0,
          }}
        >
          <div>
            <div style={{ fontSize: 14, fontWeight: 600, color: "#fff" }}>
              PptxGenJS Script
            </div>
            <div style={{ fontSize: 11, color: "#B5D4F4", marginTop: 2 }}>
              {template.name}
              {template.scriptGeneratedAt && (
                <span style={{ opacity: 0.75 }}>
                  {" "}
                  · Generated{" "}
                  {new Date(template.scriptGeneratedAt).toLocaleString()}
                </span>
              )}
            </div>
          </div>

          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            {/* Regenerate */}
            <button
              className="btn btn-sm"
              onClick={handleRegenerate}
              disabled={regenerating}
              style={{
                background: "rgba(255,255,255,0.12)",
                border: "0.5px solid rgba(255,255,255,0.3)",
                color: "#fff",
              }}
            >
              <svg
                viewBox="0 0 16 16"
                width="12"
                height="12"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.5"
                style={{
                  animation: regenerating ? "spin 1s linear infinite" : "none",
                }}
              >
                <path d="M13.5 8a5.5 5.5 0 1 1-1.1-3.3" strokeLinecap="round" />
                <path
                  d="M13.5 2.5v3h-3"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              </svg>
              {regenerating ? "Regenerating…" : "Regenerate"}
            </button>

            {/* Copy */}
            <button
              className="btn btn-sm"
              onClick={handleCopy}
              disabled={!script}
              style={{
                background: "rgba(255,255,255,0.12)",
                border: "0.5px solid rgba(255,255,255,0.3)",
                color: "#fff",
              }}
            >
              {copied ? (
                <>
                  <svg
                    viewBox="0 0 16 16"
                    width="12"
                    height="12"
                    fill="none"
                    stroke="#6ee7b7"
                    strokeWidth="1.8"
                  >
                    <path
                      d="M2 8l4 4 8-8"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                  </svg>
                  Copied!
                </>
              ) : (
                <>
                  <svg
                    viewBox="0 0 16 16"
                    width="12"
                    height="12"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="1.3"
                  >
                    <rect x="5" y="5" width="9" height="9" rx="1.5" />
                    <path d="M11 5V3a1 1 0 0 0-1-1H3a1 1 0 0 0-1 1v7a1 1 0 0 0 1 1h2" />
                  </svg>
                  Copy
                </>
              )}
            </button>

            {/* Download */}
            <button
              className="btn btn-sm"
              onClick={handleDownload}
              disabled={!script}
              style={{
                background: "#fff",
                border: "0.5px solid #fff",
                color: "#185FA5",
                fontWeight: 500,
              }}
            >
              <svg
                viewBox="0 0 16 16"
                width="12"
                height="12"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.5"
              >
                <path
                  d="M8 2v8M5 7l3 3 3-3"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
                <path d="M3 13h10" strokeLinecap="round" />
              </svg>
              Download .js
            </button>

            {/* Close */}
            <button
              className="icon-btn"
              onClick={onClose}
              style={{ color: "rgba(255,255,255,0.7)" }}
            >
              <svg
                viewBox="0 0 16 16"
                width="16"
                height="16"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.5"
              >
                <path d="M3 3l10 10M13 3L3 13" strokeLinecap="round" />
              </svg>
            </button>
          </div>
        </div>

        {/* ── Error banner ── */}
        {regenError && (
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 8,
              padding: "9px 18px",
              background: "#FAECE7",
              borderBottom: "0.5px solid #F0997B",
              fontSize: 12,
              color: "#4A1B0C",
              flexShrink: 0,
            }}
          >
            <svg
              viewBox="0 0 16 16"
              width="13"
              height="13"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.5"
            >
              <circle cx="8" cy="8" r="6" />
              <path d="M8 5v3M8 10.5v.5" strokeLinecap="round" />
            </svg>
            {regenError}
          </div>
        )}

        {/* ── Script viewer ── */}
        <div style={{ flex: 1, overflow: "auto", background: "#fff" }}>
          {script ? (
            <ScriptCode code={script} />
          ) : (
            <div
              style={{
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                justifyContent: "center",
                height: "100%",
                gap: 12,
                color: "#666",
              }}
            >
              <svg
                viewBox="0 0 24 24"
                width="36"
                height="36"
                fill="none"
                stroke="#FAEEDA"
                strokeWidth="1.3"
              >
                <circle cx="12" cy="12" r="10" />
                <path d="M12 8v4M12 16h.01" strokeLinecap="round" />
              </svg>
              <p style={{ fontSize: 13, color: "#888" }}>
                No script generated for this template yet.
              </p>
              <button
                className="btn btn-primary btn-sm"
                onClick={handleRegenerate}
                disabled={regenerating}
              >
                Generate Now
              </button>
            </div>
          )}
        </div>

        {/* ── Footer stats ── */}
        {script && (
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 16,
              padding: "8px 18px",
              borderTop: "0.5px solid #e5e3dc",
              background: "#fafaf9",
              fontSize: 11,
              color: "#999",
              flexShrink: 0,
            }}
          >
            <span>{lineCount} lines</span>
            <span>{sizeKb} KB</span>
            <span style={{ marginLeft: "auto" }}>
              Run with:{" "}
              <code
                style={{
                  background: "#f0eee8",
                  padding: "1px 6px",
                  borderRadius: 4,
                  fontFamily: "monospace",
                  fontSize: 11,
                  color: "#185FA5",
                }}
              >
                node {template.name.replace(/\s+/g, "_")}_scaffold.js
              </code>
            </span>
          </div>
        )}
      </div>

      {/* spin keyframe */}
      <style>{`@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }`}</style>
    </div>
  );
}

// ─── Syntax highlighted code block ────────────────────────────────────────────
function ScriptCode({ code }: { code: string }) {
  const lines = code.split("\n");
  return (
    <pre
      style={{
        margin: 0,
        padding: "16px 0",
        fontFamily: "'Fira Code', 'Cascadia Code', Consolas, monospace",
        fontSize: 12.5,
        lineHeight: 1.65,
        minHeight: "100%",
      }}
    >
      {lines.map((line, i) => (
        <div key={i} style={{ display: "flex", paddingRight: 24 }}>
          {/* Line number */}
          <span
            style={{
              userSelect: "none",
              minWidth: 48,
              paddingRight: 16,
              textAlign: "right",
              color: "#3d4450",
              fontSize: 11.5,
              lineHeight: "inherit",
              flexShrink: 0,
            }}
          >
            {i + 1}
          </span>
          {/* Code */}
          <span
            style={{ flex: 1, whiteSpace: "pre" }}
            dangerouslySetInnerHTML={{ __html: highlightLine(line) }}
          />
        </div>
      ))}
    </pre>
  );
}

function highlightLine(raw: string): string {
  // Escape HTML
  let h = raw
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;");

  // Comment lines
  if (h.trimStart().startsWith("//")) {
    return `<span style="color:#6a9955">${h}</span>`;
  }

  // String literals (backtick, single, double)
  h = h.replace(
    /(`[^`]*`|'[^']*'|"[^"]*")/g,
    '<span style="color:#ce9178">$1</span>',
  );
  // Keywords
  h = h.replace(
    /\b(const|let|var|function|return|if|else|for|of|in|new|await|async|require|module|exports|null|true|false|undefined)\b/g,
    '<span style="color:#569cd6">$1</span>',
  );
  // Numbers
  h = h.replace(
    /(?<![a-zA-Z#"'`])\b(\d+\.?\d*)\b(?![a-zA-Z"'`])/g,
    '<span style="color:#b5cea8">$1</span>',
  );
  // Object keys  (word:)
  h = h.replace(
    /\b([a-zA-Z_]\w*)\s*(?=:(?!:))/g,
    '<span style="color:#9cdcfe">$1</span>',
  );
  // Function calls  (word()
  h = h.replace(
    /\b([a-zA-Z_]\w*)\s*(?=\()/g,
    '<span style="color:#dcdcaa">$1</span>',
  );

  return `<span style="color:#d4d4d4">${h}</span>`;
}
