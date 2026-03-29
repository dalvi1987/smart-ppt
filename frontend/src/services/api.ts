import type {
  AiGenerateRequest,
  AiGenerationResult,
  Placeholder,
  Presentation,
  Template,
} from "../types";

const BASE_URL = import.meta.env.VITE_API_URL ?? "https://localhost:53559/api";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`, {
    headers: { "Content-Type": "application/json", ...init?.headers },
    ...init,
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }));
    throw new Error(err.error ?? "Request failed");
  }
  if (res.status === 204) return undefined as T;
  return res.json();
}

// ─── Templates ────────────────────────────────────────────────────────────────
export const templateApi = {
  getAll: () => request<Template[]>("/templates"),

  getById: (id: string) => request<Template>(`/templates/${id}`),

  upload: (file: File, name: string, description: string, category: string) => {
    const form = new FormData();
    form.append("file", file);
    form.append("name", name);
    form.append("description", description);
    form.append("category", category);
    return fetch(`${BASE_URL}/templates/upload`, {
      method: "POST",
      body: form,
    }).then((r) => {
      if (!r.ok) throw new Error("Upload failed");
      return r.json() as Promise<Template>;
    });
  },

  updateMappings: (
    templateId: string,
    mappings: Array<{
      placeholderId: string;
      mappedDataField: string;
      mappingRule: string;
    }>,
  ) =>
    request<void>(`/templates/${templateId}/mappings`, {
      method: "PUT",
      body: JSON.stringify(mappings),
    }),

  delete: (id: string) =>
    request<void>(`/templates/${id}`, { method: "DELETE" }),

  // ─── Script endpoints ──────────────────────────────────────────────────────

  /** Fetch the raw PptxGenJS .js script for a template (returns plain text) */
  getScript: (id: string): Promise<string> =>
    fetch(`${BASE_URL}/templates/${id}/script`).then((r) => {
      if (!r.ok) throw new Error(`Failed to fetch script (${r.status})`);
      return r.text();
    }),

  /** Re-run AI script generation for an existing template */
  regenerateScript: (id: string) =>
    request<Template>(`/templates/${id}/script/regenerate`, {
      method: "POST",
    }),
};

// ─── Presentations ────────────────────────────────────────────────────────────
export const presentationApi = {
  getAll: () => request<Presentation[]>("/presentations"),

  getById: (id: string) => request<Presentation>(`/presentations/${id}`),

  generate: (templateId: string, title: string, slideJson: string) =>
    request<Presentation>("/presentations/generate", {
      method: "POST",
      body: JSON.stringify({ templateId, title, slideJson }),
    }),

  generateFromAi: (
    templateId: string,
    title: string,
    slideJson: string,
    promptUsed?: string,
  ) =>
    request<Presentation>("/presentations/generate-from-ai", {
      method: "POST",
      body: JSON.stringify({ templateId, title, slideJson, promptUsed }),
    }),

  delete: (id: string) =>
    request<void>(`/presentations/${id}`, { method: "DELETE" }),
};

// ─── AI ───────────────────────────────────────────────────────────────────────
export const aiApi = {
  generate: (req: AiGenerateRequest) =>
    request<AiGenerationResult>("/ai/generate", {
      method: "POST",
      body: JSON.stringify(req),
    }),

  getProviders: () =>
    request<Array<{ provider: string; models: string[] }>>("/ai/providers"),
};
