// Stands in for CentralApi: declines cards starting with 0000, leaves those starting with 2222
// unconfirmed and shows a page for direct payments.
import { createServer } from "node:http";

createServer(async (request, response) => {
  let body = "";
  for await (const chunk of request) body += chunk;

  if (request.url === "/pay") {
    response.writeHead(200, { "Content-Type": "text/html" });
    response.end("<h1>Stub bank</h1>");
    return;
  }

  if (request.url?.endsWith("/status")) {
    response.writeHead(200, { "Content-Type": "application/json" });
    response.end(JSON.stringify({ state: "Pending" }));
    return;
  }

  const { Number: number } = JSON.parse(body);
  response.writeHead(number.startsWith("0000") ? 400 : number.startsWith("2222") ? 202 : 200);
  response.end();
}).listen(5000, "0.0.0.0");
