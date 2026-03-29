import { AppShell } from "./components/Layout/AppShell";
import { AIGeneratorPage } from "./pages/AIGeneratorPage";
import { MappingPage } from "./pages/MappingPage";
import { PreviewPage } from "./pages/PreviewPage";
import { TemplatesPage } from "./pages/TemplatesPage";
import { useAppStore } from "./store/appStore";
import "./styles.css";

export default function App() {
  const { activeScreen } = useAppStore();

  const pages: Record<typeof activeScreen, React.ReactNode> = {
    templates: <TemplatesPage />,
    ai: <AIGeneratorPage />,
    mapping: <MappingPage />,
    preview: <PreviewPage />,
    history: <PreviewPage />,
  };

  return <AppShell>{pages[activeScreen]}</AppShell>;
}
