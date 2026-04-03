// ─── Enums ────────────────────────────────────────────────────────────────────
export type SlideType = 'Title' | 'Bullet' | 'Chart' | 'Table' | 'TwoColumn' | 'SectionBreak' | 'Image' | 'Quote' | 'Timeline';
export type PlaceholderType = 'Text' | 'Chart' | 'Table' | 'Image' | 'Number' | 'Date';
export type MappingStatus = 'AutoMapped' | 'ManualMapped' | 'NeedsReview' | 'Unmapped';
export type PresentationStatus = 'Pending' | 'Generating' | 'Completed' | 'Failed';
export type GenerationSource = 'ManualJson' | 'AIPrompt' | 'ApiData';
export type TemplateCategory = 'Consulting' | 'Sales' | 'Finance' | 'Product' | 'General' | 'Custom';
export type AiProvider = 'OpenRouter' | 'AzureOpenAI';

// ─── Template ─────────────────────────────────────────────────────────────────
export interface Placeholder {
  id: string;
  name: string;
  token: string;
  type: PlaceholderType;
  mappedDataField?: string;
  mappingStatus: MappingStatus;
}

export interface SlideLayout {
  id: string;
  name: string;
  slideType: SlideType;
  sortOrder: number;
  placeholders: Placeholder[];
}

export interface Template {
  id: string;
  name: string;
  description: string;
  thumbnailUrl: string;
  category: TemplateCategory;
  layoutCount: number;
  placeholderCount: number;
  createdAt: string;
  layouts: SlideLayout[];
  // ─── Script generation ────────────────────────────────────────────────────
  generatedScript: string | null;
  scriptGeneratedAt: string | null;
}

// ─── Presentation ─────────────────────────────────────────────────────────────
export interface Presentation {
  id: string;
  title: string;
  templateId: string;
  templateName: string;
  status: PresentationStatus;
  source: GenerationSource;
  slideCount: number;
  generationTimeSeconds: number;
  downloadUrl?: string;
  createdAt: string;
  slideJson?: string;
}

// ─── AI ───────────────────────────────────────────────────────────────────────
export interface AiGenerationResult {
  slideJson: string;
  slideCount: number;
  modelUsed: string;
  tokensUsed: number;
  generationSeconds: number;
}

export interface AiGenerateRequest {
  templateId: string;
  prompt: string;
  provider: AiProvider;
  model: string;
  maxSlides: number;
  includeSpeakerNotes: boolean;
  strictSchema: boolean;
  allowedSlideTypes: string[];
}

// ─── Slide JSON Schema ────────────────────────────────────────────────────────
export interface SlideData {
  title?: string;
  subtitle?: string;
  bullets?: string[];
  speakerNotes?: string;
  chart?: {
    type: 'bar' | 'line' | 'pie' | 'doughnut';
    categories: string[];
    series: Array<{ name: string; values: number[] }>;
  };
  table?: {
    headers: string[];
    rows: string[][];
  };
}

export interface SlideDefinition {
  type: string;
  data: SlideData;
}

export interface SlideJsonPayload {
  slides: SlideDefinition[];
}

// ─── API Error ────────────────────────────────────────────────────────────────
export interface ApiError {
  error: string;
  details?: string;
}
