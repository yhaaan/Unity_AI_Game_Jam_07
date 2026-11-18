import http from "node:http";

const host = "127.0.0.1";
const port = parsePositiveInteger(process.env.OPENAI_PROXY_PORT, 8787);
const model = process.env.OPENAI_MODEL || "gpt-6-luna";
const maxOutputTokens = parsePositiveInteger(
  process.env.OPENAI_MAX_OUTPUT_TOKENS,
  256,
);
const apiKey = process.env.OPENAI_API_KEY;

function parsePositiveInteger(value, fallback) {
  const parsed = Number.parseInt(value ?? "", 10);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}

function sendJson(response, statusCode, payload) {
  response.writeHead(statusCode, {
    "Content-Type": "application/json; charset=utf-8",
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Headers": "Content-Type",
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
    "Cache-Control": "no-store",
  });
  response.end(JSON.stringify(payload));
}

async function readJson(request) {
  const chunks = [];
  let size = 0;

  for await (const chunk of request) {
    size += chunk.length;
    if (size > 64 * 1024) {
      throw new Error("Request body is too large.");
    }
    chunks.push(chunk);
  }

  const raw = Buffer.concat(chunks).toString("utf8");
  return JSON.parse(raw || "{}");
}

function extractOutputText(payload) {
  for (const item of payload.output ?? []) {
    for (const content of item.content ?? []) {
      if (content.type === "output_text" && typeof content.text === "string") {
        return content.text;
      }
    }
  }
  return "";
}

const server = http.createServer(async (request, response) => {
  if (request.method === "OPTIONS") {
    sendJson(response, 204, {});
    return;
  }

  if (request.method === "GET" && request.url === "/health") {
    sendJson(response, 200, {
      ok: true,
      model,
      apiKeyConfigured: Boolean(apiKey),
    });
    return;
  }

  if (request.method !== "POST" || request.url !== "/v1/game-response") {
    sendJson(response, 404, { error: "Not found." });
    return;
  }

  if (!apiKey) {
    sendJson(response, 503, {
      error: "OPENAI_API_KEY is not configured in the proxy process.",
    });
    return;
  }

  try {
    const body = await readJson(request);
    const input = typeof body.input === "string" ? body.input.trim() : "";

    if (!input) {
      sendJson(response, 400, { error: "A non-empty input string is required." });
      return;
    }

    const openAIResponse = await fetch("https://api.openai.com/v1/responses", {
      method: "POST",
      headers: {
        Authorization: `Bearer ${apiKey}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        model,
        instructions:
          process.env.OPENAI_GAME_INSTRUCTIONS ||
          "You are a game runtime component. Return only the concise player-facing text requested.",
        input,
        max_output_tokens: maxOutputTokens,
      }),
    });

    const payload = await openAIResponse.json();
    if (!openAIResponse.ok) {
      sendJson(response, openAIResponse.status, {
        error: payload.error?.message || "OpenAI API request failed.",
      });
      return;
    }

    sendJson(response, 200, {
      text: extractOutputText(payload),
      model: payload.model || model,
      usage: payload.usage || null,
    });
  } catch (error) {
    sendJson(response, 500, {
      error: error instanceof Error ? error.message : "Unexpected proxy error.",
    });
  }
});

server.listen(port, host, () => {
  console.log(`OpenAI local proxy: http://${host}:${port}`);
  console.log(`Model: ${model}, max output tokens: ${maxOutputTokens}`);
  if (!apiKey) {
    console.warn("OPENAI_API_KEY is not set; /health works but API calls will fail.");
  }
});

