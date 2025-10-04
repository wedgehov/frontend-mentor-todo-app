// vite.config.ts
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import fable from "vite-plugin-fable";

export default defineConfig({
  plugins: [
    // Fable must be configured to run before the React plugin
    fable({
      fsproj: "src/src.fsproj",
      failOnFirstError: true,
    }),
    react(),
  ],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5199',
        changeOrigin: true,
        secure: false,
      }
    }
  }
});
