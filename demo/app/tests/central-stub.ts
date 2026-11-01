// Stands in for CentralApi: declines cards starting with 0000 and shows a page for direct payments.
import { createServer } from "node:http";

createServer(async (request, response) => {
  let body = "";
  for await (const chunk of request) body += chunk;

  if (request.url === "/pay") {
    response.writeHead(200, { "Content-Type": "text/html" });
    response.end("<h1>Stub bank</h1>");
    return;
  }

  const declined = JSON.parse(body).Number.startsWith("0000");
  response.writeHead(declined ? 400 : 200);
  response.end();
}).listen(5000, "0.0.0.0");
