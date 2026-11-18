import { spawn } from "node:child_process";
import { createHash, randomUUID } from "node:crypto";
import { existsSync } from "node:fs";
import { lstat, mkdir, readFile, readdir, rm, stat, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import process from "node:process";
import readline from "node:readline/promises";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(scriptDir, "..", "..");
const workspaceRoot = path.resolve(projectRoot, "..");
const envPath = path.join(workspaceRoot, ".env.local");
const sessionId = `${Date.now()}-${randomUUID().slice(0, 8)}`;
const backupRoot = path.join(tmpdir(), "openai-api-dev-cli", sessionId);

const MODEL_ALIASES = Object.freeze({
  astra: "gpt-6.2-astra",
  sol: "gpt-6.1-sol",
  luna: "gpt-6-luna",
});

const SKIPPED_DIRECTORIES = new Set([
  ".git",
  ".idea",
  ".vs",
  ".vscode",
  "Build",
  "Builds",
  "Library",
  "Logs",
  "MemoryCaptures",
  "node_modules",
  "obj",
  "Recordings",
  "Temp",
  "UserSettings",
]);

const READABLE_EXTENSIONS = new Set([
  ".asmdef", ".asmref", ".asset", ".cginc", ".compute", ".cs", ".glslinc",
  ".hlsl", ".json", ".md", ".meta", ".prefab", ".shader", ".txt", ".unity",
  ".uss", ".uxml", ".xml", ".yaml", ".yml",
]);

const WRITABLE_EXTENSIONS = new Set([
  ".asmdef", ".asmref", ".cginc", ".compute", ".cs", ".glslinc", ".hlsl",
  ".json", ".md", ".shader", ".txt", ".uss", ".uxml", ".xml", ".yaml", ".yml",
]);

const MAX_READ_BYTES = 512 * 1024;
const MAX_TOOL_OUTPUT = 48_000;
const MAX_TOOL_ROUNDS = 32;

let model = process.env.OPENAI_DEV_MODEL || MODEL_ALIASES.astra;
let reasoningEffort = process.env.OPENAI_DEV_REASONING || "medium";
let previousResponseId = null;
let totalInputTokens = 0;
let totalOutputTokens = 0;
let activeResponseId = null;
const readPaths = new Set();
const backups = new Map();
const changedPaths = new Set();
const lastWrittenHashes = new Map();

function normalizeRelative(value = ".") {
  return value.replaceAll("\\", "/").replace(/^\.\//, "");
}

function isWithin(parent, child) {
  const relative = path.relative(parent, child);
  return relative === "" || (!relative.startsWith("..") && !path.isAbsolute(relative));
}

function isSecretPath(absolutePath) {
  const relative = normalizeRelative(path.relative(workspaceRoot, absolutePath)).toLowerCase();
  return relative.split("/").some((segment) =>
    segment === "auth.json" || segment === ".npmrc" || segment.startsWith(".env"));
}

function resolveProjectPath(relativePath = ".") {
  const normalized = normalizeRelative(relativePath || ".");
  const absolute = path.resolve(projectRoot, normalized);
  if (!isWithin(projectRoot, absolute)) {
    throw new Error("프로젝트 밖의 경로에는 접근할 수 없습니다.");
  }
  if (isSecretPath(absolute)) {
    throw new Error("비밀정보 파일에는 접근할 수 없습니다.");
  }
  return absolute;
}

function toProjectRelative(absolutePath) {
  return normalizeRelative(path.relative(projectRoot, absolutePath));
}

function isSkippedDirectory(name) {
  const lower = name.toLowerCase();
  return [...SKIPPED_DIRECTORIES].some((entry) => entry.toLowerCase() === lower);
}

function isReadableFile(absolutePath) {
  return READABLE_EXTENSIONS.has(path.extname(absolutePath).toLowerCase());
}

function assertWritablePath(absolutePath) {
  const relative = toProjectRelative(absolutePath);
  const lower = relative.toLowerCase();
  if (!(relative === "Assets" || relative.startsWith("Assets/"))) {
    throw new Error("자동 수정은 Unity Assets 폴더 안에서만 허용됩니다.");
  }
  if (lower.startsWith("assets/plugins/") || lower.includes("/thirdparty/")) {
    throw new Error("외부 플러그인과 ThirdParty 파일은 자동 수정하지 않습니다.");
  }
  if (!WRITABLE_EXTENSIONS.has(path.extname(absolutePath).toLowerCase())) {
    throw new Error("이 파일 형식은 안전한 자동 수정 대상이 아닙니다.");
  }
}

async function assertNoSymlinkPath(absolutePath) {
  const relative = path.relative(projectRoot, absolutePath);
  const segments = relative.split(path.sep).filter(Boolean);
  let current = projectRoot;
  for (const segment of segments) {
    current = path.join(current, segment);
    if (!existsSync(current)) break;
    const info = await lstat(current);
    if (info.isSymbolicLink()) {
      throw new Error("심볼릭 링크를 통한 파일 접근은 허용되지 않습니다.");
    }
  }
}

function hashContent(content) {
  return createHash("sha256").update(content).digest("hex");
}

async function loadApiKey() {
  if (process.env.OPENAI_API_KEY?.trim()) return process.env.OPENAI_API_KEY.trim();
  if (!existsSync(envPath)) {
    throw new Error(`OPENAI_API_KEY를 찾지 못했습니다: ${envPath}`);
  }
  const text = await readFile(envPath, "utf8");
  for (const rawLine of text.split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!line || line.startsWith("#")) continue;
    const match = line.match(/^OPENAI_API_KEY\s*=\s*(.+)$/);
    if (!match) continue;
    let value = match[1].trim();
    if ((value.startsWith('"') && value.endsWith('"')) ||
        (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }
    if (value) return value;
  }
  throw new Error(`사용 가능한 OPENAI_API_KEY가 없습니다: ${envPath}`);
}

async function walkFiles(startAbsolute, limit = 3000) {
  const files = [];
  const stack = [startAbsolute];
  while (stack.length && files.length < limit) {
    const current = stack.pop();
    let entries;
    try {
      entries = await readdir(current, { withFileTypes: true });
    } catch {
      continue;
    }
    entries.sort((a, b) => b.name.localeCompare(a.name));
    for (const entry of entries) {
      if (entry.isSymbolicLink()) continue;
      const absolute = path.join(current, entry.name);
      if (entry.isDirectory()) {
        if (!isSkippedDirectory(entry.name)) stack.push(absolute);
      } else if (entry.isFile() && !isSecretPath(absolute)) {
        files.push(absolute);
        if (files.length >= limit) break;
      }
    }
  }
  return files;
}

async function listFiles(args) {
  const start = resolveProjectPath(args.path || ".");
  const maxResults = Math.max(1, Math.min(args.max_results ?? 200, 1000));
  const extension = args.extension?.trim().toLowerCase() || null;
  const files = await walkFiles(start, 5000);
  const filtered = files
    .filter((file) => !extension || path.extname(file).toLowerCase() ===
      (extension.startsWith(".") ? extension : `.${extension}`))
    .map(toProjectRelative)
    .sort()
    .slice(0, maxResults);
  return { files: filtered, count: filtered.length, truncated: filtered.length >= maxResults };
}

async function readTextFile(args) {
  const absolute = resolveProjectPath(args.path);
  await assertNoSymlinkPath(absolute);
  const info = await stat(absolute);
  if (!info.isFile()) throw new Error("파일이 아닙니다.");
  if (!isReadableFile(absolute)) throw new Error("텍스트로 읽도록 허용된 파일 형식이 아닙니다.");
  if (info.size > MAX_READ_BYTES) throw new Error("파일이 너무 큽니다. 더 좁은 파일을 선택하세요.");
  const text = await readFile(absolute, "utf8");
  const lines = text.split(/\r?\n/);
  const start = Math.max(1, args.start_line ?? 1);
  const requestedEnd = args.end_line ?? Math.min(lines.length, start + 399);
  const end = Math.min(lines.length, Math.max(start, Math.min(requestedEnd, start + 399)));
  readPaths.add(absolute.toLowerCase());
  return {
    path: toProjectRelative(absolute),
    start_line: start,
    end_line: end,
    total_lines: lines.length,
    content: lines.slice(start - 1, end).map((line, index) => `${start + index}: ${line}`).join("\n"),
  };
}

async function searchText(args) {
  const query = args.query;
  if (!query?.trim()) throw new Error("검색어가 비어 있습니다.");
  const start = resolveProjectPath(args.path || "Assets");
  const maxResults = Math.max(1, Math.min(args.max_results ?? 100, 300));
  const files = await walkFiles(start, 5000);
  const needle = args.case_sensitive ? query : query.toLowerCase();
  const matches = [];
  for (const file of files) {
    if (!isReadableFile(file)) continue;
    let info;
    try {
      info = await stat(file);
    } catch {
      continue;
    }
    if (info.size > MAX_READ_BYTES) continue;
    const text = await readFile(file, "utf8");
    const lines = text.split(/\r?\n/);
    for (let index = 0; index < lines.length; index += 1) {
      const haystack = args.case_sensitive ? lines[index] : lines[index].toLowerCase();
      if (haystack.includes(needle)) {
        matches.push({ path: toProjectRelative(file), line: index + 1, text: lines[index].slice(0, 500) });
        if (matches.length >= maxResults) {
          return { matches, count: matches.length, truncated: true };
        }
      }
    }
  }
  return { matches, count: matches.length, truncated: false };
}

async function backupBeforeWrite(absolute) {
  const key = absolute.toLowerCase();
  if (backups.has(key)) return;
  await mkdir(backupRoot, { recursive: true });
  if (existsSync(absolute)) {
    const content = await readFile(absolute);
    const digest = createHash("sha256").update(absolute).digest("hex");
    const backupPath = path.join(backupRoot, digest);
    await writeFile(backupPath, content);
    backups.set(key, { absolute, existed: true, backupPath });
  } else {
    backups.set(key, { absolute, existed: false, backupPath: null });
  }
}

async function replaceText(args) {
  const absolute = resolveProjectPath(args.path);
  await assertNoSymlinkPath(absolute);
  assertWritablePath(absolute);
  if (!existsSync(absolute)) throw new Error("기존 파일이 없습니다. 새 파일은 create_file을 사용하세요.");
  if (!readPaths.has(absolute.toLowerCase())) {
    throw new Error("기존 파일은 같은 세션에서 먼저 read_file로 읽어야 수정할 수 있습니다.");
  }
  if (!args.old_text) throw new Error("old_text는 비어 있을 수 없습니다.");
  const current = await readFile(absolute, "utf8");
  const count = current.split(args.old_text).length - 1;
  if (count === 0) throw new Error("old_text와 정확히 일치하는 내용이 없습니다. 파일을 다시 읽으세요.");
  if (!args.replace_all && count !== 1) {
    throw new Error(`old_text가 ${count}번 나타납니다. 범위를 더 구체적으로 지정하세요.`);
  }
  await backupBeforeWrite(absolute);
  const updated = args.replace_all
    ? current.split(args.old_text).join(args.new_text)
    : current.replace(args.old_text, args.new_text);
  await writeFile(absolute, updated, "utf8");
  lastWrittenHashes.set(absolute.toLowerCase(), hashContent(updated));
  changedPaths.add(absolute);
  readPaths.add(absolute.toLowerCase());
  return { path: toProjectRelative(absolute), replacements: args.replace_all ? count : 1 };
}

async function createFile(args) {
  const absolute = resolveProjectPath(args.path);
  await assertNoSymlinkPath(absolute);
  assertWritablePath(absolute);
  if (existsSync(absolute)) throw new Error("파일이 이미 존재합니다. read_file 후 replace_text를 사용하세요.");
  const parent = path.dirname(absolute);
  if (!existsSync(parent)) {
    throw new Error("상위 폴더가 없습니다. 폴더 규칙 보호를 위해 자동으로 새 폴더를 만들지 않습니다.");
  }
  await backupBeforeWrite(absolute);
  await writeFile(absolute, args.content, { encoding: "utf8", flag: "wx" });
  lastWrittenHashes.set(absolute.toLowerCase(), hashContent(args.content));
  changedPaths.add(absolute);
  readPaths.add(absolute.toLowerCase());
  return { path: toProjectRelative(absolute), created: true };
}

function runProcess(command, args, options = {}) {
  const timeoutMs = options.timeoutMs ?? 120_000;
  const cwd = options.cwd ?? projectRoot;
  return new Promise((resolve) => {
    const child = spawn(command, args, { cwd, shell: false, windowsHide: true });
    let stdout = "";
    let stderr = "";
    let timedOut = false;
    const append = (current, chunk) => (current + chunk.toString()).slice(-MAX_TOOL_OUTPUT);
    child.stdout.on("data", (chunk) => { stdout = append(stdout, chunk); });
    child.stderr.on("data", (chunk) => { stderr = append(stderr, chunk); });
    const timer = setTimeout(() => {
      timedOut = true;
      child.kill();
    }, timeoutMs);
    child.on("error", (error) => {
      clearTimeout(timer);
      resolve({ exit_code: null, timed_out: timedOut, stdout, stderr: `${stderr}\n${error.message}`.trim() });
    });
    child.on("close", (code) => {
      clearTimeout(timer);
      resolve({ exit_code: code, timed_out: timedOut, stdout: stdout.trim(), stderr: stderr.trim() });
    });
  });
}

async function gitStatusAndDiff() {
  const statusResult = await runProcess("git", ["status", "--short", "--", "Project RTD"], { cwd: workspaceRoot });
  const diffResult = await runProcess("git", ["diff", "--", "Project RTD"], { cwd: workspaceRoot });
  return {
    status: statusResult.stdout,
    diff: diffResult.stdout.slice(-40_000),
    changed_by_this_session: [...changedPaths].map(toProjectRelative).sort(),
  };
}

async function unityStatus() {
  return runProcess("unity", ["status", "--project-path", projectRoot, "--format", "json"], { timeoutMs: 30_000 });
}

async function unityCompile() {
  const before = await runProcess("unity", [
    "command", "console", "status", "--project-path", projectRoot, "--format", "json",
    "--caller", "plugin", "--skill", "unity-cli",
  ], { timeoutMs: 30_000 });
  let cursor = null;
  let session = null;
  try {
    const parsed = JSON.parse(before.stdout);
    cursor = parsed.cursor ?? parsed.data?.result?.cursor ?? null;
    session = parsed.session ?? parsed.data?.result?.session ?? null;
  } catch {
    // The recompile result remains authoritative when the console cursor is unavailable.
  }
  const compile = await runProcess("unity", [
    "recompile", "--project-path", projectRoot, "--format", "json", "--timeout", "120",
  ], { timeoutMs: 150_000 });
  let consoleErrors = null;
  if (cursor !== null && session) {
    consoleErrors = await runProcess("unity", [
      "command", "console", "get", "--since", String(cursor), "--since-session", session,
      "--level", "error", "--project-path", projectRoot, "--format", "json",
      "--caller", "plugin", "--skill", "unity-cli",
    ], { timeoutMs: 30_000 });
  }
  return { before_console: before, compile, console_errors_since_start: consoleErrors };
}

async function unityEditorTests(args) {
  const commandArgs = [
    "command", "test", "run", "--mode", "editor",
    "--timeout", String(Math.max(30, Math.min(args.timeout_seconds ?? 600, 900))),
  ];
  if (args.filter?.trim()) commandArgs.push("--filter", args.filter.trim());
  commandArgs.push(
    "--project-path", projectRoot, "--format", "json",
    "--caller", "plugin", "--skill", "unity-cli",
  );
  return runProcess("unity", commandArgs, { timeoutMs: 930_000 });
}

async function undoSessionChanges() {
  const entries = [...backups.values()].reverse();
  const restored = [];
  const skipped = [];
  for (const entry of entries) {
    const key = entry.absolute.toLowerCase();
    if (existsSync(entry.absolute) && lastWrittenHashes.has(key)) {
      const currentHash = hashContent(await readFile(entry.absolute));
      if (currentHash !== lastWrittenHashes.get(key)) {
        skipped.push(toProjectRelative(entry.absolute));
        continue;
      }
    }
    if (entry.existed) {
      const content = await readFile(entry.backupPath);
      await writeFile(entry.absolute, content);
    } else if (existsSync(entry.absolute)) {
      await rm(entry.absolute, { force: true });
    }
    restored.push(toProjectRelative(entry.absolute));
  }
  for (const relative of restored) {
    const absolute = resolveProjectPath(relative);
    backups.delete(absolute.toLowerCase());
    changedPaths.delete(absolute);
    lastWrittenHashes.delete(absolute.toLowerCase());
  }
  readPaths.clear();
  return { restored, skipped };
}

const tools = [
  {
    type: "function",
    name: "list_files",
    description: "Unity 프로젝트 안의 파일 경로를 나열한다. Library, Temp 등 생성 폴더와 비밀정보 파일은 제외한다.",
    strict: true,
    parameters: {
      type: "object",
      properties: {
        path: { type: ["string", "null"], description: "프로젝트 기준 폴더. null이면 프로젝트 루트." },
        extension: { type: ["string", "null"], description: "선택 확장자 필터. 예: .cs" },
        max_results: { type: ["integer", "null"], description: "최대 결과 수." },
      },
      required: ["path", "extension", "max_results"],
      additionalProperties: false,
    },
  },
  {
    type: "function",
    name: "read_file",
    description: "프로젝트의 텍스트 파일을 줄 번호와 함께 읽는다. 한 번에 최대 400줄이다.",
    strict: true,
    parameters: {
      type: "object",
      properties: {
        path: { type: "string" },
        start_line: { type: ["integer", "null"] },
        end_line: { type: ["integer", "null"] },
      },
      required: ["path", "start_line", "end_line"],
      additionalProperties: false,
    },
  },
  {
    type: "function",
    name: "search_text",
    description: "프로젝트 텍스트 파일에서 문자열을 검색한다.",
    strict: true,
    parameters: {
      type: "object",
      properties: {
        query: { type: "string" },
        path: { type: ["string", "null"] },
        case_sensitive: { type: "boolean" },
        max_results: { type: ["integer", "null"] },
      },
      required: ["query", "path", "case_sensitive", "max_results"],
      additionalProperties: false,
    },
  },
  {
    type: "function",
    name: "replace_text",
    description: "이미 읽은 기존 파일에서 정확히 일치하는 텍스트를 교체한다. Assets 안의 코드/UI/셰이더 파일만 가능하다.",
    strict: true,
    parameters: {
      type: "object",
      properties: {
        path: { type: "string" },
        old_text: { type: "string" },
        new_text: { type: "string" },
        replace_all: { type: "boolean" },
      },
      required: ["path", "old_text", "new_text", "replace_all"],
      additionalProperties: false,
    },
  },
  {
    type: "function",
    name: "create_file",
    description: "기존 폴더 안에 새 코드/UI/셰이더 파일을 만든다. 폴더와 메타 파일은 만들지 않는다.",
    strict: true,
    parameters: {
      type: "object",
      properties: {
        path: { type: "string" },
        content: { type: "string" },
      },
      required: ["path", "content"],
      additionalProperties: false,
    },
  },
  {
    type: "function",
    name: "git_diff",
    description: "현재 Git 상태와 추적된 파일의 diff를 읽는다. 아무 것도 수정하지 않는다.",
    strict: true,
    parameters: { type: "object", properties: {}, required: [], additionalProperties: false },
  },
  {
    type: "function",
    name: "unity_status",
    description: "이 프로젝트의 실행 중인 Unity Editor와 Pipeline 연결 상태를 확인한다.",
    strict: true,
    parameters: { type: "object", properties: {}, required: [], additionalProperties: false },
  },
  {
    type: "function",
    name: "unity_compile",
    description: "실행 중인 Unity Editor에서 스크립트를 재컴파일하고 새 Console 오류를 확인한다.",
    strict: true,
    parameters: { type: "object", properties: {}, required: [], additionalProperties: false },
  },
  {
    type: "function",
    name: "unity_editmode_tests",
    description: "실행 중인 Unity Editor에서 EditMode 테스트를 실행한다. 필요하면 이름 필터를 사용한다.",
    strict: true,
    parameters: {
      type: "object",
      properties: {
        filter: { type: ["string", "null"] },
        timeout_seconds: { type: ["integer", "null"] },
      },
      required: ["filter", "timeout_seconds"],
      additionalProperties: false,
    },
  },
];

const instructions = `당신은 Unity 프로젝트를 직접 다루는 수석 개발 에이전트다.
사용자에게는 한국어로 답한다. 프로젝트 루트는 ${projectRoot} 이다.

규칙:
- 추측하지 말고 list_files, search_text, read_file로 필요한 최소 문맥을 먼저 확인한다.
- 사용자의 기존 변경을 보존하고 요청과 무관한 파일은 수정하지 않는다.
- 기존 파일을 수정하기 전에 반드시 read_file로 해당 부분을 읽는다.
- 씬, 프리팹, .meta, 바이너리 에셋은 수정하지 않는다. 가능한 코드 변경만 수행한다.
- 새 파일은 기존 폴더에만 만든다. 폴더를 만들거나 옮기지 않는다.
- MonoBehaviour 하나당 파일 하나를 사용하고 파일명과 클래스명을 일치시킨다.
- 변경 후 git_diff를 확인하고, C# 변경은 unity_compile로 검증한다.
- 관련 테스트가 있으면 가장 좁은 EditMode 테스트부터 실행한다.
- 도구 결과가 실패하면 성공했다고 말하지 말고 원인과 필요한 다음 행동을 명확히 말한다.
- 비밀 키나 .env 파일을 읽거나 요구하지 않는다.
- 삭제, 커밋, 푸시, 패키지 설치, 빌드, Editor 종료는 할 수 없으며 시도하지 않는다.
- 최종 답변에는 변경 파일, 검증 결과, 남은 문제를 간결하게 정리한다.`;

function safeToolSummary(name, args) {
  const safe = {};
  for (const key of ["path", "query", "filter", "mode"]) {
    if (args[key] !== undefined && args[key] !== null) safe[key] = args[key];
  }
  return Object.keys(safe).length ? `${name} ${JSON.stringify(safe)}` : name;
}

async function executeTool(call) {
  let args;
  try {
    args = JSON.parse(call.arguments || "{}");
  } catch {
    return JSON.stringify({ ok: false, error: "도구 인자를 JSON으로 해석할 수 없습니다." });
  }
  console.log(`  [tool] ${safeToolSummary(call.name, args)}`);
  try {
    let result;
    switch (call.name) {
      case "list_files": result = await listFiles(args); break;
      case "read_file": result = await readTextFile(args); break;
      case "search_text": result = await searchText(args); break;
      case "replace_text": result = await replaceText(args); break;
      case "create_file": result = await createFile(args); break;
      case "git_diff": result = await gitStatusAndDiff(); break;
      case "unity_status": result = await unityStatus(); break;
      case "unity_compile": result = await unityCompile(); break;
      case "unity_editmode_tests": result = await unityEditorTests(args); break;
      default: throw new Error(`알 수 없는 도구: ${call.name}`);
    }
    const serialized = JSON.stringify({ ok: true, result });
    return serialized.length > MAX_TOOL_OUTPUT
      ? `${serialized.slice(0, MAX_TOOL_OUTPUT)}\n...도구 출력이 잘렸습니다...`
      : serialized;
  } catch (error) {
    return JSON.stringify({ ok: false, error: error.message });
  }
}

async function createResponse(apiKey, input, continuationId = null) {
  const body = {
    model,
    instructions,
    input,
    tools,
    tool_choice: "auto",
    reasoning: { effort: reasoningEffort },
    max_output_tokens: 12_000,
  };
  if (continuationId) body.previous_response_id = continuationId;

  let lastError;
  for (let attempt = 0; attempt < 3; attempt += 1) {
    try {
      const response = await fetch("https://api.openai.com/v1/responses", {
        method: "POST",
        headers: {
          Authorization: `Bearer ${apiKey}`,
          "Content-Type": "application/json",
          "X-Client-Request-Id": randomUUID(),
        },
        body: JSON.stringify(body),
        signal: AbortSignal.timeout(10 * 60 * 1000),
      });
      const requestId = response.headers.get("x-request-id");
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        const message = payload?.error?.message || `HTTP ${response.status}`;
        const error = new Error(`${message}${requestId ? ` (request: ${requestId})` : ""}`);
        error.status = response.status;
        throw error;
      }
      return payload;
    } catch (error) {
      lastError = error;
      if (error.status === 429 || (error.status && error.status < 500) || attempt === 2) throw error;
      await new Promise((resolve) => setTimeout(resolve, 750 * (attempt + 1)));
    }
  }
  throw lastError;
}

function recordUsage(response) {
  totalInputTokens += response.usage?.input_tokens || 0;
  totalOutputTokens += response.usage?.output_tokens || 0;
}

function extractText(response) {
  const chunks = [];
  for (const item of response.output || []) {
    if (item.type !== "message") continue;
    for (const content of item.content || []) {
      if (content.type === "output_text" && content.text) chunks.push(content.text);
    }
  }
  return chunks.join("\n").trim();
}

async function runAgentTurn(apiKey, userInput) {
  let response = await createResponse(apiKey, userInput, previousResponseId);
  for (let round = 0; round < MAX_TOOL_ROUNDS; round += 1) {
    recordUsage(response);
    activeResponseId = response.id;
    const calls = (response.output || []).filter((item) => item.type === "function_call");
    if (calls.length === 0) {
      previousResponseId = response.id;
      const text = extractText(response);
      console.log(`\n${text || "응답 텍스트가 없습니다."}`);
      console.log(`\n[tokens] 이번 세션 누적 input ${totalInputTokens.toLocaleString()} / output ${totalOutputTokens.toLocaleString()}`);
      return;
    }
    const outputs = [];
    for (const call of calls) {
      outputs.push({
        type: "function_call_output",
        call_id: call.call_id,
        output: await executeTool(call),
      });
    }
    response = await createResponse(apiKey, outputs, response.id);
  }
  throw new Error(`도구 호출이 ${MAX_TOOL_ROUNDS}회를 넘었습니다. 작업을 더 작게 나눠주세요.`);
}

function printHelp() {
  console.log(`
명령:
  /help                 도움말
  /new                  대화 문맥 초기화
  /model                현재 모델 확인
  /model astra|sol|luna 모델 변경 후 문맥 초기화
  /effort low|medium|high 추론 강도 변경 후 문맥 초기화
  /status               모델, 토큰, 이번 세션 변경 파일 확인
  /undo                 이번 CLI 세션이 변경한 파일 전부 복원
  /exit                 종료

일반 문장은 API 개발 에이전트에게 전달됩니다.`);
}

async function handleSlashCommand(input) {
  const [command, value] = input.trim().split(/\s+/, 2);
  switch (command.toLowerCase()) {
    case "/help":
      printHelp();
      return true;
    case "/new":
      previousResponseId = null;
      activeResponseId = null;
      console.log("대화 문맥을 초기화했습니다.");
      return true;
    case "/model":
      if (!value) {
        console.log(`현재 모델: ${model}`);
      } else {
        const selected = MODEL_ALIASES[value.toLowerCase()] || value;
        model = selected;
        previousResponseId = null;
        activeResponseId = null;
        console.log(`모델을 ${model}(으)로 변경하고 대화 문맥을 초기화했습니다.`);
      }
      return true;
    case "/effort":
      if (!value || !["low", "medium", "high"].includes(value.toLowerCase())) {
        console.log(`현재 추론 강도: ${reasoningEffort}. 사용법: /effort low|medium|high`);
      } else {
        reasoningEffort = value.toLowerCase();
        previousResponseId = null;
        activeResponseId = null;
        console.log(`추론 강도를 ${reasoningEffort}(으)로 변경하고 대화 문맥을 초기화했습니다.`);
      }
      return true;
    case "/status":
      console.log(JSON.stringify({
        model,
        reasoning_effort: reasoningEffort,
        previous_response_id: previousResponseId,
        input_tokens: totalInputTokens,
        output_tokens: totalOutputTokens,
        changed_files: [...changedPaths].map(toProjectRelative).sort(),
        project: projectRoot,
      }, null, 2));
      return true;
    case "/undo": {
      const { restored, skipped } = await undoSessionChanges();
      previousResponseId = null;
      activeResponseId = null;
      if (restored.length) console.log(`복원 완료:\n- ${restored.join("\n- ")}`);
      if (skipped.length) console.log(`외부에서 다시 변경되어 복원을 건너뜀:\n- ${skipped.join("\n- ")}`);
      if (!restored.length && !skipped.length) console.log("복원할 세션 변경이 없습니다.");
      return true;
    }
    case "/exit":
    case "/quit":
      return "exit";
    default:
      console.log("알 수 없는 명령입니다. /help를 입력하세요.");
      return true;
  }
}

async function main() {
  const apiKey = await loadApiKey();
  const oneShotIndex = process.argv.indexOf("--prompt");
  if (oneShotIndex >= 0) {
    const prompt = process.argv.slice(oneShotIndex + 1).join(" ").trim();
    if (!prompt) throw new Error("--prompt 뒤에 작업 내용을 입력하세요.");
    await runAgentTurn(apiKey, prompt);
    return;
  }

  console.log("OpenAI API Dev CLI");
  console.log(`Project: ${projectRoot}`);
  console.log(`Model: ${model} / reasoning: ${reasoningEffort}`);
  console.log("API Platform 크레딧을 사용합니다. /help로 명령을 확인하세요.");

  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  try {
    while (true) {
      const input = (await rl.question("\napi-dev> ")).trim();
      if (!input) continue;
      if (input.startsWith("/")) {
        const handled = await handleSlashCommand(input);
        if (handled === "exit") break;
        continue;
      }
      try {
        await runAgentTurn(apiKey, input);
      } catch (error) {
        console.error(`\n[오류] ${error.message}`);
        if (activeResponseId) console.error(`[마지막 응답 ID] ${activeResponseId}`);
      }
    }
  } finally {
    rl.close();
    await rm(backupRoot, { recursive: true, force: true }).catch(() => {});
  }
}

main().catch((error) => {
  console.error(`[시작 실패] ${error.message}`);
  process.exitCode = 1;
});
