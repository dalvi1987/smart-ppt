import { useEffect, useState } from "react";
import { aiApi, presentationApi } from "../services/api";
import { useAppStore } from "../store/appStore";
import type { AiProvider } from "../types";

const EXAMPLE_PROMPTS = [
  {
    label: "Investor Deck",
    text: "Create a 12-slide investor deck for a B2B SaaS startup. Include executive summary, $50B market opportunity, product overview with 3 key features, traction metrics (ARR $2M, 40% YoY growth), competitive landscape, team, and $5M Series A ask.",
  },
  {
    label: "Q3 Board Update",
    text: "Build a 10-slide Q3 board update. Cover revenue performance ($2M ARR), product milestones delivered, key customer wins, headcount updates, burn rate, and Q4 priorities.",
  },
  {
    label: "Product Launch",
    text: "Generate a 12-slide product launch deck for a mobile productivity app. Cover the problem, solution overview, 3 key features with screenshots, pricing tiers, go-to-market strategy, and launch timeline.",
  },
  {
    label: "Market Analysis",
    text: "Create a market analysis deck covering the B2B SaaS landscape in 2026. Include TAM/SAM/SOM ($50B/$8B/$500M), key market trends, competitor matrix, and our strategic positioning.",
  },
];

const SLIDE_TYPES = [
  "TitleSlide",
  "BulletSlide",
  "ChartSlide",
  "TableSlide",
  "TwoColumnSlide",
  "SectionBreak",
];

export function AIGeneratorPage() {
  const {
    aiPrompt,
    setAiPrompt,
    aiResult,
    setAiResult,
    aiLoading,
    setAiLoading,
    selectedTemplate,
    setActiveScreen,
    addNotification,
    presentations,
    setPresentations,
  } = useAppStore();

  const [provider, setProvider] = useState<AiProvider>("OpenRouter");
  const [model, setModel] = useState("google/gemma-3n-e4b-it:free");
  const [maxSlides, setMaxSlides] = useState(12);
  const [includeSpeakerNotes, setIncludeSpeakerNotes] = useState(false);
  const [strictSchema, setStrictSchema] = useState(true);
  const [allowedTypes, setAllowedTypes] = useState(
    new Set(["TitleSlide", "BulletSlide", "ChartSlide", "TableSlide"]),
  );
  const [providers, setProviders] = useState<
    Array<{ provider: string; models: string[] }>
  >([]);
  const [generating, setGenerating] = useState(false);

  useEffect(() => {
    aiApi
      .getProviders()
      .then(setProviders)
      .catch(() => {});
  }, []);

  const models = providers.find((p) => p.provider === provider)?.models ?? [
    "google/gemma-3n-e4b-it:free",
  ];

  const toggleType = (t: string) => {
    setAllowedTypes((prev) => {
      const next = new Set(prev);
      next.has(t) ? next.delete(t) : next.add(t);
      return next;
    });
  };

  const handleGenerate = async () => {
    if (!aiPrompt.trim()) {
      addNotification({ type: "error", message: "Please enter a prompt" });
      return;
    }
    setAiLoading(true);
    setAiResult(null);
    try {
      const result = await aiApi.generate({
        prompt: aiPrompt,
        provider,
        model,
        maxSlides,
        includeSpeakerNotes,
        strictSchema,
        allowedSlideTypes: Array.from(allowedTypes),
      });
      setAiResult(result);
      addNotification({
        type: "success",
        message: `Generated ${result.slideCount} slides in ${result.generationSeconds.toFixed(1)}s`,
      });
    } catch (e: any) {
      addNotification({ type: "error", message: e.message });
    } finally {
      setAiLoading(false);
    }
  };

  const handleCreatePresentation = async () => {
    if (!aiResult || !selectedTemplate) {
      addNotification({
        type: "error",
        message: "Select a template first (go to Templates tab)",
      });
      return;
    }
    setGenerating(true);
    try {
      const p = await presentationApi.generateFromAi(
        selectedTemplate.id,
        "AI Generated Deck",
        aiResult.slideJson,
        aiPrompt,
      );
      setPresentations([p, ...presentations]);
      addNotification({
        type: "success",
        message: "Presentation generated! Go to Preview to download.",
      });
      setActiveScreen("preview");
    } catch (e: any) {
      addNotification({ type: "error", message: e.message });
    } finally {
      setGenerating(false);
    }
  };

  const parsedSlides = (() => {
    if (!aiResult) return [];
    try {
      return JSON.parse(aiResult.slideJson).slides ?? [];
    } catch {
      return [];
    }
  })();

  return (
    <div className="ai-layout">
      {/* Left: Prompt + Output */}
      <div className="ai-main">
        <div className="card">
          <div className="card-header">
            <h3>Describe your presentation</h3>
            <p>
              Be specific about audience, data, tone, and number of slides for
              best results
            </p>
          </div>
          <textarea
            className="prompt-textarea"
            rows={7}
            value={aiPrompt}
            onChange={(e) => setAiPrompt(e.target.value)}
            placeholder="e.g. Create a 12-slide investor deck for a B2B SaaS startup. Include executive summary, $50B market opportunity..."
          />
          <div className="example-prompts">
            {EXAMPLE_PROMPTS.map((ex) => (
              <button
                key={ex.label}
                className="chip"
                onClick={() => setAiPrompt(ex.text)}
              >
                {ex.label}
              </button>
            ))}
          </div>
          <div className="prompt-footer">
            <span className="char-count">{aiPrompt.length} / 2000</span>
            <button
              className="btn btn-primary"
              onClick={handleGenerate}
              disabled={aiLoading || !aiPrompt.trim()}
            >
              {aiLoading ? (
                <span className="loading-dots">
                  <span />
                  <span />
                  <span />
                </span>
              ) : (
                "Generate slides"
              )}
            </button>
          </div>
        </div>

        {/* Output */}
        {aiLoading && (
          <div className="card ai-loading-card">
            <div className="ai-loading">
              <span className="loading-dots">
                <span />
                <span />
                <span />
              </span>
              <span>Generating slide structure via {model}...</span>
            </div>
          </div>
        )}

        {aiResult && !aiLoading && (
          <div className="card">
            <div className="card-header-row">
              <div>
                <h3>Generated structure</h3>
                <p>
                  {aiResult.slideCount} slides ·{" "}
                  {aiResult.generationSeconds.toFixed(1)}s ·{" "}
                  {aiResult.modelUsed}
                </p>
              </div>
              <div className="btn-group">
                <button
                  className="btn btn-ghost btn-sm"
                  onClick={() => {
                    navigator.clipboard.writeText(aiResult.slideJson);
                    addNotification({
                      type: "info",
                      message: "Copied to clipboard",
                    });
                  }}
                >
                  Copy JSON
                </button>
                <button
                  className="btn btn-primary btn-sm"
                  onClick={handleCreatePresentation}
                  disabled={generating}
                >
                  {generating ? "Creating..." : "Create PPTX →"}
                </button>
              </div>
            </div>

            {/* Slide summary cards */}
            <div className="slide-summary-grid">
              {parsedSlides.map((s: any, i: number) => (
                <div key={i} className="slide-summary-card">
                  <div className="slide-num">{i + 1}</div>
                  <div className="slide-summary-info">
                    <div className="slide-summary-title">
                      {s.data?.title ?? "Untitled"}
                    </div>
                    <div className="slide-summary-type">{s.type}</div>
                  </div>
                </div>
              ))}
            </div>

            {/* Raw JSON */}
            <details className="json-details">
              <summary>View raw JSON</summary>
              <pre className="json-block">
                {JSON.stringify(JSON.parse(aiResult.slideJson), null, 2)}
              </pre>
            </details>
          </div>
        )}
      </div>

      {/* Right: Settings */}
      <aside className="ai-sidebar">
        <div className="card">
          <h4>AI settings</h4>
          <div className="setting-group">
            <label>Provider</label>
            <select
              className="input input-sm"
              value={provider}
              onChange={(e) => {
                setProvider(e.target.value as AiProvider);
                setModel(
                  providers.find((p) => p.provider === e.target.value)
                    ?.models[0] ?? "",
                );
              }}
            >
              <option value="OpenRouter">OpenRouter</option>
              <option value="AzureOpenAI">Azure OpenAI</option>
            </select>
          </div>
          <div className="setting-group">
            <label>Model</label>
            <select
              className="input input-sm"
              value={model}
              onChange={(e) => setModel(e.target.value)}
            >
              {models.map((m) => (
                <option key={m}>{m}</option>
              ))}
            </select>
          </div>
          <div className="setting-group">
            <label>Max slides</label>
            <div className="slider-row">
              <input
                type="range"
                min={4}
                max={25}
                value={maxSlides}
                onChange={(e) => setMaxSlides(+e.target.value)}
              />
              <span className="slider-val">{maxSlides}</span>
            </div>
          </div>
          <div className="setting-toggle">
            <label>Strict JSON schema</label>
            <button
              className={`toggle ${strictSchema ? "on" : ""}`}
              onClick={() => setStrictSchema(!strictSchema)}
            />
          </div>
          <div className="setting-toggle">
            <label>Speaker notes</label>
            <button
              className={`toggle ${includeSpeakerNotes ? "on" : ""}`}
              onClick={() => setIncludeSpeakerNotes(!includeSpeakerNotes)}
            />
          </div>
        </div>

        <div className="card">
          <h4>Slide types</h4>
          <div className="type-grid">
            {SLIDE_TYPES.map((t) => (
              <button
                key={t}
                className={`type-btn ${allowedTypes.has(t) ? "active" : ""}`}
                onClick={() => toggleType(t)}
              >
                {t.replace("Slide", "").replace("Break", " Break")}
              </button>
            ))}
          </div>
        </div>

        <div className="card">
          <h4>Template</h4>
          {selectedTemplate ? (
            <div className="selected-template-info">
              <div className="template-dot" />
              <div>
                <div className="stname">{selectedTemplate.name}</div>
                <div className="stmeta">
                  {selectedTemplate.placeholderCount} placeholders
                </div>
              </div>
              <button
                className="btn btn-ghost btn-xs"
                onClick={() => setActiveScreen("templates")}
              >
                Change
              </button>
            </div>
          ) : (
            <button
              className="btn btn-ghost btn-sm full-width"
              onClick={() => setActiveScreen("templates")}
            >
              Select a template →
            </button>
          )}
        </div>
      </aside>
    </div>
  );
}
