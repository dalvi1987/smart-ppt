import { create } from "zustand";
import type { AiGenerationResult, Presentation, Template } from "../types";

interface AppState {
  // Templates
  templates: Template[];
  selectedTemplate: Template | null;
  templatesLoading: boolean;
  setTemplates: (t: Template[]) => void;
  selectTemplate: (t: Template | null) => void;
  setTemplatesLoading: (v: boolean) => void;
  addTemplate: (t: Template) => void;
  removeTemplate: (id: string) => void;

  // AI
  aiResult: AiGenerationResult | null;
  aiLoading: boolean;
  aiPrompt: string;
  setAiResult: (r: AiGenerationResult | null) => void;
  setAiLoading: (v: boolean) => void;
  setAiPrompt: (p: string) => void;

  // Presentations
  presentations: Presentation[];
  currentPresentation: Presentation | null;
  presentationsLoading: boolean;
  setPresentations: (p: Presentation[]) => void;
  setCurrentPresentation: (p: Presentation | null) => void;
  setPresentationsLoading: (v: boolean) => void;
  removePresentation: (id: string) => void;

  // UI
  activeScreen: "templates" | "ai" | "mapping" | "preview" | "history";
  setActiveScreen: (s: AppState["activeScreen"]) => void;
  sidebarOpen: boolean;
  toggleSidebar: () => void;

  // Notifications
  notifications: Notification[];
  addNotification: (n: Omit<Notification, "id">) => void;
  removeNotification: (id: string) => void;
}

interface Notification {
  id: string;
  type: "success" | "error" | "info";
  message: string;
}

export const useAppStore = create<AppState>((set, get) => ({
  templates: [],
  selectedTemplate: null,
  templatesLoading: false,
  setTemplates: (templates) => set({ templates }),
  selectTemplate: (selectedTemplate) => set({ selectedTemplate }),
  setTemplatesLoading: (templatesLoading) => set({ templatesLoading }),
  addTemplate: (t) => set((s) => ({ templates: [t, ...s.templates] })),
  removeTemplate: (id) =>
    set((s) => ({ templates: s.templates.filter((t) => t.id !== id) })),

  aiResult: null,
  aiLoading: false,
  aiPrompt: "",
  setAiResult: (aiResult) => set({ aiResult }),
  setAiLoading: (aiLoading) => set({ aiLoading }),
  setAiPrompt: (aiPrompt) => set({ aiPrompt }),

  presentations: [],
  currentPresentation: null,
  presentationsLoading: false,
  setPresentations: (presentations) => set({ presentations }),
  setCurrentPresentation: (currentPresentation) => set({ currentPresentation }),
  setPresentationsLoading: (presentationsLoading) => set({ presentationsLoading }),
  removePresentation: (id) =>
    set((s) => ({ presentations: s.presentations.filter((p) => p.id !== id) })),

  activeScreen: "templates",
  setActiveScreen: (activeScreen) => set({ activeScreen }),
  sidebarOpen: true,
  toggleSidebar: () => set((s) => ({ sidebarOpen: !s.sidebarOpen })),

  notifications: [],
  addNotification: (n) => {
    const id = crypto.randomUUID();
    set((s) => ({ notifications: [...s.notifications, { ...n, id }] }));
    setTimeout(() => get().removeNotification(id), 4000);
  },
  removeNotification: (id) =>
    set((s) => ({ notifications: s.notifications.filter((n) => n.id !== id) })),
}));
