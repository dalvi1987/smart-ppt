import React, { useEffect, useRef, useState } from "react";
import { templateApi } from "../services/api";
import { useAppStore } from "../store/appStore";
import { ScriptViewerModal } from "../components/ScriptViewerModal";
import type { Template, TemplateCategory } from "../types";

const CATEGORIES: TemplateCategory[] = [
  "Consulting",
  "Sales",
  "Finance",
  "Product",
  "General",
  "Custom",
];

export function TemplatesPage() {
  const {
    templates,
    setTemplates,
    selectedTemplate,
    selectTemplate,
    setTemplatesLoading,
    addTemplate,
    addNotification,
    setActiveScreen,
  } = useAppStore();
  const [uploading, setUploading] = useState(false);
  const [dragOver, setDragOver] = useState(false);
  const [showUploadForm, setShowUploadForm] = useState(false);
  const [uploadForm, setUploadForm] = useState({
    name: "",
    description: "",
    category: "Consulting" as TemplateCategory,
  });
  const [pendingFile, setPendingFile] = useState<File | null>(null);
  const [filter, setFilter] = useState<TemplateCategory | "All">("All");
  const [scriptTemplate, setScriptTemplate] = useState<Template | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    setTemplatesLoading(true);
    templateApi
      .getAll()
      .then(setTemplates)
      .catch(() =>
        addNotification({ type: "error", message: "Failed to load templates" }),
      )
      .finally(() => setTemplatesLoading(false));
  }, []);

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    const file = e.dataTransfer.files[0];
    if (file?.name.endsWith(".pptx")) {
      setPendingFile(file);
      setShowUploadForm(true);
    } else
      addNotification({ type: "error", message: "Please drop a .pptx file" });
  };

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      setPendingFile(file);
      setUploadForm((f) => ({ ...f, name: file.name.replace(".pptx", "") }));
      setShowUploadForm(true);
    }
  };

  const handleUpload = async () => {
    if (!pendingFile || !uploadForm.name) return;
    setUploading(true);
    try {
      const template = await templateApi.upload(
        pendingFile,
        uploadForm.name,
        uploadForm.description,
        uploadForm.category,
      );
      addTemplate(template);
      selectTemplate(template);
      setShowUploadForm(false);
      setPendingFile(null);
      addNotification({
        type: "success",
        message: `Template "${template.name}" uploaded — ${template.placeholderCount} placeholders detected`,
      });
      // Auto-open script viewer if script was generated on upload
      if (template.generatedScript) {
        setScriptTemplate(template);
      }
    } catch (e: any) {
      addNotification({ type: "error", message: e.message });
    } finally {
      setUploading(false);
    }
  };

  const handleDelete = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (!confirm("Delete this template?")) return;
    try {
      await templateApi.delete(id);
      useAppStore.getState().removeTemplate(id);
      if (selectedTemplate?.id === id) selectTemplate(null);
      addNotification({ type: "success", message: "Template deleted" });
    } catch {
      addNotification({ type: "error", message: "Delete failed" });
    }
  };

  // Keep store + modal in sync when script is regenerated
  const handleScriptUpdated = (updated: Template) => {
    setTemplates(
      useAppStore.getState().templates.map((t) =>
        t.id === updated.id ? updated : t,
      ),
    );
    if (selectedTemplate?.id === updated.id) selectTemplate(updated);
    setScriptTemplate(updated);
    addNotification({ type: "success", message: "Script regenerated" });
  };

  const filtered =
    filter === "All"
      ? templates
      : templates.filter((t) => t.category === filter);

  return (
    <div className="page-container">
      {/* Upload zone */}
      <section
        className={`upload-zone ${dragOver ? "drag-active" : ""}`}
        onDrop={handleDrop}
        onDragOver={(e) => {
          e.preventDefault();
          setDragOver(true);
        }}
        onDragLeave={() => setDragOver(false)}
        onClick={() => fileRef.current?.click()}
      >
        <input
          ref={fileRef}
          type="file"
          accept=".pptx"
          style={{ display: "none" }}
          onChange={handleFileSelect}
        />
        <div className="upload-icon-wrap">
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.5"
            width="28"
            height="28"
          >
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
            <polyline points="17 8 12 3 7 8" />
            <line x1="12" y1="3" x2="12" y2="15" />
          </svg>
        </div>
        <div className="upload-text">
          <strong>Drop your .pptx template here</strong>
          <span>or click to browse · Max 50MB · .pptx only</span>
        </div>
      </section>

      {/* Upload form modal */}
      {showUploadForm && (
        <div className="modal-overlay" onClick={() => setShowUploadForm(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Configure template</h3>
              <button
                className="icon-btn"
                onClick={() => setShowUploadForm(false)}
              >
                ×
              </button>
            </div>
            <div className="modal-body">
              <div className="form-group">
                <label>Template name *</label>
                <input
                  className="input"
                  value={uploadForm.name}
                  onChange={(e) =>
                    setUploadForm((f) => ({ ...f, name: e.target.value }))
                  }
                  placeholder="e.g. Consulting Deck Q4"
                />
              </div>
              <div className="form-group">
                <label>Description</label>
                <textarea
                  className="input"
                  rows={2}
                  value={uploadForm.description}
                  onChange={(e) =>
                    setUploadForm((f) => ({
                      ...f,
                      description: e.target.value,
                    }))
                  }
                  placeholder="Briefly describe this template..."
                />
              </div>
              <div className="form-group">
                <label>Category</label>
                <select
                  className="input"
                  value={uploadForm.category}
                  onChange={(e) =>
                    setUploadForm((f) => ({
                      ...f,
                      category: e.target.value as TemplateCategory,
                    }))
                  }
                >
                  {CATEGORIES.map((c) => (
                    <option key={c}>{c}</option>
                  ))}
                </select>
              </div>
              <div className="file-info">
                <svg
                  viewBox="0 0 16 16"
                  width="14"
                  height="14"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="1.3"
                >
                  <rect x="3" y="1" width="10" height="14" rx="1" />
                  <path d="M6 5h4M6 8h4M6 11h2" strokeLinecap="round" />
                </svg>
                {pendingFile?.name} (
                {(pendingFile!.size / 1024 / 1024).toFixed(2)} MB)
              </div>
            </div>
            <div className="modal-footer">
              <button
                className="btn btn-ghost"
                onClick={() => setShowUploadForm(false)}
              >
                Cancel
              </button>
              <button
                className="btn btn-primary"
                onClick={handleUpload}
                disabled={uploading || !uploadForm.name}
              >
                {uploading ? "Uploading & generating script…" : "Upload & Parse"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Filter bar */}
      <div className="filter-bar">
        <div className="filter-chips">
          {(["All", ...CATEGORIES] as const).map((cat) => (
            <button
              key={cat}
              className={`chip ${filter === cat ? "active" : ""}`}
              onClick={() => setFilter(cat)}
            >
              {cat}
            </button>
          ))}
        </div>
        <span className="filter-count">
          {filtered.length} template{filtered.length !== 1 ? "s" : ""}
        </span>
      </div>

      {/* Template grid */}
      {filtered.length === 0 ? (
        <div className="empty-state">
          <p>No templates yet. Upload your first .pptx above.</p>
        </div>
      ) : (
        <div className="template-grid">
          {filtered.map((t) => (
            <TemplateCard
              key={t.id}
              template={t}
              selected={selectedTemplate?.id === t.id}
              onSelect={() => selectTemplate(t)}
              onDelete={(e) => handleDelete(t.id, e)}
              onViewScript={(e) => {
                e.stopPropagation();
                setScriptTemplate(t);
              }}
            />
          ))}
        </div>
      )}

      {/* Bottom action bar */}
      {selectedTemplate && (
        <div className="action-bar">
          <div className="action-bar-info">
            <strong>{selectedTemplate.name}</strong>
            <span>
              {selectedTemplate.layoutCount} layouts ·{" "}
              {selectedTemplate.placeholderCount} placeholders
            </span>
          </div>
          <div className="action-bar-btns">
            <button
              className="btn btn-ghost btn-sm"
              onClick={() => setScriptTemplate(selectedTemplate)}
            >
              {/* code icon */}
              <svg viewBox="0 0 16 16" width="13" height="13" fill="none" stroke="currentColor" strokeWidth="1.4">
                <path d="M5 4L1 8l4 4M11 4l4 4-4 4M9 2l-2 12" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
              View Script
            </button>
            <button
              className="btn btn-ghost btn-sm"
              onClick={() => setActiveScreen("ai")}
            >
              Generate with AI →
            </button>
            <button
              className="btn btn-primary btn-sm"
              onClick={() => setActiveScreen("mapping")}
            >
              Map placeholders →
            </button>
          </div>
        </div>
      )}

      {/* Script viewer modal */}
      {scriptTemplate && (
        <ScriptViewerModal
          template={scriptTemplate}
          onClose={() => setScriptTemplate(null)}
          onScriptUpdated={handleScriptUpdated}
        />
      )}
    </div>
  );
}

function TemplateCard({
  template,
  selected,
  onSelect,
  onDelete,
  onViewScript,
}: {
  template: Template;
  selected: boolean;
  onSelect: () => void;
  onDelete: (e: React.MouseEvent) => void;
  onViewScript: (e: React.MouseEvent) => void;
}) {
  const COLORS: Record<string, string> = {
    Consulting: "#185FA5",
    Sales: "#D85A30",
    Finance: "#0F6E56",
    Product: "#D4537E",
    General: "#888780",
    Custom: "#534AB7",
  };
  const color = COLORS[template.category] ?? "#185FA5";
  const hasScript = Boolean(template.generatedScript);

  return (
    <div
      className={`template-card ${selected ? "selected" : ""}`}
      onClick={onSelect}
    >
      <div className="template-thumb" style={{ background: `${color}18` }}>
        <div className="slide-mini">
          <div
            className="slide-mini-bar"
            style={{ background: color, width: "70%" }}
          />
          <div className="slide-mini-line" style={{ width: "90%" }} />
          <div className="slide-mini-line" style={{ width: "60%" }} />
        </div>
        {selected && <div className="selected-check">✓</div>}

        {/* Script status badge — top-left of thumb */}
        <div
          style={{
            position: "absolute",
            top: 6,
            left: 6,
            display: "flex",
            alignItems: "center",
            gap: 3,
            fontSize: 9,
            fontWeight: 600,
            padding: "2px 6px",
            borderRadius: 4,
            background: hasScript ? "#EAF3DE" : "#f5f4f1",
            color: hasScript ? "#3B6D11" : "#aaa",
            border: `0.5px solid ${hasScript ? "#9FE1CB" : "#e0ded8"}`,
            letterSpacing: "0.02em",
          }}
        >
          <svg viewBox="0 0 10 10" width="8" height="8" fill="none" stroke="currentColor" strokeWidth="1.5">
            {hasScript ? (
              <path d="M1.5 5l2.5 2.5 5-5" strokeLinecap="round" strokeLinejoin="round" />
            ) : (
              <>
                <path d="M3 2.5L1 5l2 2.5M7 2.5L9 5l-2 2.5" strokeLinecap="round" />
                <path d="M6 2l-2 6" strokeLinecap="round" />
              </>
            )}
          </svg>
          {hasScript ? "Script ready" : "No script"}
        </div>
      </div>

      <div className="template-info">
        <div className="template-name">{template.name}</div>
        <div className="template-meta">
          {template.layoutCount} layouts · {template.placeholderCount}{" "}
          placeholders
        </div>
        <div className="template-footer">
          <span
            className="category-badge"
            style={{ background: `${color}18`, color }}
          >
            {template.category}
          </span>

          <div style={{ display: "flex", alignItems: "center", gap: 4 }}>
            {/* View script button */}
            <button
              title={hasScript ? "View PptxGenJS script" : "No script yet — click to generate"}
              onClick={onViewScript}
              style={{
                background: "none",
                border: "none",
                color: hasScript ? "#185FA5" : "#ccc",
                padding: "3px",
                borderRadius: 4,
                display: "flex",
                transition: "0.12s",
                cursor: "pointer",
              }}
              onMouseEnter={(e) =>
                (e.currentTarget.style.background = hasScript ? "#E6F1FB" : "#f5f4f1")
              }
              onMouseLeave={(e) =>
                (e.currentTarget.style.background = "none")
              }
            >
              <svg viewBox="0 0 16 16" width="13" height="13" fill="none" stroke="currentColor" strokeWidth="1.3">
                <path d="M5 4L1 8l4 4M11 4l4 4-4 4M9 2l-2 12" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            </button>

            <button
              className="delete-btn"
              onClick={onDelete}
              title="Delete template"
            >
              <svg
                viewBox="0 0 16 16"
                width="13"
                height="13"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.3"
              >
                <path
                  d="M2 4h12M6 4V2h4v2M5 4v9a1 1 0 0 0 1 1h4a1 1 0 0 0 1-1V4"
                  strokeLinecap="round"
                />
              </svg>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
