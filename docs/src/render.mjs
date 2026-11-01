// Renders the sources next to this file into docs/. Run with the Playwright image, see docs/README.md.
import { chromium } from "/app/node_modules/playwright/index.mjs";

const browser = await chromium.launch();

const infra = await browser.newPage({ viewport: { width: 2000, height: 1022 }, deviceScaleFactor: 1.8 });
await infra.goto("file:///docs/src/infra.html", { waitUntil: "networkidle" });
await infra.screenshot({ path: "/docs/infra.png" });

for (const name of ["tech", "slide"]) {
  const page = await browser.newPage();
  await page.goto(`file:///docs/src/${name}.html`, { waitUntil: "networkidle" });
  await page.pdf({ path: `/docs/${name}.pdf`, format: "A4", landscape: true, printBackground: true, preferCSSPageSize: true });
}

await browser.close();
