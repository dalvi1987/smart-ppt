import { useState } from "react";
import { templateApi } from "../services/api";
import { useAppStore } from "../store/appStore";
import type {
  MappingStatus,
  Placeholder,
  PlaceholderType,
  SlideLayout,
} from "../types";

const STATUS_COLOR: Record<MappingStatus, string> = {
  AutoMapped: "#639922",
  ManualMapped: "#185FA5",
  NeedsReview: "#EF9F27",
  Unmapped: "#888780",
};
const TYPE_COLOR: Record<PlaceholderType, string> = {
  Text: "#185FA5",
  Chart: "#BA7517",
  Table: "#3B6D11",
  Image: "#993556",
  Number: "#534AB7",
  Date: "#0F6E56",
};

export function MappingPage() {
  const { selectedTemplate, setActiveScreen, addNotification } = useAppStore();
  const [activeLayout, setActiveLayout] = useState<SlideLayout | null>(
    selectedTemplate?.layouts[0] ?? null,
  );
  const [mappings, setMappings] = useState<Record<string, string>>(() => {
    const m: Record<string, string> = {};
    selectedTemplate?.layouts.forEach((l) =>
      l.placeholders.forEach((p) => {
        if (p.mappedDataField) m[p.id] = p.mappedDataField;
      }),
    );
    return m;
  });
  const [saving, setSaving] = useState(false);

  if (!selectedTemplate) {
    return (
      <div className="empty-state centered">
        <p>No template selected.</p>
        <button
          className="btn btn-primary"
          onClick={() => setActiveScreen("templates")}
        >
          Go to Templates
        </button>
      </div>
    );
  }

  const allPlaceholders = selectedTemplate.layouts.flatMap(
    (l) => l.placeholders,
  );
  const autoMapped = allPlaceholders.filter(
    (p) => p.mappingStatus === "AutoMapped",
  ).length;
  const needsReview = allPlaceholders.filter(
    (p) => p.mappingStatus === "NeedsReview" || p.mappingStatus === "Unmapped",
  ).length;

  const handleSave = async () => {
    setSaving(true);
    try {
      const payload = Object.entries(mappings).map(
        ([placeholderId, mappedDataField]) => ({
          placeholderId,
          mappedDataField,
          mappingRule: "manual",
        }),
      );
      await templateApi.updateMappings(selectedTemplate.id, payload);
      addNotification({
        type: "success",
        message: "Mappings saved successfully",
      });
      setActiveScreen("preview");
    } catch (e: any) {
      addNotification({ type: "error", message: e.message });
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="page-container">
      {/* Summary bar */}
      <div className="info-banner">
        <svg
          viewBox="0 0 16 16"
          width="14"
          height="14"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.3"
        >
          <circle cx="8" cy="8" r="6" />
          <path d="M8 7v4M8 5.5v.5" strokeLinecap="round" />
        </svg>
        <span>
          {autoMapped} of {allPlaceholders.length} placeholders auto-mapped ·{" "}
          {needsReview} need review
        </span>
        <button
          className="btn btn-primary btn-sm"
          style={{ marginLeft: "auto" }}
          onClick={handleSave}
          disabled={saving}
        >
          {saving ? "Saving..." : "Save & Continue →"}
        </button>
      </div>

      <div className="mapping-layout">
        {/* Layout list */}
        <div className="card layout-panel">
          <h4>Slide layouts</h4>
          <ul className="layout-list">
            {selectedTemplate.layouts.map((layout) => {
              const unmapped = layout.placeholders.filter(
                (p) => !mappings[p.id] && !p.mappedDataField,
              ).length;
              return (
                <li
                  key={layout.id}
                  className={`layout-item ${activeLayout?.id === layout.id ? "active" : ""}`}
                  onClick={() => setActiveLayout(layout)}
                >
                  <div className="layout-thumb-mini">
                    <div
                      style={{
                        height: 4,
                        background: "#185FA5",
                        borderRadius: 2,
                        width: "80%",
                        marginBottom: 2,
                      }}
                    />
                    <div
                      style={{
                        height: 2,
                        background: "#ddd",
                        borderRadius: 1,
                        width: "70%",
                        marginBottom: 2,
                      }}
                    />
                    <div
                      style={{
                        height: 2,
                        background: "#ddd",
                        borderRadius: 1,
                        width: "55%",
                      }}
                    />
                  </div>
                  <div className="layout-item-info">
                    <div className="layout-item-name">{layout.name}</div>
                    <div className="layout-item-sub">
                      {layout.placeholders.length} placeholders
                    </div>
                  </div>
                  {unmapped > 0 && (
                    <span className="unmapped-badge">{unmapped}</span>
                  )}
                </li>
              );
            })}
          </ul>
        </div>

        {/* Placeholder mappings for selected layout */}
        <div className="card mapping-panel">
          <h4>Placeholders — {activeLayout?.name}</h4>
          {activeLayout ? (
            <div className="placeholder-list">
              {activeLayout.placeholders.map((ph) => (
                <PlaceholderRow
                  key={ph.id}
                  placeholder={ph}
                  value={mappings[ph.id] ?? ph.mappedDataField ?? ""}
                  onChange={(v) => setMappings((m) => ({ ...m, [ph.id]: v }))}
                />
              ))}
            </div>
          ) : (
            <p className="muted">Select a layout to view its placeholders.</p>
          )}
        </div>
      </div>

      {/* Full mapping table */}
      <div className="card" style={{ marginTop: 16 }}>
        <div className="card-header-row">
          <h4>All mappings</h4>
          <span className="muted-sm">{selectedTemplate.name}</span>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Placeholder</th>
                <th>Type</th>
                <th>Layout</th>
                <th>Mapped to</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {selectedTemplate.layouts.flatMap((l) =>
                l.placeholders.map((ph) => (
                  <tr key={ph.id}>
                    <td>
                      <code className="token">{ph.token}</code>
                    </td>
                    <td>
                      <span
                        className="type-badge"
                        style={{
                          background: `${TYPE_COLOR[ph.type]}18`,
                          color: TYPE_COLOR[ph.type],
                        }}
                      >
                        {ph.type}
                      </span>
                    </td>
                    <td className="muted-sm">{l.name}</td>
                    <td>
                      <input
                        className="input input-sm"
                        value={mappings[ph.id] ?? ph.mappedDataField ?? ""}
                        onChange={(e) =>
                          setMappings((m) => ({
                            ...m,
                            [ph.id]: e.target.value,
                          }))
                        }
                        placeholder="data.fieldName"
                      />
                    </td>
                    <td>
                      <span
                        className="status-dot"
                        style={{ background: STATUS_COLOR[ph.mappingStatus] }}
                      />
                      <span className="muted-sm">{ph.mappingStatus}</span>
                    </td>
                  </tr>
                )),
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

function PlaceholderRow({
  placeholder,
  value,
  onChange,
}: {
  placeholder: Placeholder;
  value: string;
  onChange: (v: string) => void;
}) {
  const color = STATUS_COLOR[placeholder.mappingStatus];
  return (
    <div className="ph-row">
      <code className="ph-token">{placeholder.token}</code>
      <svg viewBox="0 0 16 8" width="24" height="12" fill="none">
        <path
          d="M0 4h14M10 1l4 3-4 3"
          stroke="#aaa"
          strokeWidth="1.2"
          strokeLinecap="round"
        />
      </svg>
      <input
        className="input input-sm ph-input"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder="e.g. data.title"
      />
      <span
        className="type-badge sm"
        style={{
          background: `${TYPE_COLOR[placeholder.type]}18`,
          color: TYPE_COLOR[placeholder.type],
        }}
      >
        {placeholder.type}
      </span>
      <span
        className="status-dot"
        style={{ background: color }}
        title={placeholder.mappingStatus}
      />
    </div>
  );
}
