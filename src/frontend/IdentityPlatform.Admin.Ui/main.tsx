import { createRoot } from "react-dom/client";
import { App } from "./Features/Shell/App";

const root = document.getElementById("app");
if (!root) throw new Error("Workspace root is unavailable");
createRoot(root).render(<App />);
