/**
 * Tiện ích dùng chung cho các script AI-Native SDLC.
 *
 * `verify.mjs`, `lint-ratchet.mjs` và cả ba hook đều import từ đây, để định nghĩa
 * "file mã nguồn đã đổi" và cách băm chúng là DUY NHẤT — nếu hai bên tính khác nhau,
 * cổng chặn commit sẽ sai theo cách rất khó debug.
 *
 * Mọi thứ đặc thù dự án đọc từ `sdlc.config.json` ở gốc repo. File này không hardcode
 * tên workspace, tên framework hay đường dẫn nào.
 */
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { readFileSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * Gốc repo suy từ vị trí của chính file này (scripts/sdlc/lib.mjs), KHÔNG từ cwd —
 * verify.mjs gọi các cổng với cwd đặt tại thư mục workspace.
 */
export const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

export const CONFIG_PATH = 'sdlc.config.json';

function loadConfig() {
  const p = join(ROOT, CONFIG_PATH);
  if (!existsSync(p)) {
    console.error(`\n✘ Không tìm thấy ${CONFIG_PATH} ở gốc repo (${ROOT}).`);
    console.error('  Bộ khung SDLC cần file này để biết workspace nào có cổng nào.');
    console.error('  Copy từ sdlc-base/sdlc.config.json rồi sửa cho dự án.\n');
    process.exit(2);
  }
  try {
    return JSON.parse(readFileSync(p, 'utf8'));
  } catch (e) {
    console.error(`\n✘ ${CONFIG_PATH} không phải JSON hợp lệ: ${e.message}\n`);
    process.exit(2);
  }
}

export const CONFIG = loadConfig();

/** { <workspace>: { <tên cổng>: <lệnh shell> } } — chạy tuần tự, cwd tại thư mục workspace. */
export const WORKSPACES = Object.fromEntries(
  Object.entries(CONFIG.workspaces ?? {}).map(([ws, cfg]) => [ws, cfg.gates ?? {}]),
);

/** { <workspace>: <lệnh e2e> } — chỉ chạy khi chạm E2E_TRIGGERS hoặc ép bằng --e2e. */
export const E2E_GATES = Object.fromEntries(
  Object.entries(CONFIG.workspaces ?? {})
    .filter(([, cfg]) => cfg.e2eGate)
    .map(([ws, cfg]) => [ws, cfg.e2eGate]),
);

/** { <workspace>: <lệnh eslint xuất JSON> } — lint-ratchet.mjs dùng. Phải CHỈ ĐỌC (không --fix). */
export const LINT_COMMANDS = Object.fromEntries(
  Object.entries(CONFIG.workspaces ?? {})
    .filter(([, cfg]) => cfg.lintReportCommand)
    .map(([ws, cfg]) => [ws, cfg.lintReportCommand]),
);

/** Prefix đường dẫn mà khi chạm vào thì BẮT BUỘC chạy e2e. */
export const E2E_TRIGGERS = CONFIG.e2eTriggers ?? [];

/** Test hồi quy không được phép biến mất (eval E05). */
export const REQUIRED_TESTS = CONFIG.requiredTests ?? [];

const CODE_EXT = new Set(CONFIG.codeExtensions ?? []);
const IGNORED_PREFIXES = CONFIG.ignoredPrefixes ?? [];

export const STATE_PATH = '.brain/sdlc-state.json';

function git(args, cwd) {
  // stderr: 'pipe' để git không in thẳng ra terminal ở những chỗ ta đã bắt lỗi (repo mới chưa có HEAD).
  return execFileSync('git', args, { cwd, encoding: 'utf8', maxBuffer: 32 * 1024 * 1024, stdio: ['ignore', 'pipe', 'pipe'] });
}

/** Có phải file mã nguồn đáng gác cổng không? */
export function isCodeFile(path) {
  if (IGNORED_PREFIXES.some((p) => path.startsWith(p))) return false;
  if (path.endsWith('package-lock.json')) return false;
  const dot = path.lastIndexOf('.');
  return dot !== -1 && CODE_EXT.has(path.slice(dot));
}

/**
 * Danh sách file mã nguồn đã đổi trong working tree (đã stage lẫn chưa stage lẫn untracked).
 * `since` (một git ref) sẽ gộp thêm các file đã đổi so với ref đó.
 */
export function changedCodeFiles(cwd, since) {
  const paths = new Set();

  // `-uall` là bắt buộc: mặc định git gộp thư mục untracked thành MỘT dòng ("?? scripts/"),
  // khiến cả một tính năng mới trở nên vô hình với cổng chặn commit.
  for (const line of git(['status', '--porcelain', '-uall'], cwd).split('\n')) {
    if (!line.trim()) continue;
    let p = line.slice(3).trim();
    // Đổi tên hiển thị dạng "cũ -> mới"; chỉ quan tâm file mới.
    const arrow = p.indexOf(' -> ');
    if (arrow !== -1) p = p.slice(arrow + 4);
    if (p.startsWith('"') && p.endsWith('"')) p = JSON.parse(p);
    paths.add(p);
  }

  if (since) {
    try {
      for (const p of git(['diff', '--name-only', `${since}...HEAD`], cwd).split('\n')) {
        if (p.trim()) paths.add(p.trim());
      }
    } catch {
      // ref không tồn tại (repo mới, chưa có origin) — bỏ qua, chỉ dùng working tree.
    }
  }

  return [...paths].filter(isCodeFile).sort();
}

/**
 * Dấu vân tay của trạng thái mã nguồn hiện tại: băm nội dung mọi file mã đã đổi.
 * Ổn định qua `git add` (không phụ thuộc trạng thái stage), nên hook chặn commit
 * chỉ đỏ khi nội dung code thật sự đổi sau lần verify gần nhất.
 */
export function fingerprint(cwd, files) {
  const h = createHash('sha1');
  for (const f of files) {
    const abs = join(cwd, f);
    h.update(f);
    h.update('\0');
    h.update(existsSync(abs) ? createHash('sha1').update(readFileSync(abs)).digest('hex') : 'deleted');
    h.update('\n');
  }
  return h.digest('hex');
}

/**
 * Suy ra các workspace bị ảnh hưởng từ danh sách file.
 * Workspace tên "." = dự án một package ở gốc → mọi file mã đều thuộc về nó.
 */
export function scopeOf(files) {
  return Object.keys(WORKSPACES).filter((ws) =>
    (ws === '.' ? files.length > 0 : files.some((f) => f.startsWith(`${ws}/`))));
}

/** Thay đổi này có bắt buộc chạy e2e không? */
export function needsE2E(files) {
  return files.some((f) => E2E_TRIGGERS.some((t) => f.startsWith(t)));
}

export function readState(cwd) {
  const p = join(cwd, STATE_PATH);
  if (!existsSync(p)) return {};
  try {
    return JSON.parse(readFileSync(p, 'utf8'));
  } catch {
    return {};
  }
}

export function writeState(cwd, state) {
  const p = join(cwd, STATE_PATH);
  mkdirSync(dirname(p), { recursive: true });
  writeFileSync(p, `${JSON.stringify(state, null, 2)}\n`, 'utf8');
}

export function headSha(cwd) {
  try {
    return git(['rev-parse', '--short', 'HEAD'], cwd).trim();
  } catch {
    return 'unknown';
  }
}
