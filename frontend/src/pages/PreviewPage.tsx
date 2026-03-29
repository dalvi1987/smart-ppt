import { useEffect, useState } from "react";
import { presentationApi } from "../services/api";
import { useAppStore } from "../store/appStore";

type ExportFormat = "pptx" | "pdf" | "json";

const STATUS_STYLES: Record<string, string> = {
  Completed:  "bg-emerald-50 text-emerald-700 border border-emerald-200",
  Failed:     "bg-red-50 text-red-700 border border-red-200",
  Generating: "bg-amber-50 text-amber-700 border border-amber-200",
  Pending:    "bg-slate-100 text-slate-600 border border-slate-200",
};
const SOURCE_ICON: Record<string, string> = {
  AIPrompt: "🤖", ManualJson: "📝", ApiData: "🔗",
};

export function PreviewPage() {
  const {
    presentations, setPresentations,
    presentationsLoading, setPresentationsLoading,
    removePresentation, addNotification, setActiveScreen,
  } = useAppStore();

  const [activePresId, setActivePresId] = useState<string | null>(null);
  const [exportFormat, setExportFormat] = useState<ExportFormat>("pptx");
  const [activeSlide, setActiveSlide]   = useState(0);
  const [deleting, setDeleting]         = useState<string | null>(null);

  useEffect(() => {
    setPresentationsLoading(true);
    presentationApi.getAll()
      .then((data) => {
        setPresentations(data);
        if (data.length) setActivePresId(data[0].id);
      })
      .catch(() => addNotification({ type: "error", message: "Failed to load presentations" }))
      .finally(() => setPresentationsLoading(false));
  }, []);

  useEffect(() => { setActiveSlide(0); }, [activePresId]);

  const currentPres = presentations.find((p) => p.id === activePresId);

  // slides come directly from slideJson in the API response — works after page reload
  const slides = (() => {
    if (!currentPres?.slideJson) return [];
    try { return JSON.parse(currentPres.slideJson).slides ?? []; }
    catch { return []; }
  })();

  const handleDelete = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (!confirm("Delete this presentation?")) return;
    setDeleting(id);
    try {
      await presentationApi.delete(id);
      removePresentation(id);
      if (activePresId === id) {
        const remaining = presentations.filter((p) => p.id !== id);
        setActivePresId(remaining[0]?.id ?? null);
      }
      addNotification({ type: "success", message: "Presentation deleted" });
    } catch {
      addNotification({ type: "error", message: "Delete failed" });
    } finally {
      setDeleting(null);
    }
  };

  const handleDownload = () => {
    if (exportFormat === "json") {
      if (!currentPres?.slideJson) { addNotification({ type: "error", message: "Slide JSON not available" }); return; }
      const blob = new Blob([currentPres.slideJson], { type: "application/json" });
      const a = document.createElement("a");
      a.href = URL.createObjectURL(blob);
      a.download = `${currentPres?.title ?? "slides"}.json`;
      a.click();
      return;
    }
    if (!currentPres?.downloadUrl) { addNotification({ type: "error", message: "No download URL available" }); return; }
    window.open(currentPres.downloadUrl, "_blank");
  };

  if (presentationsLoading) return (
    <div className="flex items-center justify-center h-64">
      <div className="flex gap-1.5">
        {[0,1,2].map(i => <div key={i} className="w-2.5 h-2.5 rounded-full bg-blue-500 animate-bounce" style={{ animationDelay: `${i*0.15}s` }} />)}
      </div>
    </div>
  );

  if (presentations.length === 0) return (
    <div className="flex flex-col items-center justify-center h-64 gap-4 text-center">
      <div className="w-16 h-16 rounded-2xl bg-blue-50 flex items-center justify-center text-3xl">📊</div>
      <div>
        <p className="font-semibold text-slate-700">No presentations yet</p>
        <p className="text-sm text-slate-400 mt-1">Generate one using the AI Generator</p>
      </div>
      <button className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700 transition-colors" onClick={() => setActiveScreen("ai")}>
        Go to AI Generator →
      </button>
    </div>
  );

  return (
    <div className="flex gap-4 min-h-0">

      {/* ── LEFT SIDEBAR ─────────────────────────────────────────────── */}
      <div className="w-64 flex-shrink-0 flex flex-col gap-3">

        {/* Presentations list */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
          <div className="px-4 py-2.5 border-b border-slate-100 flex items-center justify-between">
            <span className="text-[11px] font-semibold text-slate-400 uppercase tracking-widest">Presentations</span>
            <span className="text-xs bg-slate-100 text-slate-500 px-1.5 py-0.5 rounded-md">{presentations.length}</span>
          </div>
          <div className="overflow-y-auto max-h-80 divide-y divide-slate-50">
            {presentations.map((p) => (
              <div key={p.id} onClick={() => setActivePresId(p.id)}
                className={`group flex items-start gap-2.5 px-3 py-2.5 cursor-pointer transition-all
                  ${activePresId === p.id ? "bg-blue-50 border-l-[3px] border-l-blue-500" : "hover:bg-slate-50 border-l-[3px] border-l-transparent"}`}
              >
                <div className={`w-7 h-7 rounded-lg flex-shrink-0 flex items-center justify-center text-base mt-0.5 ${activePresId === p.id ? "bg-blue-100" : "bg-slate-100"}`}>
                  {SOURCE_ICON[p.source] ?? "📄"}
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-slate-800 truncate leading-snug">{p.title}</p>
                  <p className="text-[11px] text-slate-400 mt-0.5">{p.slideCount} slides · {new Date(p.createdAt).toLocaleDateString()}</p>
                  <span className={`inline-block mt-1.5 text-[10px] px-2 py-0.5 rounded-full font-semibold ${STATUS_STYLES[p.status] ?? STATUS_STYLES.Pending}`}>{p.status}</span>
                </div>
                <button onClick={(e) => handleDelete(p.id, e)} disabled={deleting === p.id}
                  className="opacity-0 group-hover:opacity-100 p-1 rounded-md hover:bg-red-50 hover:text-red-500 text-slate-300 transition-all flex-shrink-0 mt-0.5" title="Delete">
                  {deleting === p.id
                    ? <svg className="w-3.5 h-3.5 animate-spin" fill="none" viewBox="0 0 24 24"><circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"/><path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z"/></svg>
                    : <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" strokeWidth="1.8" viewBox="0 0 16 16"><path d="M2 4h12M6 4V2h4v2M5 4v9a1 1 0 001 1h4a1 1 0 001-1V4" strokeLinecap="round"/></svg>
                  }
                </button>
              </div>
            ))}
          </div>
        </div>

        {/* Slide strip */}
        {slides.length > 0 && (
          <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
            <div className="px-4 py-2.5 border-b border-slate-100 flex items-center justify-between">
              <span className="text-[11px] font-semibold text-slate-400 uppercase tracking-widest">Slides</span>
              <span className="text-xs bg-slate-100 text-slate-500 px-1.5 py-0.5 rounded-md">{slides.length}</span>
            </div>
            <div className="overflow-y-auto max-h-60 p-2 flex flex-col gap-1">
              {slides.map((s: any, i: number) => (
                <div key={i} onClick={() => setActiveSlide(i)}
                  className={`flex items-center gap-2 p-1.5 rounded-lg cursor-pointer transition-colors ${activeSlide === i ? "bg-blue-50 ring-1 ring-blue-200" : "hover:bg-slate-50"}`}>
                  <span className="text-[10px] text-slate-400 w-4 text-right">{i + 1}</span>
                  <div className="w-9 h-6 bg-white border border-slate-200 rounded flex-shrink-0 flex flex-col p-1 gap-0.5">
                    <div className="h-1.5 rounded-sm bg-blue-400" style={{ width: "75%" }} />
                    <div className="h-1 rounded-sm bg-slate-200" />
                    <div className="h-1 rounded-sm bg-slate-200" style={{ width: "65%" }} />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="text-[11px] font-medium text-slate-700 truncate leading-tight">{s.data?.title ?? "Untitled"}</p>
                    <p className="text-[10px] text-slate-400">{s.type?.replace("Slide", "") ?? ""}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* ── MAIN ─────────────────────────────────────────────────────── */}
      <div className="flex-1 flex flex-col gap-3 min-w-0">

        {/* Stats */}
        {currentPres && (
          <div className="grid grid-cols-4 gap-3">
            {[
              { label: "Slides",   value: String(currentPres.slideCount || "—") },
              { label: "Source",   value: `${SOURCE_ICON[currentPres.source] ?? ""} ${currentPres.source}` },
              { label: "Status",   value: currentPres.status, isStatus: true },
              { label: "Gen time", value: currentPres.generationTimeSeconds ? `${currentPres.generationTimeSeconds.toFixed(1)}s` : "—" },
            ].map((item) => (
              <div key={item.label} className="bg-white rounded-xl border border-slate-200 shadow-sm px-4 py-3">
                <p className="text-xs text-slate-400 mb-1.5">{item.label}</p>
                {item.isStatus
                  ? <span className={`text-xs font-semibold px-2.5 py-1 rounded-full ${STATUS_STYLES[item.value] ?? ""}`}>{item.value}</span>
                  : <p className="text-base font-bold text-slate-800 truncate">{item.value}</p>
                }
              </div>
            ))}
          </div>
        )}

        {/* Slide canvas */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden flex-shrink-0" style={{ position: "relative", height: "340px" }}>
          {slides.length > 0 ? (
            <>
              <SlideRenderer slide={slides[activeSlide]} index={activeSlide} />
              {slides.length > 1 && (
                <div className="absolute bottom-3 left-0 right-0 flex items-center justify-center gap-2 z-10">
                  <button onClick={() => setActiveSlide(i => Math.max(0, i-1))} disabled={activeSlide === 0}
                    className="px-3 py-1.5 bg-white/90 backdrop-blur border border-slate-200 rounded-lg text-xs font-medium shadow-sm disabled:opacity-40 hover:bg-white transition-colors">← Prev</button>
                  <span className="px-2.5 py-1.5 bg-white/90 backdrop-blur border border-slate-200 rounded-lg text-xs text-slate-600 shadow-sm font-medium">
                    {activeSlide + 1} / {slides.length}
                  </span>
                  <button onClick={() => setActiveSlide(i => Math.min(slides.length-1, i+1))} disabled={activeSlide === slides.length-1}
                    className="px-3 py-1.5 bg-white/90 backdrop-blur border border-slate-200 rounded-lg text-xs font-medium shadow-sm disabled:opacity-40 hover:bg-white transition-colors">Next →</button>
                </div>
              )}
            </>
          ) : (
            <div className="absolute inset-0 flex flex-col items-center justify-center gap-3 bg-slate-50">
              <div className="w-12 h-12 rounded-xl bg-slate-200 flex items-center justify-center text-2xl">🖼️</div>
              <p className="text-sm text-slate-500 font-medium">
                {!currentPres ? "Select a presentation" : currentPres.status !== "Completed" ? "Generating..." : "No preview available"}
              </p>
            </div>
          )}
        </div>

        {/* Export */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-sm p-4 flex items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <span className="text-sm font-medium text-slate-600">Export as</span>
            <div className="flex gap-1.5">
              {(["pptx","pdf","json"] as ExportFormat[]).map((f) => (
                <button key={f} onClick={() => setExportFormat(f)}
                  className={`px-3 py-1.5 rounded-lg text-xs font-bold border transition-all
                    ${exportFormat === f ? "bg-blue-600 text-white border-blue-600 shadow-sm" : "bg-white text-slate-500 border-slate-200 hover:border-blue-300 hover:text-blue-600"}`}>
                  .{f.toUpperCase()}
                </button>
              ))}
            </div>
          </div>
          <div className="flex items-center gap-2">
            <button onClick={() => setActiveScreen("mapping")} className="px-3 py-2 text-sm text-slate-600 border border-slate-200 rounded-lg hover:bg-slate-50 transition-colors">
              ← Edit mappings
            </button>
            <button onClick={handleDownload} disabled={!currentPres || currentPres.status !== "Completed"}
              className="px-4 py-2 bg-blue-600 text-white text-sm font-semibold rounded-lg hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors flex items-center gap-2 shadow-sm">
              <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                <path d="M12 15V3m0 12l-4-4m4 4l4-4M2 17l.621 2.485A2 2 0 004.561 21h14.878a2 2 0 001.94-1.515L22 17" strokeLinecap="round" strokeLinejoin="round"/>
              </svg>
              Download .{exportFormat}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

function SlideRenderer({ slide, index }: { slide: any; index: number }) {
  const { type, data } = slide;
  const gradients = ["from-blue-600 to-blue-900","from-emerald-600 to-teal-900","from-violet-600 to-purple-900","from-orange-500 to-red-800","from-pink-600 to-rose-900"];
  const accents   = ["#3B82F6","#10B981","#8B5CF6","#F59E0B","#EC4899"];
  const accent    = accents[index % accents.length];

  if (type === "TitleSlide") return (
    <div className={`absolute inset-0 bg-gradient-to-br ${gradients[index % gradients.length]} flex flex-col items-center justify-center p-12 text-center`}>
      <div className="w-10 h-1 rounded-full bg-white/30 mb-6" />
      <h2 className="text-3xl font-bold text-white leading-tight mb-3">{data.title}</h2>
      {data.subtitle && <p className="text-lg text-white/70 font-light">{data.subtitle}</p>}
      <div className="w-6 h-1 rounded-full bg-white/20 mt-6" />
    </div>
  );

  if (type === "ChartSlide") {
    const cats = data.chart?.categories ?? ["A","B","C","D","E"];
    const vals = data.chart?.series?.[0]?.values ?? [40,65,90,75,85];
    const max  = Math.max(...vals, 1);
    return (
      <div className="absolute inset-0 flex flex-col p-8 bg-white">
        <div className="flex items-center gap-3 mb-5">
          <div className="w-1 h-7 rounded-full" style={{ background: accent }} />
          <h3 className="text-xl font-bold text-slate-800">{data.title}</h3>
        </div>
        <div className="flex-1 flex items-end gap-3 pb-6 px-2">
          {cats.map((cat: string, i: number) => (
            <div key={i} className="flex-1 flex flex-col items-center gap-1.5">
              <span className="text-xs font-semibold" style={{ color: accent }}>{vals[i] ?? ""}</span>
              <div className="w-full rounded-t-lg" style={{ height: `${Math.max((vals[i]/max)*100,4)}%`, background: accent, opacity: 0.65 + (i%5)*0.07 }} />
              <span className="text-xs text-slate-500 truncate w-full text-center">{cat}</span>
            </div>
          ))}
        </div>
      </div>
    );
  }

  if (type === "TableSlide" && data.table) return (
    <div className="absolute inset-0 flex flex-col p-8 bg-white">
      <div className="flex items-center gap-3 mb-4">
        <div className="w-1 h-7 rounded-full" style={{ background: accent }} />
        <h3 className="text-xl font-bold text-slate-800">{data.title}</h3>
      </div>
      <div className="flex-1 overflow-hidden rounded-xl border border-slate-200">
        <table className="w-full text-xs">
          <thead><tr style={{ background: accent }}>
            {data.table.headers.map((h: string, i: number) => <th key={i} className="px-3 py-2.5 text-left text-white font-semibold">{h}</th>)}
          </tr></thead>
          <tbody>
            {data.table.rows.slice(0,5).map((r: string[], i: number) => (
              <tr key={i} className={i%2===0 ? "bg-white" : "bg-slate-50"}>
                {r.map((c,j) => <td key={j} className="px-3 py-2 text-slate-700 border-b border-slate-100">{c}</td>)}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );

  return (
    <div className="absolute inset-0 flex flex-col p-8 bg-white">
      <div className="flex items-center gap-3 mb-5">
        <div className="w-1 h-7 rounded-full" style={{ background: accent }} />
        <h3 className="text-xl font-bold text-slate-800">{data.title}</h3>
      </div>
      <div className="flex flex-col gap-3">
        {data.bullets?.map((b: string, i: number) => (
          <div key={i} className="flex items-start gap-3">
            <div className="w-5 h-5 rounded-full flex-shrink-0 flex items-center justify-center mt-0.5" style={{ background: `${accent}20` }}>
              <div className="w-1.5 h-1.5 rounded-full" style={{ background: accent }} />
            </div>
            <span className="text-sm text-slate-700 leading-relaxed">{b}</span>
          </div>
        ))}
        {data.subtitle && <p className="text-sm text-slate-400 mt-2 italic">{data.subtitle}</p>}
      </div>
    </div>
  );
}
