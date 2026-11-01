import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "tests",
  workers: 1,
  reporter: "list",
  use: {
    // The browser sees localhost, as it does for real users; inside Docker that is the ui service.
    baseURL: "http://localhost:8082",
    launchOptions: { args: ["--host-resolver-rules=MAP localhost:8082 ui:5000"] },
  },
});
