import { spawn } from "node:child_process";
import { createHash, randomUUID } from "node:crypto";
import { existsSync } from "node:fs";
import { lstat, mkdir, readFile, readdir, rm, stat, writeFile } from "node:fs/promises";
import { createServer } from "node:http";
import { tmpdir } from "node:os";
import path from "node:path";
import process from "node:process";
import readline from "node:readline/promises";
import { fileURLToPath } from "node:url";

const APP_NAME = "Unity Game Jam";
const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(scriptDir, "..", "..");
const workspaceRoot = path.resolve(projectRoot, "..");
const envPath = path.join(workspaceRoot, ".env.local");
const statePath = path.join(workspaceRoot, ".unity-game-jam-state.json");
const memoryPath = path.join(workspaceRoot, ".unity-game-jam-memory.json");
const sessionId = `${Date.now()}-${randomUUID().slice(0, 8)}`;
const backupRoot = path.join(tmpdir(), "openai-api-dev-cli", sessionId);

const MODEL_ALIASES = Object.freeze({
  astra: "gpt-6.2-astra",
  sol: "gpt-6.2-sol",
  luna: "gpt-6-luna",
});

// Always offered in the model picker, even when the model list API is unreachable.
const BUILTIN_MODELS = Object.freeze(["gpt-6.2-astra", "gpt-6.2-sol", "gpt-6.1-sol", "gpt-6-luna"]);
const REASONING_EFFORTS = Object.freeze(["low", "medium", "high"]);
const MODEL_ID_PATTERN = /^[A-Za-z0-9._:-]{1,80}$/;

const SKIPPED_DIRECTORIES = new Set([
  ".git",
  ".vs",
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
  ".asmdef", ".asmref", ".asset", ".bat", ".cginc", ".cmd", ".compute", ".cs",
  ".glslinc", ".gradle", ".hlsl", ".ini", ".js", ".json", ".md", ".meta", ".mjs",
  ".prefab", ".props", ".ps1", ".py", ".rsp", ".shader", ".sh", ".targets", ".toml",
  ".ts", ".txt", ".unity", ".uss", ".uxml", ".xml", ".yaml", ".yml",
]);

const WRITABLE_EXTENSIONS = new Set([
  ".asmdef", ".asmref", ".asset", ".bat", ".cginc", ".cmd", ".compute", ".cs",
  ".glslinc", ".gradle", ".hlsl", ".ini", ".js", ".json", ".md", ".meta", ".mjs",
  ".prefab", ".props", ".ps1", ".py", ".rsp", ".shader", ".sh", ".targets", ".toml",
  ".ts", ".txt", ".unity", ".uss", ".uxml", ".xml", ".yaml", ".yml",
]);

const WRITABLE_FILENAMES = new Set([".editorconfig", ".gitattributes", ".gitignore"]);

// Files whose content feeds the stable project overview in the instructions.
const KEY_CONTEXT_FILES = new Set([
  "agents.md", "readme.md", "packages/manifest.json", "projectsettings/projectversion.txt",
  "projectsettings/editorbuildsettings.asset", "projectsettings/tagmanager.asset",
  "projectsettings/projectsettings.asset",
]);

// Korean words commonly used in requests, mapped to identifiers likely to appear in code.
const KOREAN_CODE_TERMS = Object.freeze({
  플레이어: ["player"], 적: ["enemy"], 몬스터: ["enemy", "monster"], 보스: ["boss"],
  점프: ["jump"], 이동: ["move", "movement"], 카메라: ["camera"], 파츠: ["part"],
  부품: ["part"], 키메라: ["chimera"], 물: ["water"], 수조: ["water", "tank"],
  드론: ["drone"], 과학자: ["scientist"], 씬: ["scene"], 테스트: ["test"],
  애니메이션: ["anim"], 사운드: ["audio", "sound"], 소리: ["audio", "sound"],
  체력: ["health", "hp"], 공격: ["attack"], 무기: ["weapon"], 아이템: ["item"],
  스테이지: ["stage"], 타이틀: ["title"], 엔딩: ["ending"], 배경: ["background", "parallax"],
  패럴랙스: ["parallax"], 먼지: ["dust"], 셰이더: ["shader"], 입력: ["input"],
  플랫폼: ["platform"], 발판: ["platform"], 충돌: ["collision", "collider"],
  레이어: ["layer"], 비주얼: ["visual"], 이펙트: ["effect", "fx"], 파티클: ["particle"],
  게임오버: ["gameover"], 리스폰: ["respawn"], 체크포인트: ["checkpoint"],
  대화: ["dialog", "dialogue"], 저장: ["save"], 설정: ["settings", "config"],
});

const SEARCH_STOPWORDS = new Set([
  "the", "and", "for", "with", "this", "that", "from", "into", "file", "files", "code",
  "unity", "please", "fix", "bug", "add", "make", "use", "test", "tests", "script",
]);

const MAX_READ_BYTES = 512 * 1024;
const MAX_TOOL_OUTPUT = 48_000;
const MAX_TOOL_ROUNDS = 32;
const DEFAULT_CONTEXT_TOKENS = Number.parseInt(process.env.OPENAI_DEV_CONTEXT_TOKENS || "", 10) || 1_050_000;
const CHANGE_LOG_LIMIT = 2000;
const MEMORY_LIMIT = 300;

let model = process.env.OPENAI_DEV_MODEL || MODEL_ALIASES.astra;
let reasoningEffort = REASONING_EFFORTS.includes(process.env.OPENAI_DEV_REASONING)
  ? process.env.OPENAI_DEV_REASONING : "medium";
let writeScope = process.env.OPENAI_DEV_WRITE_SCOPE === "assets" ? "assets" : "project";
let activeResponseId = null;
let activeWebRequests = 0;
let mutationQueue = Promise.resolve();
const webSessions = new Map();
const readPaths = new Set();
const backups = new Map();
const changedPaths = new Set();
const lastWrittenHashes = new Map();
let stateSaveQueue = Promise.resolve();
let memorySaveQueue = Promise.resolve();

// The CLI has a single conversation; it uses the same shape as a web session.
const cliSession = createSessionRecord({ register: false });

const fileIndex = new Map();
const changeLog = [];
const agentWrites = new Map();
let indexScannedAt = 0;
let indexScanPromise = null;
let projectContext = "";
let projectContextDirty = true;
let memoryEntries = [];
let usageTotals = { all: emptyUsage(), by_day: {} };
let modelCatalog = { models: BUILTIN_MODELS.map(describeModel), fetched_at: null, source: "builtin", error: null };

function emptyUsage() {
  return { input: 0, cached: 0, output: 0, reasoning: 0, calls: 0 };
}

function addUsage(target, usage) {
  for (const key of Object.keys(emptyUsage())) target[key] = (target[key] || 0) + (usage[key] || 0);
}

function todayKey() {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
}

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
    segment === "auth.json" || segment === "credentials.json" || segment === "secrets.json" ||
    segment === ".npmrc" || segment === ".unity-game-jam-state.json" ||
    segment === ".unity-game-jam-memory.json" || segment.startsWith(".env"));
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
  const segments = lower.split("/");
  if (segments.some((segment) => isSkippedDirectory(segment))) {
    throw new Error("Unity 생성물, 캐시 또는 버전 관리 내부 폴더는 수정할 수 없습니다.");
  }
  if (lower === "90. tools/openaidevcli" || lower.startsWith("90. tools/openaidevcli/")) {
    throw new Error("API 에이전트는 자신의 실행 코드와 권한 규칙을 수정할 수 없습니다.");
  }
  if (writeScope === "assets" && !(lower === "assets" || lower.startsWith("assets/"))) {
    throw new Error("현재 쓰기 범위는 Assets로 제한되어 있습니다. /scope project로 변경하세요.");
  }
  const extension = path.extname(absolutePath).toLowerCase();
  const fileName = path.basename(absolutePath).toLowerCase();
  if (!WRITABLE_EXTENSIONS.has(extension) && !WRITABLE_FILENAMES.has(fileName)) {
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

// ---------------------------------------------------------------------------
// Project index: file list with modification times, change log, and symbols.
// ---------------------------------------------------------------------------

function isKeyContextFile(relative) {
  const lower = relative.toLowerCase();
  return KEY_CONTEXT_FILES.has(lower) || lower.endsWith(".asmdef") || lower.endsWith("/agents.md");
}

function scanProjectIndex(force = false) {
  if (!force && Date.now() - indexScannedAt < 4000) return Promise.resolve();
  indexScanPromise ||= (async () => {
    const files = (await walkFiles(projectRoot, 20_000)).filter(isReadableFile);
    const firstScan = indexScannedAt === 0;
    const seen = new Set();
    const now = Date.now();
    for (let offset = 0; offset < files.length; offset += 64) {
      const batch = files.slice(offset, offset + 64);
      const infos = await Promise.all(batch.map((file) => stat(file).catch(() => null)));
      batch.forEach((file, index) => {
        const info = infos[index];
        if (!info) return;
        const relative = toProjectRelative(file);
        const key = relative.toLowerCase();
        seen.add(key);
        const previous = fileIndex.get(key);
        if (previous && previous.mtimeMs === info.mtimeMs && previous.size === info.size) return;
        fileIndex.set(key, { path: relative, mtimeMs: info.mtimeMs, size: info.size, symbols: previous?.symbols, symbolsMtime: previous?.symbolsMtime });
        if (!firstScan) recordChange(relative, previous ? "modified" : "added", now);
      });
    }
    for (const [key, entry] of [...fileIndex]) {
      if (seen.has(key)) continue;
      fileIndex.delete(key);
      if (!firstScan) recordChange(entry.path, "deleted", now);
    }
    indexScannedAt = Date.now();
  })().finally(() => { indexScanPromise = null; });
  return indexScanPromise;
}

function recordChange(relative, kind, at) {
  const writer = agentWrites.get(relative.toLowerCase());
  const byAgent = writer && at - writer.at < 10 * 60 * 1000;
  changeLog.push({ path: relative, kind, at, by: byAgent ? "agent" : "external", session_id: byAgent ? writer.sessionId : null });
  if (changeLog.length > CHANGE_LOG_LIMIT) changeLog.splice(0, changeLog.length - CHANGE_LOG_LIMIT);
  if (isKeyContextFile(relative)) projectContextDirty = true;
}

function recentChanges({ since = 0, excludeSessionId = null, limit = 40 } = {}) {
  const latest = new Map();
  for (const entry of changeLog) {
    if (entry.at <= since || entry.path.toLowerCase().endsWith(".meta")) continue;
    if (excludeSessionId && entry.session_id === excludeSessionId) continue;
    latest.set(entry.path.toLowerCase(), entry);
  }
  return [...latest.values()].sort((a, b) => b.at - a.at).slice(0, limit);
}

async function fileSymbols(entry) {
  if (entry.symbols && entry.symbolsMtime === entry.mtimeMs) return entry.symbols;
  let symbols = [];
  if (entry.size <= MAX_READ_BYTES) {
    const text = await readFile(path.join(projectRoot, entry.path), "utf8").catch(() => "");
    const found = new Set();
    for (const match of text.matchAll(/\b(?:class|struct|interface|enum|record)\s+([A-Za-z_]\w*)/g)) found.add(match[1]);
    for (const match of text.matchAll(/\b(?:public|private|protected|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|async\s+)*[\w<>[\],.]+\s+([A-Z]\w*)\s*\(/g)) {
      found.add(match[1]);
    }
    symbols = [...found].slice(0, 60);
  }
  entry.symbols = symbols;
  entry.symbolsMtime = entry.mtimeMs;
  return symbols;
}

function extractSearchTerms(message) {
  const terms = new Set();
  for (const word of message.match(/[A-Za-z_][A-Za-z0-9_]{2,}/g) || []) {
    const lower = word.toLowerCase();
    if (!SEARCH_STOPWORDS.has(lower)) terms.add(lower);
    for (const part of word.split(/(?<=[a-z0-9])(?=[A-Z])|_/)) {
      const partLower = part.toLowerCase();
      if (partLower.length >= 3 && !SEARCH_STOPWORDS.has(partLower)) terms.add(partLower);
    }
  }
  for (const [korean, english] of Object.entries(KOREAN_CODE_TERMS)) {
    if (message.includes(korean)) english.forEach((term) => terms.add(term));
  }
  return [...terms].slice(0, 30);
}

async function selectRelevantFiles(message, limit = 8) {
  const terms = extractSearchTerms(message);
  if (!terms.length) return [];
  const recentCutoff = Date.now() - 30 * 60 * 1000;
  const recentlyChanged = new Set(changeLog.filter((entry) => entry.at > recentCutoff).map((entry) => entry.path.toLowerCase()));
  const scored = [];
  for (const [key, entry] of fileIndex) {
    if (!key.startsWith("assets/") || !/\.(cs|shader|hlsl|compute|uss|uxml|asmdef)$/.test(key)) continue;
    const baseName = path.basename(key).replace(/\.[^.]+$/, "");
    const directory = path.dirname(key);
    const symbols = key.endsWith(".cs") ? await fileSymbols(entry) : [];
    const lowerSymbols = symbols.map((symbol) => symbol.toLowerCase());
    let score = 0;
    const hits = new Set();
    for (const term of terms) {
      if (baseName === term) score += 10;
      else if (baseName.includes(term)) score += 6;
      if (directory.includes(term)) score += 2;
      const symbolHits = symbols.filter((_, index) => lowerSymbols[index].includes(term));
      if (symbolHits.length) {
        score += Math.min(6, symbolHits.length * 2);
        symbolHits.slice(0, 4).forEach((symbol) => hits.add(symbol));
      }
    }
    if (score === 0) continue;
    if (recentlyChanged.has(key)) score += 2;
    if (/externals|textmesh pro/.test(key)) score *= 0.3;
    scored.push({ path: entry.path, score, symbols: hits.size ? [...hits] : symbols.slice(0, 4) });
  }
  return scored.sort((a, b) => b.score - a.score).slice(0, limit);
}

// ---------------------------------------------------------------------------
// Stable project overview built from AGENTS.md, README, packages, asmdef and
// ProjectSettings. It changes only when one of those files changes, so the
// instruction prefix stays identical between requests and hits the prompt cache.
// ---------------------------------------------------------------------------

async function readProjectText(relative, maxChars = 12_000) {
  const absolute = path.join(projectRoot, relative);
  if (!existsSync(absolute) || isSecretPath(absolute)) return "";
  const text = await readFile(absolute, "utf8").catch(() => "");
  return text.length > maxChars ? `${text.slice(0, maxChars)}\n…(이하 생략)` : text;
}

function parseBuildScenes(yaml) {
  const scenes = [];
  const pattern = /- enabled: (\d)\s*\n\s*path: (.+)/g;
  for (const match of yaml.matchAll(pattern)) scenes.push(`${match[2].trim()}${match[1] === "1" ? "" : " (비활성)"}`);
  return scenes;
}

function parseTagManager(yaml) {
  const result = { tags: [], layers: [], sorting: [] };
  let section = null;
  let layerIndex = 0;
  for (const line of yaml.split(/\r?\n/)) {
    if (/^ {2}tags:/.test(line)) { section = line.includes("[]") ? null : "tags"; continue; }
    if (/^ {2}layers:/.test(line)) { section = "layers"; layerIndex = 0; continue; }
    if (/^ {2}m_SortingLayers:/.test(line)) { section = "sorting"; continue; }
    // List items sit at the same indent as their key ("  - Default"); any other key ends the list.
    if (/^ {2}[^\s-]/.test(line)) { section = null; continue; }
    if (section === "tags") {
      const match = line.match(/^ {2}- (.+)$/);
      if (match) result.tags.push(match[1].trim());
    } else if (section === "layers") {
      const match = line.match(/^ {2}-\s?(.*)$/);
      if (match) {
        if (match[1].trim()) result.layers.push(`${layerIndex}:${match[1].trim()}`);
        layerIndex += 1;
      }
    } else if (section === "sorting") {
      const match = line.match(/- name: (.+)$/);
      if (match) result.sorting.push(match[1].trim());
    }
  }
  return result;
}

async function buildProjectContext() {
  await scanProjectIndex();
  const entries = [...fileIndex.values()];
  const count = (pattern) => entries.filter((entry) => pattern.test(entry.path.toLowerCase())).length;
  const sections = [];

  const projectSettings = await readProjectText("ProjectSettings/ProjectSettings.asset", 400_000);
  const productName = projectSettings.match(/productName: (.+)/)?.[1]?.trim();
  const inputHandler = projectSettings.match(/activeInputHandler: (\d)/)?.[1];
  const unityVersion = (await readProjectText("ProjectSettings/ProjectVersion.txt")).match(/m_EditorVersion: (.+)/)?.[1]?.trim();
  sections.push([
    "## 프로젝트 개요",
    `- 제품명: ${productName || "알 수 없음"} / Unity ${unityVersion || "알 수 없음"}`,
    `- 입력 시스템: ${{ 0: "Input Manager(구)", 1: "Input System(신)", 2: "둘 다" }[inputHandler] || "알 수 없음"}`,
    `- 인덱스: 텍스트 파일 ${entries.length}개 (C# ${count(/\.cs$/)}, 씬 ${count(/\.unity$/)}, 프리팹 ${count(/\.prefab$/)}, asmdef ${count(/\.asmdef$/)}, 셰이더 ${count(/\.(shader|hlsl)$/)})`,
  ].join("\n"));

  const folders = new Map();
  for (const entry of entries) {
    const segments = entry.path.split("/");
    if (segments[0] !== "Assets" || segments.length < 2) continue;
    const folder = segments.slice(0, Math.min(3, segments.length - 1)).join("/") || "Assets";
    const stats = folders.get(folder) || { cs: 0, other: 0 };
    if (entry.path.endsWith(".cs")) stats.cs += 1;
    else if (!entry.path.endsWith(".meta")) stats.other += 1;
    folders.set(folder, stats);
  }
  const folderLines = [...folders].sort(([a], [b]) => a.localeCompare(b)).slice(0, 80)
    .filter(([folder]) => folder !== "Assets")
    .map(([folder, stats]) => `- ${folder}${stats.cs || stats.other ? ` (C# ${stats.cs}, 기타 텍스트 ${stats.other})` : ""}`);
  if (folderLines.length) sections.push(`## Assets 폴더 구조\n${folderLines.join("\n")}`);

  const buildScenes = parseBuildScenes(await readProjectText("ProjectSettings/EditorBuildSettings.asset", 100_000));
  if (buildScenes.length) sections.push(`## 빌드 씬 순서\n${buildScenes.map((scene, index) => `${index}. ${scene}`).join("\n")}`);

  const tags = parseTagManager(await readProjectText("ProjectSettings/TagManager.asset", 100_000));
  sections.push([
    "## 태그·레이어",
    `- 사용자 태그: ${tags.tags.join(", ") || "없음"}`,
    `- 레이어: ${tags.layers.join(", ") || "기본"}`,
    `- 소팅 레이어: ${tags.sorting.join(", ") || "Default"}`,
  ].join("\n"));

  try {
    const manifest = JSON.parse(await readProjectText("Packages/manifest.json", 100_000));
    const packages = Object.entries(manifest.dependencies || {})
      .filter(([name]) => !name.startsWith("com.unity.modules."))
      .map(([name, version]) => `${name}@${version}`);
    sections.push(`## 패키지 (내장 모듈 제외)\n${packages.join(", ")}`);
  } catch {
    // A missing or malformed manifest is reported by Unity itself.
  }

  const asmdefLines = [];
  for (const entry of entries.filter((item) => item.path.toLowerCase().endsWith(".asmdef") && item.path.startsWith("Assets/"))) {
    try {
      const asmdef = JSON.parse(await readProjectText(entry.path, 50_000));
      const details = [
        asmdef.references?.length ? `참조: ${asmdef.references.join(", ")}` : null,
        asmdef.includePlatforms?.length ? `플랫폼: ${asmdef.includePlatforms.join(", ")}` : null,
        asmdef.defineConstraints?.length ? `조건: ${asmdef.defineConstraints.join(", ")}` : null,
      ].filter(Boolean).join(" / ");
      asmdefLines.push(`- ${asmdef.name} (${entry.path})${details ? ` — ${details}` : ""}`);
    } catch {
      asmdefLines.push(`- ${entry.path} (해석 실패)`);
    }
  }
  if (asmdefLines.length) sections.push(`## 어셈블리(asmdef)\n${asmdefLines.join("\n")}`);

  const docPaths = entries.map((entry) => entry.path).filter((file) => /\.md$/i.test(file) && /^Assets\/99\. Documents\//.test(file));
  if (docPaths.length) sections.push(`## 문서 파일 (필요하면 read_file)\n${docPaths.slice(0, 40).map((file) => `- ${file}`).join("\n")}`);

  for (const [label, absolute] of [
    ["AGENTS.md (저장소 루트)", path.join(workspaceRoot, "AGENTS.md")],
    ["AGENTS.md (프로젝트)", path.join(projectRoot, "AGENTS.md")],
    ["README.md (프로젝트)", path.join(projectRoot, "README.md")],
  ]) {
    if (!existsSync(absolute)) continue;
    const text = await readFile(absolute, "utf8").catch(() => "");
    if (text.trim()) sections.push(`## ${label}\n${text.slice(0, 12_000)}`);
  }

  projectContext = `# 프로젝트 자동 이해\n\n${sections.join("\n\n")}`;
  projectContextDirty = false;
  return projectContext;
}

async function getProjectContext() {
  await scanProjectIndex();
  if (projectContextDirty || !projectContext) await buildProjectContext();
  return projectContext;
}

// ---------------------------------------------------------------------------
// Long-term memory: project decisions and work summaries that outlive sessions.
// ---------------------------------------------------------------------------

async function loadMemory() {
  if (!existsSync(memoryPath)) return;
  const saved = JSON.parse(await readFile(memoryPath, "utf8"));
  memoryEntries = Array.isArray(saved.entries) ? saved.entries.filter((entry) => entry?.id && entry?.text) : [];
}

function persistMemory() {
  const payload = JSON.stringify({ version: 1, savedAt: new Date().toISOString(), entries: memoryEntries }, null, 2);
  memorySaveQueue = memorySaveQueue.then(() => writeFile(memoryPath, payload, "utf8"), () => writeFile(memoryPath, payload, "utf8"));
  return memorySaveQueue;
}

async function addMemory(kind, text, source = null) {
  const clean = String(text || "").replace(/\s+/g, " ").trim().slice(0, 600);
  if (!clean) throw new Error("기억할 내용이 비어 있습니다.");
  if (/sk-[A-Za-z0-9_-]{16,}|api[_-]?key\s*[:=]/i.test(clean)) throw new Error("비밀정보로 보이는 내용은 기억하지 않습니다.");
  const entry = { id: randomUUID().slice(0, 8), kind, text: clean, at: new Date().toISOString(), source };
  memoryEntries.push(entry);
  // Decisions are kept; the oldest summaries and notes are dropped first.
  while (memoryEntries.length > MEMORY_LIMIT) {
    const index = memoryEntries.findIndex((item) => item.kind !== "decision");
    memoryEntries.splice(index >= 0 ? index : 0, 1);
  }
  await persistMemory();
  return entry;
}

async function deleteMemory(id) {
  const index = memoryEntries.findIndex((entry) => entry.id === id);
  if (index < 0) throw new Error("해당 기억을 찾을 수 없습니다.");
  const [removed] = memoryEntries.splice(index, 1);
  await persistMemory();
  return removed;
}

function memoryContext() {
  const decisions = memoryEntries.filter((entry) => entry.kind === "decision").slice(-60);
  const others = memoryEntries.filter((entry) => entry.kind !== "decision").slice(-25);
  if (!decisions.length && !others.length) return "# 장기 기억\n아직 저장된 결정사항이나 작업 요약이 없다.";
  const format = (entry) => `- [${entry.id}] ${entry.at.slice(0, 10)} ${entry.text}`;
  return [
    "# 장기 기억 (이전 세션에서 저장한 내용)",
    decisions.length ? `## 결정사항·규칙\n${decisions.map(format).join("\n")}` : "",
    others.length ? `## 최근 작업 요약·메모\n${others.map(format).join("\n")}` : "",
  ].filter(Boolean).join("\n\n");
}

// ---------------------------------------------------------------------------
// File tools
// ---------------------------------------------------------------------------

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

function noteAgentWrite(absolute, session) {
  agentWrites.set(toProjectRelative(absolute).toLowerCase(), { at: Date.now(), sessionId: session?.id ?? null });
}

async function replaceText(args, session) {
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
    : current.replace(args.old_text, () => args.new_text);
  noteAgentWrite(absolute, session);
  await writeFile(absolute, updated, "utf8");
  lastWrittenHashes.set(absolute.toLowerCase(), hashContent(updated));
  changedPaths.add(absolute);
  readPaths.add(absolute.toLowerCase());
  return { path: toProjectRelative(absolute), replacements: args.replace_all ? count : 1 };
}

async function createFile(args, session) {
  const absolute = resolveProjectPath(args.path);
  await assertNoSymlinkPath(absolute);
  assertWritablePath(absolute);
  if (existsSync(absolute)) throw new Error("파일이 이미 존재합니다. read_file 후 replace_text를 사용하세요.");
  const parent = path.dirname(absolute);
  if (!existsSync(parent)) {
    throw new Error("상위 폴더가 없습니다. 폴더 규칙 보호를 위해 자동으로 새 폴더를 만들지 않습니다.");
  }
  await backupBeforeWrite(absolute);
  noteAgentWrite(absolute, session);
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
    const append = (current, chunk) => (current + chunk.toString()).slice(-MAX_TOOL_OUTPUT * 4);
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

// ---------------------------------------------------------------------------
// Unity Editor tools (read-only scene inspection, compile, tests)
// ---------------------------------------------------------------------------

function parseJsonOutput(text) {
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    const items = [];
    for (const line of text.split(/\r?\n/)) {
      if (!line.trim()) continue;
      try { items.push(JSON.parse(line)); } catch { return text; }
    }
    return items;
  }
}

// Editor queries run one at a time: concurrent Pipeline requests can stall while the Editor is busy.
let unityQueryQueue = Promise.resolve();

function unityCommand(commandPath, params = {}, timeoutMs = 30_000) {
  const result = unityQueryQueue.then(() => runUnityCommand(commandPath, params, timeoutMs));
  unityQueryQueue = result.catch(() => {});
  return result;
}

async function runUnityCommand(commandPath, params, timeoutMs) {
  const args = ["command", ...commandPath.split(" ")];
  for (const [name, value] of Object.entries(params)) {
    if (value === null || value === undefined || value === "") continue;
    args.push(`--${name}`, String(value));
  }
  args.push("--project-path", projectRoot, "--format", "json");
  const result = await runProcess("unity", args, { timeoutMs });
  if (result.exit_code !== 0) {
    const detail = (result.stderr || result.stdout || "응답 없음").slice(0, 2000);
    throw new Error(`Unity 명령 실패 (${commandPath}): ${detail}${result.timed_out ? " (시간 초과)" : ""}. Unity Editor가 이 프로젝트로 열려 있는지 확인하세요.`);
  }
  return parseJsonOutput(result.stdout);
}

async function unityStatus() {
  return runProcess("unity", ["status", "--project-path", projectRoot, "--format", "json"], { timeoutMs: 30_000 });
}

async function unitySceneState() {
  const editor = await unityCommand("editor status");
  const scenes = await unityCommand("scene list_open");
  const editorSummary = editor && typeof editor === "object" ? {
    status: editor.status, compiling: editor.compiling, play_mode: editor.playMode,
    unity_version: editor.unityVersion, domain_reload: editor.domainReloadInProgress,
  } : editor;
  return { editor: editorSummary, open_scenes: Array.isArray(scenes) ? scenes : [scenes].filter(Boolean) };
}

function compactHierarchy(snapshot, { rootPath = null, maxDepth = 6, maxNodes = 700 } = {}) {
  const lines = [];
  let nodes = 0;
  let truncated = false;
  const visit = (node, depth) => {
    if (nodes >= maxNodes) { truncated = true; return; }
    nodes += 1;
    const components = (node.components || []).filter((name) => name !== "Transform" && name !== "RectTransform");
    const flags = node.activeSelf === false ? " (비활성)" : "";
    const children = node.children || [];
    let line = `${"  ".repeat(depth)}${node.name}${flags}${components.length ? ` [${components.join(", ")}]` : ""}`;
    if (depth >= maxDepth && children.length) {
      line += ` …자식 ${children.length}개`;
      lines.push(line);
      return;
    }
    lines.push(line);
    for (const child of children) visit(child, depth + 1);
  };
  const findNode = (list) => {
    for (const node of list || []) {
      if (node.hierarchyPath === rootPath) return node;
      const found = findNode(node.children);
      if (found) return found;
    }
    return null;
  };
  let roots = snapshot.roots || [];
  if (rootPath) {
    const node = findNode(roots);
    if (!node) throw new Error(`'${rootPath}' 경로의 GameObject가 없습니다. hierarchyPath는 '/Root/Child' 형식입니다.`);
    roots = [node];
  }
  for (const root of roots) visit(root, 0);
  return {
    scene: snapshot.sceneName,
    scene_path: snapshot.scenePath,
    dirty: snapshot.isDirty,
    node_count: nodes,
    truncated,
    note: "각 줄은 '이름 [Transform 외 컴포넌트]'. 하위 경로는 '/부모/자식' 형식의 hierarchyPath로 지정한다.",
    tree: lines.join("\n"),
  };
}

async function unityHierarchy(args) {
  const snapshot = await unityCommand("scene get_hierarchy", { path: args.scene_path }, 60_000);
  if (!snapshot || typeof snapshot !== "object" || Array.isArray(snapshot)) {
    throw new Error("Unity에서 Hierarchy를 받지 못했습니다.");
  }
  const maxDepth = Math.max(1, Math.min(args.max_depth ?? 6, 20));
  return compactHierarchy(snapshot, { rootPath: args.root_path || null, maxDepth });
}

async function unityFindGameObjects(args) {
  if (!args.name && !args.tag && !args.component_type && !args.hierarchy_path) {
    throw new Error("name, tag, component_type, hierarchy_path 중 하나 이상을 지정하세요.");
  }
  const found = await unityCommand("gameobject find", {
    name: args.name, tag: args.tag, type: args.component_type, "hierarchy-path": args.hierarchy_path,
  });
  const items = (Array.isArray(found) ? found : [found]).filter(Boolean);
  return {
    count: items.length,
    objects: items.slice(0, 100).map((item) => ({ hierarchy_path: item.hierarchyPath, instance_id: item.instanceId })),
    truncated: items.length > 100,
  };
}

async function unityComponentFields(args) {
  return unityCommand("component get_fields", {
    target: args.target, component: args.component, field: args.field, detail: "value",
  });
}

async function unitySelection() {
  const selection = await unityCommand("gameobject get_selection");
  return { selection: Array.isArray(selection) ? selection : [selection].filter(Boolean) };
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

const NO_ARGS = { type: "object", properties: {}, required: [], additionalProperties: false };

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
    description: "프로젝트의 텍스트 파일을 줄 번호와 함께 읽는다. 한 번에 최대 400줄이다. 필요한 줄 범위만 읽어 토큰을 아낀다.",
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
    description: "이미 읽은 기존 파일에서 정확히 일치하는 텍스트를 교체한다. Unity 프로젝트 전체의 허용된 텍스트 파일에 쓸 수 있다.",
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
    description: "Unity 프로젝트의 기존 폴더 안에 새 텍스트 파일을 만든다. 폴더는 자동 생성하지 않는다.",
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
    parameters: NO_ARGS,
  },
  {
    type: "function",
    name: "unity_status",
    description: "이 프로젝트의 실행 중인 Unity Editor와 Pipeline 연결 상태를 확인한다.",
    strict: true,
    parameters: NO_ARGS,
  },
  {
    type: "function",
    name: "unity_scene_state",
    description: "실행 중인 Unity Editor의 상태(컴파일·플레이 모드)와 열린 씬 목록(활성·저장 안 됨 여부)을 조회한다. 읽기 전용.",
    strict: true,
    parameters: NO_ARGS,
  },
  {
    type: "function",
    name: "unity_hierarchy",
    description: "열린 씬의 실시간 Hierarchy를 GameObject 트리와 컴포넌트 이름으로 압축해 조회한다. 큰 씬은 root_path와 max_depth로 범위를 좁힌다. 읽기 전용.",
    strict: true,
    parameters: {
      type: "object",
      properties: {
        scene_path: { type: ["string", "null"], description: "열린 씬 경로. null이면 활성 씬." },
        root_path: { type: ["string", "null"], description: "이 hierarchyPath 아래만 조회. 예: /Player" },
        max_depth: { type: ["integer", "null"], description: "트리 깊이 제한. 기본 6." },
      },
      required: ["scene_path", "root_path", "max_depth"],
      additionalProperties: false,
    },
  },
  {
    type: "function",
    name: "unity_find_gameobjects",
    description: "로드된 씬에서 이름, 태그, 컴포넌트 타입, hierarchyPath로 GameObject를 찾는다(조건은 AND). 읽기 전용.",
    strict: true,
    parameters: {
      type: "object",
      properties: {
        name: { type: ["string", "null"] },
        tag: { type: ["string", "null"] },
        component_type: { type: ["string", "null"], description: "예: Rigidbody2D, PlayerController" },
        hierarchy_path: { type: ["string", "null"] },
      },
      required: ["name", "tag", "component_type", "hierarchy_path"],
      additionalProperties: false,
    },
  },
  {
    type: "function",
    name: "unity_component_fields",
    description: "씬 GameObject의 컴포넌트(또는 에셋)의 현재 직렬화 필드 값을 읽는다. 인스펙터 값 확인용. 읽기 전용.",
    strict: true,
    parameters: {
      type: "object",
      properties: {
        target: { type: "string", description: "hierarchyPath(예: /Player), instanceId, 에셋 경로 또는 GUID" },
        component: { type: ["string", "null"], description: "target이 GameObject일 때 컴포넌트 타입 이름" },
        field: { type: ["string", "null"], description: "특정 필드 경로만 읽을 때. 예: moveSpeed" },
      },
      required: ["target", "component", "field"],
      additionalProperties: false,
    },
  },
  {
    type: "function",
    name: "unity_selection",
    description: "Unity Editor에서 사용자가 현재 선택한 오브젝트를 조회한다. '이거', '선택한 오브젝트' 같은 요청에 사용한다. 읽기 전용.",
    strict: true,
    parameters: NO_ARGS,
  },
  {
    type: "function",
    name: "unity_compile",
    description: "실행 중인 Unity Editor에서 스크립트를 재컴파일하고 새 Console 오류를 확인한다.",
    strict: true,
    parameters: NO_ARGS,
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
  {
    type: "function",
    name: "memory_save",
    description: "다음 세션에서도 기억해야 할 내용을 장기 기억에 저장한다. decision: 사용자가 정한 규칙·설계 결정, summary: 완료한 작업 한 줄 요약, note: 기타 알아둘 사실.",
    strict: true,
    parameters: {
      type: "object",
      properties: {
        kind: { type: "string", enum: ["decision", "summary", "note"] },
        text: { type: "string", description: "한두 문장. 파일 경로나 클래스명을 포함하면 좋다." },
      },
      required: ["kind", "text"],
      additionalProperties: false,
    },
  },
  {
    type: "function",
    name: "memory_delete",
    description: "더 이상 맞지 않는 장기 기억 항목을 id로 삭제한다. 사용자가 결정을 바꾸면 이전 항목을 지우고 새로 저장한다.",
    strict: true,
    parameters: {
      type: "object",
      properties: { id: { type: "string" } },
      required: ["id"],
      additionalProperties: false,
    },
  },
];

const baseInstructions = `당신은 Unity 프로젝트를 직접 다루는 수석 개발 에이전트다.
사용자에게는 한국어로 답한다. 프로젝트 루트는 ${projectRoot} 이다.

규칙:
- 추측하지 말고 list_files, search_text, read_file로 필요한 최소 문맥을 먼저 확인한다.
- 요청마다 '자동 선별 관련 파일'과 '변경된 파일' 안내가 붙을 수 있다. 후보 중 필요한 파일만, 필요한 줄 범위만 읽어 토큰을 아낀다.
- 아래 '프로젝트 자동 이해'와 '장기 기억'을 우선 참고하고, 이미 아는 내용은 다시 읽지 않는다.
- 씬 상태, GameObject 구성, 인스펙터 값은 unity_scene_state, unity_hierarchy, unity_find_gameobjects, unity_component_fields, unity_selection으로 실시간 조회한다. 큰 씬은 root_path와 max_depth로 좁힌다.
- 사용자의 기존 변경을 보존하고 요청과 무관한 파일은 수정하지 않는다.
- 기존 파일을 수정하기 전에 반드시 read_file로 해당 부분을 읽는다.
- Unity 프로젝트 전체의 허용된 텍스트 파일을 수정할 수 있지만, Library/Temp/Logs/Build/.git/비밀정보와 바이너리는 건드리지 않는다.
- 씬, 프리팹, .meta는 사용자가 명시적으로 요구하거나 작업에 꼭 필요한 경우에만 수정하고, 가능한 Unity 도구를 우선한다.
- 90. Tools/OpenAIDevCLI는 자신의 권한 코드이므로 수정하지 않는다.
- 새 파일은 기존 폴더에만 만든다. 폴더를 만들거나 옮기지 않는다.
- MonoBehaviour 하나당 파일 하나를 사용하고 파일명과 클래스명을 일치시킨다.
- 변경 후 git_diff를 확인하고, C# 변경은 unity_compile로 검증한다.
- 관련 테스트가 있으면 가장 좁은 EditMode 테스트부터 실행한다.
- 도구 결과가 실패하면 성공했다고 말하지 말고 원인과 필요한 다음 행동을 명확히 말한다.
- 비밀 키나 .env 파일을 읽거나 요구하지 않는다.
- 삭제, 커밋, 푸시, 패키지 설치, 빌드, Editor 종료, 씬 오브젝트 수정은 할 수 없으며 시도하지 않는다.
- 사용자가 규칙이나 설계 결정을 말하면 memory_save(decision)로 저장한다. 결정이 바뀌면 이전 항목을 memory_delete로 지운다.
- 파일을 변경한 작업을 마치면 memory_save(summary)로 무엇을 왜 바꿨는지 한 줄 요약을 남긴다. 비밀정보는 저장하지 않는다.
- 최종 답변에는 변경 파일, 검증 결과, 남은 문제를 간결하게 정리한다.`;

function safeToolArgs(args) {
  const safe = {};
  for (const key of ["path", "query", "filter", "mode", "scene_path", "root_path", "name", "tag", "component_type", "hierarchy_path", "target", "component", "field", "kind", "id"]) {
    if (args[key] !== undefined && args[key] !== null) safe[key] = args[key];
  }
  return safe;
}

function safeToolSummary(name, args) {
  const safe = safeToolArgs(args);
  return Object.keys(safe).length ? `${name} ${JSON.stringify(safe)}` : name;
}

function serializeMutation(task) {
  const result = mutationQueue.then(task, task);
  mutationQueue = result.catch(() => {});
  return result;
}

const SERIALIZED_TOOLS = new Set(["replace_text", "create_file", "unity_compile", "unity_editmode_tests"]);

async function invokeTool(name, args, session) {
  switch (name) {
    case "list_files": return listFiles(args);
    case "read_file": return readTextFile(args);
    case "search_text": return searchText(args);
    case "replace_text": return replaceText(args, session);
    case "create_file": return createFile(args, session);
    case "git_diff": return gitStatusAndDiff();
    case "unity_status": return unityStatus();
    case "unity_scene_state": return unitySceneState();
    case "unity_hierarchy": return unityHierarchy(args);
    case "unity_find_gameobjects": return unityFindGameObjects(args);
    case "unity_component_fields": return unityComponentFields(args);
    case "unity_selection": return unitySelection();
    case "unity_compile": return unityCompile();
    case "unity_editmode_tests": return unityEditorTests(args);
    case "memory_save": return addMemory(args.kind, args.text, session?.title ?? null);
    case "memory_delete": return deleteMemory(args.id);
    default: throw new Error(`알 수 없는 도구: ${name}`);
  }
}

async function executeTool(call, toolLog, session) {
  let args;
  try {
    args = JSON.parse(call.arguments || "{}");
  } catch {
    return JSON.stringify({ ok: false, error: "도구 인자를 JSON으로 해석할 수 없습니다." });
  }
  const logEntry = { name: call.name, arguments: safeToolArgs(args), status: "running" };
  toolLog.push(logEntry);
  console.log(`  [tool] ${safeToolSummary(call.name, args)}`);
  try {
    const invoke = () => invokeTool(call.name, args, session);
    const result = SERIALIZED_TOOLS.has(call.name) ? await serializeMutation(invoke) : await invoke();
    logEntry.status = "completed";
    const serialized = JSON.stringify({ ok: true, result });
    return serialized.length > MAX_TOOL_OUTPUT
      ? `${serialized.slice(0, MAX_TOOL_OUTPUT)}\n...도구 출력이 잘렸습니다. 범위를 좁혀 다시 조회하세요...`
      : serialized;
  } catch (error) {
    logEntry.status = "failed";
    logEntry.error = error.message.slice(0, 300);
    return JSON.stringify({ ok: false, error: error.message });
  }
}

// ---------------------------------------------------------------------------
// OpenAI Responses API
// ---------------------------------------------------------------------------

function describeModel(id) {
  const match = id.match(/^gpt-(\d+(?:\.\d+)*)-([a-z]+)(?:-(.+))?$/i);
  if (!match) return { id, version: id, family: "other", variant: null };
  return { id, version: match[1], family: match[2].toLowerCase(), variant: match[3] || null };
}

function compareModels(a, b) {
  const versionA = a.version.split(".").map(Number);
  const versionB = b.version.split(".").map(Number);
  for (let index = 0; index < Math.max(versionA.length, versionB.length); index += 1) {
    const difference = (versionB[index] || 0) - (versionA[index] || 0);
    if (difference) return difference;
  }
  if (!a.variant !== !b.variant) return a.variant ? 1 : -1;
  return (b.variant || "").localeCompare(a.variant || "") || a.id.localeCompare(b.id);
}

async function refreshModelCatalog(apiKey, force = false) {
  const fetchedAt = modelCatalog.fetched_at ? Date.parse(modelCatalog.fetched_at) : 0;
  if (!force && Date.now() - fetchedAt < 10 * 60 * 1000) return modelCatalog;
  const ids = new Set(BUILTIN_MODELS);
  let source = "builtin";
  let error = null;
  try {
    const response = await fetch("https://api.openai.com/v1/models", {
      headers: { Authorization: `Bearer ${apiKey}` },
      signal: AbortSignal.timeout(15_000),
    });
    const payload = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(payload?.error?.message || `HTTP ${response.status}`);
    for (const item of payload.data || []) {
      if (typeof item.id !== "string" || !/^gpt-\d/.test(item.id)) continue;
      if (/(audio|realtime|tts|transcribe|image|search|embedding|moderation|instruct)/i.test(item.id)) continue;
      ids.add(item.id);
    }
    source = "api";
  } catch (fetchError) {
    error = fetchError.message;
  }
  const models = [...ids].map(describeModel).sort(compareModels);
  modelCatalog = { models, fetched_at: new Date().toISOString(), source, error };
  return modelCatalog;
}

function isAllowedModel(id) {
  return typeof id === "string" && modelCatalog.models.some((entry) => entry.id === id);
}

function resolveModelId(value) {
  if (typeof value !== "string") return null;
  const alias = MODEL_ALIASES[value.toLowerCase()];
  return alias || value.trim();
}

// The Models API does not report context windows; OPENAI_DEV_CONTEXT_TOKENS overrides the default.
function contextWindowFor() {
  return DEFAULT_CONTEXT_TOKENS;
}

async function createResponse(apiKey, input, { continuationId = null, selectedModel = model, effort = reasoningEffort, cacheKey = null } = {}) {
  const context = await getProjectContext();
  const body = {
    model: selectedModel,
    // Static parts first: identical prefixes across requests are served from the prompt cache.
    instructions: `${baseInstructions}\n\n${context}\n\n${memoryContext()}`,
    input,
    tools,
    tool_choice: "auto",
    reasoning: { effort },
    max_output_tokens: 12_000,
    truncation: "auto",
  };
  if (cacheKey) body.prompt_cache_key = cacheKey;
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

function usageFromResponse(response) {
  const usage = response.usage || {};
  return {
    input: usage.input_tokens || 0,
    cached: usage.input_tokens_details?.cached_tokens || 0,
    output: usage.output_tokens || 0,
    reasoning: usage.output_tokens_details?.reasoning_tokens || 0,
    calls: 1,
  };
}

function recordUsage(response, session, turnUsage) {
  const usage = usageFromResponse(response);
  addUsage(turnUsage, usage);
  addUsage(session.usage, usage);
  addUsage(usageTotals.all, usage);
  usageTotals.by_day[todayKey()] ||= emptyUsage();
  addUsage(usageTotals.by_day[todayKey()], usage);
  // The latest call's input + output is the size of the conversation the model now holds.
  session.contextTokens = usage.input + usage.output;
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

async function buildTurnContext(message, session) {
  await scanProjectIndex();
  const since = session.lastTurnAt ? Date.parse(session.lastTurnAt) : serverStartedAt;
  const changes = recentChanges({ since, excludeSessionId: session.id, limit: 30 });
  const relevant = await selectRelevantFiles(message);
  const parts = [];
  if (changes.length) {
    const kindLabel = { added: "추가", modified: "수정", deleted: "삭제" };
    parts.push(`지난 요청 이후 변경된 파일 (다른 세션·사용자·Unity 변경 포함, 필요하면 다시 읽을 것):\n${changes
      .map((entry) => `- ${entry.path} (${kindLabel[entry.kind]}${entry.by === "agent" ? ", 다른 API 세션" : ""})`).join("\n")}`);
  }
  if (relevant.length) {
    parts.push(`자동 선별 관련 파일 후보 (참고용, 필요한 것만 읽을 것):\n${relevant
      .map((item) => `- ${item.path}${item.symbols.length ? ` — ${item.symbols.join(", ")}` : ""}`).join("\n")}`);
  }
  return { text: parts.join("\n\n"), changes, relevant };
}

async function runAgentTurn(apiKey, userInput, { silent = false, session = cliSession } = {}) {
  const toolLog = [];
  const turnUsage = emptyUsage();
  const selectedModel = session.model;
  const effort = session.reasoningEffort || reasoningEffort;
  const turnContext = await buildTurnContext(userInput, session);
  session.lastTurnAt = new Date().toISOString();
  const input = turnContext.text
    ? [{ role: "developer", content: turnContext.text }, { role: "user", content: userInput }]
    : userInput;
  const options = { selectedModel, effort, cacheKey: `unity-game-jam-${session.id}` };
  let response = await createResponse(apiKey, input, { ...options, continuationId: session.previousResponseId });
  for (let round = 0; round < MAX_TOOL_ROUNDS; round += 1) {
    recordUsage(response, session, turnUsage);
    activeResponseId = response.id;
    session.activeResponseId = response.id;
    const calls = (response.output || []).filter((item) => item.type === "function_call");
    if (calls.length === 0) {
      session.previousResponseId = response.id;
      session.updatedAt = new Date().toISOString();
      const text = extractText(response);
      if (!silent) {
        console.log(`\n${text || "응답 텍스트가 없습니다."}`);
        console.log(`\n[tokens] 이번 요청 input ${turnUsage.input.toLocaleString()} (캐시 ${turnUsage.cached.toLocaleString()}) / output ${turnUsage.output.toLocaleString()} · 세션 누적 input ${session.usage.input.toLocaleString()} / output ${session.usage.output.toLocaleString()}`);
      }
      const changedFiles = [...new Set(toolLog
        .filter((entry) => ["replace_text", "create_file"].includes(entry.name) && entry.status === "completed")
        .map((entry) => entry.arguments.path))];
      return {
        text: text || "응답 텍스트가 없습니다.",
        tools: toolLog,
        model: selectedModel,
        reasoning_effort: effort,
        write_scope: writeScope,
        usage: turnUsage,
        changed_files: changedFiles,
        relevant_files: turnContext.relevant.map((item) => item.path),
        input_tokens: session.usage.input,
        output_tokens: session.usage.output,
      };
    }
    const outputs = [];
    for (const call of calls) {
      outputs.push({
        type: "function_call_output",
        call_id: call.call_id,
        output: await executeTool(call, toolLog, session),
      });
    }
    response = await createResponse(apiKey, outputs, { ...options, continuationId: response.id });
  }
  throw new Error(`도구 호출이 ${MAX_TOOL_ROUNDS}회를 넘었습니다. 작업을 더 작게 나눠주세요.`);
}

// Saves a work summary when the model changed files but did not write one itself.
async function ensureWorkSummary(result, message, session) {
  if (!result.changed_files.length) return;
  if (result.tools.some((entry) => entry.name === "memory_save" && entry.arguments.kind === "summary" && entry.status === "completed")) return;
  const request = message.replace(/\s+/g, " ").slice(0, 80);
  const files = result.changed_files.slice(0, 6).join(", ");
  await addMemory("summary", `요청 "${request}" → 변경: ${files}${result.changed_files.length > 6 ? " 외" : ""}`, session.title).catch(() => {});
}

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

function printHelp() {
  console.log(`
명령:
  /help                 도움말
  /new                  대화 문맥 초기화
  /model                현재 모델과 선택 가능한 모델 확인
  /model <id|alias>     모델 변경 후 문맥 초기화 (예: /model gpt-6.1-sol, /model astra)
  /effort low|medium|high 추론 강도 변경
  /scope assets|project 쓰기 범위 변경 후 문맥 초기화
  /status               모델, 토큰, 이번 세션 변경 파일 확인
  /memory               장기 기억 목록
  /undo                 이번 CLI 세션이 변경한 파일 전부 복원
  /exit                 종료

일반 문장은 ${APP_NAME} 에이전트에게 전달됩니다.`);
}

function resetConversation(session) {
  session.previousResponseId = null;
  session.activeResponseId = null;
  session.contextTokens = 0;
  activeResponseId = null;
}

async function handleSlashCommand(input, apiKey) {
  const [command, value] = input.trim().split(/\s+/, 2);
  switch (command.toLowerCase()) {
    case "/help":
      printHelp();
      return true;
    case "/new":
      resetConversation(cliSession);
      console.log("대화 문맥을 초기화했습니다.");
      return true;
    case "/model": {
      await refreshModelCatalog(apiKey);
      if (!value) {
        console.log(`현재 모델: ${cliSession.model}\n선택 가능: ${modelCatalog.models.map((entry) => entry.id).join(", ")}`);
        return true;
      }
      const selected = resolveModelId(value);
      if (!isAllowedModel(selected)) {
        console.log(`지원하지 않는 모델입니다: ${value}`);
        return true;
      }
      cliSession.model = selected;
      resetConversation(cliSession);
      console.log(`모델을 ${selected}(으)로 변경하고 대화 문맥을 초기화했습니다.`);
      return true;
    }
    case "/effort":
      if (!value || !REASONING_EFFORTS.includes(value.toLowerCase())) {
        console.log(`현재 추론 강도: ${cliSession.reasoningEffort}. 사용법: /effort low|medium|high`);
      } else {
        cliSession.reasoningEffort = value.toLowerCase();
        console.log(`추론 강도를 ${cliSession.reasoningEffort}(으)로 변경했습니다.`);
      }
      return true;
    case "/scope":
      if (!value || !["assets", "project"].includes(value.toLowerCase())) {
        console.log(`현재 쓰기 범위: ${writeScope}. 사용법: /scope assets|project`);
      } else {
        writeScope = value.toLowerCase();
        resetConversation(cliSession);
        console.log(`쓰기 범위를 ${writeScope}(으)로 변경하고 대화 문맥을 초기화했습니다.`);
      }
      return true;
    case "/status":
      console.log(JSON.stringify({
        model: cliSession.model,
        reasoning_effort: cliSession.reasoningEffort,
        write_scope: writeScope,
        previous_response_id: cliSession.previousResponseId,
        usage: cliSession.usage,
        context_tokens: cliSession.contextTokens,
        context_remaining_tokens: Math.max(0, contextWindowFor(cliSession.model) - cliSession.contextTokens),
        changed_files: [...changedPaths].map(toProjectRelative).sort(),
        project: projectRoot,
      }, null, 2));
      return true;
    case "/memory":
      console.log(memoryContext());
      return true;
    case "/undo": {
      const { restored, skipped } = await undoSessionChanges();
      resetConversation(cliSession);
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

// ---------------------------------------------------------------------------
// Web sessions
// ---------------------------------------------------------------------------

const serverStartedAt = Date.now();

function createSessionRecord({ register = true } = {}) {
  const now = new Date().toISOString();
  const session = {
    id: randomUUID(),
    title: "새 세션",
    model,
    reasoningEffort,
    previousResponseId: null,
    activeResponseId: null,
    usage: emptyUsage(),
    contextTokens: 0,
    messageCount: 0,
    history: [],
    busy: false,
    lastTurnAt: null,
    createdAt: now,
    updatedAt: now,
  };
  if (register) webSessions.set(session.id, session);
  return session;
}

function persistSessions() {
  const sessions = [...webSessions.values()].map(({ busy, activeResponseId: _active, ...session }) => ({ ...session }));
  const payload = JSON.stringify({ version: 2, savedAt: new Date().toISOString(), usage: usageTotals, sessions }, null, 2);
  stateSaveQueue = stateSaveQueue.then(() => writeFile(statePath, payload, "utf8"), () => writeFile(statePath, payload, "utf8"));
  return stateSaveQueue;
}

async function loadPersistentSessions() {
  if (!existsSync(statePath)) return;
  const saved = JSON.parse(await readFile(statePath, "utf8"));
  if (saved.usage?.all) {
    usageTotals = { all: { ...emptyUsage(), ...saved.usage.all }, by_day: saved.usage.by_day || {} };
  }
  for (const item of saved.sessions || []) {
    if (!item.id || !MODEL_ID_PATTERN.test(item.model || "")) continue;
    // Version 1 stored token totals as flat fields.
    const usage = item.usage
      ? { ...emptyUsage(), ...item.usage }
      : { ...emptyUsage(), input: item.inputTokens || 0, output: item.outputTokens || 0 };
    webSessions.set(item.id, {
      ...createSessionRecord({ register: false }),
      id: item.id,
      title: item.title || "새 세션",
      model: item.model,
      reasoningEffort: REASONING_EFFORTS.includes(item.reasoningEffort) ? item.reasoningEffort : reasoningEffort,
      previousResponseId: item.previousResponseId || null,
      usage,
      contextTokens: item.contextTokens ?? ((item.lastInputTokens || 0) + (item.lastOutputTokens || 0)),
      messageCount: item.messageCount || 0,
      history: Array.isArray(item.history) ? item.history.slice(-500) : [],
      lastTurnAt: item.lastTurnAt || null,
      createdAt: item.createdAt || new Date().toISOString(),
      updatedAt: item.updatedAt || new Date().toISOString(),
    });
  }
  // State files from version 1 had no overall totals; start them from the sessions' own totals.
  if (!saved.usage?.all) {
    for (const session of webSessions.values()) addUsage(usageTotals.all, session.usage);
  }
}

function contextView(session) {
  const window = contextWindowFor(session.model);
  return {
    context_used_tokens: session.contextTokens,
    context_remaining_tokens: Math.max(0, window - session.contextTokens),
    context_window_tokens: window,
  };
}

function webSessionSummary(session) {
  return {
    id: session.id,
    title: session.title,
    model: session.model,
    reasoning_effort: session.reasoningEffort,
    usage: session.usage,
    ...contextView(session),
    updated_at: session.updatedAt,
    busy: session.busy,
  };
}

function usageOverview() {
  return { today: usageTotals.by_day[todayKey()] || emptyUsage(), all: usageTotals.all };
}

function webState(session) {
  return {
    session_id: session.id,
    title: session.title,
    model: session.model,
    reasoning_effort: session.reasoningEffort,
    write_scope: writeScope,
    usage: session.usage,
    ...contextView(session),
    totals: usageOverview(),
    history: session.history,
    changed_files: [...changedPaths].map(toProjectRelative).sort(),
    busy: session.busy,
    active_requests: activeWebRequests,
  };
}

function projectInfo() {
  return {
    project_root: projectRoot,
    indexed_files: fileIndex.size,
    index_scanned_at: indexScannedAt ? new Date(indexScannedAt).toISOString() : null,
    recent_changes: recentChanges({ since: 0, limit: 40 }).map((entry) => ({
      path: entry.path, kind: entry.kind, by: entry.by, at: new Date(entry.at).toISOString(),
    })),
    changed_by_agent: [...changedPaths].map(toProjectRelative).sort(),
    memory: [...memoryEntries].reverse(),
    totals: usageOverview(),
  };
}

function getWebSession(id) {
  return typeof id === "string" ? webSessions.get(id) : null;
}

function sendJson(response, status, value) {
  const body = JSON.stringify(value);
  response.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Content-Length": Buffer.byteLength(body),
    "Cache-Control": "no-store",
    "X-Content-Type-Options": "nosniff",
  });
  response.end(body);
}

async function readJsonBody(request) {
  const chunks = [];
  let size = 0;
  for await (const chunk of request) {
    size += chunk.length;
    if (size > 64 * 1024) throw new Error("요청이 너무 큽니다.");
    chunks.push(chunk);
  }
  if (chunks.length === 0) return {};
  try {
    return JSON.parse(Buffer.concat(chunks).toString("utf8"));
  } catch {
    const error = new Error("요청 본문이 올바른 JSON이 아닙니다.");
    error.httpStatus = 400;
    throw error;
  }
}

function parseWebPort() {
  const index = process.argv.indexOf("--port");
  if (index < 0) return 4317;
  const port = Number.parseInt(process.argv[index + 1], 10);
  if (!Number.isInteger(port) || port < 1024 || port > 65535) {
    throw new Error("--port에는 1024~65535 사이의 포트를 지정하세요.");
  }
  return port;
}

async function startWebServer(apiKey) {
  await scanProjectIndex(true);
  await buildProjectContext();
  await loadPersistentSessions().catch((error) => console.error(`[세션 복원 실패] ${error.message}`));
  await loadMemory().catch((error) => console.error(`[장기 기억 복원 실패] ${error.message}`));
  await refreshModelCatalog(apiKey, true);
  const port = parseWebPort();
  const host = "127.0.0.1";
  const url = `http://${host}:${port}`;
  const browserToken = randomUUID();
  const htmlTemplate = await readFile(path.join(scriptDir, "web.html"), "utf8");
  const html = htmlTemplate.replace("__API_DEV_TOKEN__", JSON.stringify(browserToken));
  if (webSessions.size === 0) {
    createSessionRecord();
    await persistSessions();
  }
  // Keeps the change log current between requests so edits made in Unity or an IDE are caught.
  const indexTimer = setInterval(() => { scanProjectIndex().catch(() => {}); }, 20_000);
  indexTimer.unref();

  const server = createServer(async (request, response) => {
    const requestUrl = new URL(request.url || "/", url);
    const allowedHosts = new Set([`${host}:${port}`, `localhost:${port}`]);
    if (!allowedHosts.has(request.headers.host || "")) {
      sendJson(response, 403, { error: "허용되지 않은 Host입니다." });
      return;
    }

    if (request.method === "GET" && requestUrl.pathname === "/health") {
      sendJson(response, 200, { ok: true });
      return;
    }
    if (request.method === "GET" && requestUrl.pathname === "/favicon.ico") {
      response.writeHead(204);
      response.end();
      return;
    }
    if (request.method === "GET" && requestUrl.pathname === "/") {
      response.writeHead(200, {
        "Content-Type": "text/html; charset=utf-8",
        "Content-Length": Buffer.byteLength(html),
        "Cache-Control": "no-store",
        "Content-Security-Policy": "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; connect-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'none'",
        "Referrer-Policy": "no-referrer",
        "X-Content-Type-Options": "nosniff",
        "X-Frame-Options": "DENY",
      });
      response.end(html);
      return;
    }

    const origin = request.headers.origin;
    const token = request.headers["x-api-dev-token"];
    if ((origin && origin !== url) || token !== browserToken) {
      sendJson(response, 403, { error: "브라우저 세션 인증에 실패했습니다." });
      return;
    }

    try {
      if (request.method === "GET" && requestUrl.pathname === "/api/models") {
        await refreshModelCatalog(apiKey, requestUrl.searchParams.get("refresh") === "1");
        sendJson(response, 200, { ...modelCatalog, default_model: model, efforts: REASONING_EFFORTS, default_effort: reasoningEffort });
        return;
      }
      if (request.method === "GET" && requestUrl.pathname === "/api/project") {
        await scanProjectIndex();
        sendJson(response, 200, projectInfo());
        return;
      }
      const memoryDeleteMatch = requestUrl.pathname.match(/^\/api\/memory\/([0-9a-f]{8})$/i);
      if (request.method === "DELETE" && memoryDeleteMatch) {
        await deleteMemory(memoryDeleteMatch[1]);
        sendJson(response, 200, projectInfo());
        return;
      }
      if (request.method === "POST" && requestUrl.pathname === "/api/memory") {
        const body = await readJsonBody(request);
        const kind = ["decision", "summary", "note"].includes(body.kind) ? body.kind : "note";
        await addMemory(kind, body.text, "사용자");
        sendJson(response, 201, projectInfo());
        return;
      }
      if (request.method === "GET" && requestUrl.pathname === "/api/sessions") {
        const sessions = [...webSessions.values()]
          .sort((a, b) => b.updatedAt.localeCompare(a.updatedAt))
          .map(webSessionSummary);
        sendJson(response, 200, { sessions, active_requests: activeWebRequests, totals: usageOverview() });
        return;
      }
      if (request.method === "POST" && requestUrl.pathname === "/api/sessions") {
        const session = createSessionRecord();
        await persistSessions();
        sendJson(response, 201, webState(session));
        return;
      }
      const sessionDeleteMatch = requestUrl.pathname.match(/^\/api\/sessions\/([0-9a-f-]+)$/i);
      if (request.method === "DELETE" && sessionDeleteMatch) {
        const session = getWebSession(sessionDeleteMatch[1]);
        if (!session) {
          sendJson(response, 404, { error: "세션을 찾을 수 없습니다." });
          return;
        }
        if (session.busy) {
          sendJson(response, 409, { error: "작업 중인 세션은 닫을 수 없습니다." });
          return;
        }
        webSessions.delete(sessionDeleteMatch[1]);
        if (webSessions.size === 0) createSessionRecord();
        await persistSessions();
        sendJson(response, 200, { sessions: [...webSessions.values()].map(webSessionSummary) });
        return;
      }
      if (request.method === "GET" && requestUrl.pathname === "/api/state") {
        const session = getWebSession(requestUrl.searchParams.get("session_id"));
        if (!session) {
          sendJson(response, 404, { error: "세션을 찾을 수 없습니다." });
          return;
        }
        sendJson(response, 200, webState(session));
        return;
      }
      if (request.method === "POST" && requestUrl.pathname === "/api/reset") {
        const body = await readJsonBody(request);
        const session = getWebSession(body.session_id);
        if (!session) {
          sendJson(response, 404, { error: "세션을 찾을 수 없습니다." });
          return;
        }
        if (session.busy) {
          sendJson(response, 409, { error: "작업 중인 세션은 초기화할 수 없습니다." });
          return;
        }
        resetConversation(session);
        session.messageCount = 0;
        session.title = "새 세션";
        session.history = [];
        session.updatedAt = new Date().toISOString();
        await persistSessions();
        sendJson(response, 200, webState(session));
        return;
      }
      if (request.method === "POST" && requestUrl.pathname === "/api/chat") {
        const body = await readJsonBody(request);
        const session = getWebSession(body.session_id);
        if (!session) {
          sendJson(response, 404, { error: "세션을 찾을 수 없습니다." });
          return;
        }
        if (session.busy) {
          sendJson(response, 409, { error: "이 세션에서 다른 요청을 처리 중입니다." });
          return;
        }
        const message = typeof body.message === "string" ? body.message.trim() : "";
        if (!message || message.length > 20_000) {
          sendJson(response, 400, { error: "1~20,000자의 요청을 입력하세요." });
          return;
        }
        const selected = resolveModelId(body.model) || session.model;
        if (!isAllowedModel(selected) && selected !== session.model) {
          sendJson(response, 400, { error: `지원하지 않는 모델입니다: ${selected}` });
          return;
        }
        if (body.effort !== undefined && !REASONING_EFFORTS.includes(body.effort)) {
          sendJson(response, 400, { error: "추론 강도는 low, medium, high 중 하나입니다." });
          return;
        }
        if (session.model !== selected) {
          session.model = selected;
          resetConversation(session);
        }
        if (body.effort) session.reasoningEffort = body.effort;
        session.busy = true;
        activeWebRequests += 1;
        session.history.push({ role: "user", text: message, tools: [] });
        await persistSessions();
        try {
          const result = await runAgentTurn(apiKey, message, { silent: true, session });
          if (session.messageCount === 0) {
            const compact = message.replace(/\s+/g, " ");
            session.title = compact.length > 28 ? `${compact.slice(0, 28)}…` : compact;
          }
          session.messageCount += 1;
          await ensureWorkSummary(result, message, session);
          session.history.push({
            role: "assistant",
            text: result.text,
            tools: result.tools || [],
            meta: { model: result.model, effort: result.reasoning_effort, usage: result.usage, changed_files: result.changed_files },
          });
          session.updatedAt = new Date().toISOString();
          await persistSessions();
          sendJson(response, 200, {
            ...result,
            ...contextView(session),
            session_usage: session.usage,
            totals: usageOverview(),
            session: webSessionSummary(session),
          });
        } catch (error) {
          session.history.push({ role: "assistant", text: `오류: ${error.message}`, tools: [] });
          session.updatedAt = new Date().toISOString();
          await persistSessions().catch(() => {});
          throw error;
        } finally {
          session.busy = false;
          activeWebRequests -= 1;
        }
        return;
      }
      sendJson(response, 404, { error: "찾을 수 없는 경로입니다." });
    } catch (error) {
      console.error(`[웹 요청 오류] ${error.message}`);
      if (!response.headersSent) sendJson(response, error.httpStatus || 500, { error: error.message });
    }
  });

  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(port, host, resolve);
  });
  console.log(APP_NAME);
  console.log(`Project: ${projectRoot}`);
  console.log(`URL: ${url}`);
  console.log(`Model: ${model} / reasoning: ${reasoningEffort} / scope: ${writeScope}`);
  console.log(`Index: ${fileIndex.size} files · models: ${modelCatalog.models.length} (${modelCatalog.source}${modelCatalog.error ? `, 목록 조회 실패: ${modelCatalog.error}` : ""})`);
  console.log("이 창을 닫으면 브라우저 채팅도 종료됩니다.");

  if (!process.argv.includes("--no-open")) {
    const opener = spawn("cmd.exe", ["/c", "start", "", url], {
      detached: true,
      stdio: "ignore",
      windowsHide: true,
    });
    opener.unref();
  }

  await new Promise((resolve) => {
    const close = () => server.close(resolve);
    process.once("SIGINT", close);
    process.once("SIGTERM", close);
  });
  await rm(backupRoot, { recursive: true, force: true }).catch(() => {});
}

// Runs one read-only tool without the API, for checking the tool layer: --tool <name> '<json args>'
async function runToolDirectly() {
  const index = process.argv.indexOf("--tool");
  const name = process.argv[index + 1];
  const allowed = new Set(["list_files", "read_file", "search_text", "git_diff", "unity_status", "unity_scene_state",
    "unity_hierarchy", "unity_find_gameobjects", "unity_component_fields", "unity_selection", "project_context", "relevant_files"]);
  if (!allowed.has(name)) throw new Error(`--tool에는 읽기 전용 도구만 지정할 수 있습니다: ${[...allowed].join(", ")}`);
  const args = JSON.parse(process.argv[index + 2] || "{}");
  await scanProjectIndex(true);
  let result;
  if (name === "project_context") result = await buildProjectContext();
  else if (name === "relevant_files") result = await selectRelevantFiles(args.message || "");
  else result = await invokeTool(name, args, cliSession);
  console.log(typeof result === "string" ? result : JSON.stringify(result, null, 2));
}

async function main() {
  if (process.argv.includes("--tool")) {
    await runToolDirectly();
    return;
  }
  const apiKey = await loadApiKey();
  if (process.argv.includes("--web")) {
    await startWebServer(apiKey);
    return;
  }
  await loadMemory().catch((error) => console.error(`[장기 기억 복원 실패] ${error.message}`));
  await scanProjectIndex(true);
  const oneShotIndex = process.argv.indexOf("--prompt");
  if (oneShotIndex >= 0) {
    const prompt = process.argv.slice(oneShotIndex + 1).join(" ").trim();
    if (!prompt) throw new Error("--prompt 뒤에 작업 내용을 입력하세요.");
    await runAgentTurn(apiKey, prompt);
    return;
  }

  console.log(`${APP_NAME} CLI`);
  console.log(`Project: ${projectRoot}`);
  console.log(`Model: ${cliSession.model} / reasoning: ${cliSession.reasoningEffort}`);
  console.log(`Write scope: ${writeScope}`);
  console.log("API Platform 크레딧을 사용합니다. /help로 명령을 확인하세요.");

  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  try {
    while (true) {
      const input = (await rl.question("\nunity-game-jam> ")).trim();
      if (!input) continue;
      if (input.startsWith("/")) {
        const handled = await handleSlashCommand(input, apiKey);
        if (handled === "exit") break;
        continue;
      }
      try {
        const result = await runAgentTurn(apiKey, input);
        await ensureWorkSummary(result, input, cliSession);
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
