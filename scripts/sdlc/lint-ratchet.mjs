#!/usr/bin/env node
/**
 * Cổng lint kiểu RATCHET (bánh cóc).
 *
 * Dự án có sẵn (brownfield) gần như luôn đã mang một đống lỗi eslint tồn đọng. Một cổng
 * "lint phải sạch tuyệt đối" sẽ đỏ ngay từ ngày đầu, và hệ quả duy nhất là mọi người đặt
 * SDLC_SKIP_VERIFY_GATE=1 — cổng chặn mất hết tác dụng.
 *
 * Nên cổng này so sánh theo TỪNG FILE với baseline:
 *   - file có số lỗi TĂNG        → đỏ
 *   - file mới có lỗi            → đỏ
 *   - file có số lỗi GIẢM        → xanh, và nhắc cập nhật baseline
 *
 * So sánh theo từng file (không phải tổng) để không thể "sửa 5 lỗi ở A, thêm 5 lỗi ở B".
 *
 *   node scripts/sdlc/lint-ratchet.mjs <workspace|--all> [--update-baseline]
 */
import { execSync, execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { ROOT, LINT_COMMANDS } from './lib.mjs';

const ws = process.argv[2];
const UPDATE = process.argv.includes('--update-baseline');
const BASELINE = join(ROOT, 'docs', 'evals', 'lint-baseline.json');

if (!ws) {
  console.error('Cần tên workspace. VD: node scripts/sdlc/lint-ratchet.mjs backend');
  process.exit(2);
}

// `--all` = chạy lần lượt cho mọi workspace có khai báo lintReportCommand.
// Dùng cho `npm run sdlc:lint-baseline`, để script npm không phải liệt kê tên workspace.
if (ws === '--all') {
  let failed = 0;
  for (const w of Object.keys(LINT_COMMANDS)) {
    try {
      execFileSync(process.execPath, [fileURLToPath(import.meta.url), w, ...process.argv.slice(3)],
        { stdio: 'inherit' });
    } catch {
      failed = 1;
    }
  }
  process.exit(failed);
}

const dir = join(ROOT, ws);
const CMD = LINT_COMMANDS[ws];
if (!CMD) {
  console.log(`(${ws} không khai báo lintReportCommand trong sdlc.config.json — bỏ qua cổng lint)`);
  process.exit(0);
}

let raw;
try {
  raw = execSync(CMD, { cwd: dir, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, stdio: ['ignore', 'pipe', 'pipe'] });
} catch (e) {
  // eslint thoát khác 0 khi có lỗi — đó là trường hợp bình thường, stdout vẫn có JSON.
  raw = e.stdout ?? '';
  if (!raw.trim()) {
    console.error(`Không chạy được lint cho ${ws}:\n${e.stderr ?? e.message}`);
    process.exit(2);
  }
}

// npm chèn dòng log của nó trước JSON; cắt từ dấu '[' đầu tiên.
const start = raw.indexOf('[');
let report;
try {
  report = JSON.parse(raw.slice(start));
} catch {
  console.error(`Không đọc được báo cáo eslint JSON của ${ws}.`);
  process.exit(2);
}

const current = {};
for (const f of report) {
  if (!f.errorCount) continue;
  current[relative(ROOT, f.filePath).split('\\').join('/')] = f.errorCount;
}

const all = existsSync(BASELINE) ? JSON.parse(readFileSync(BASELINE, 'utf8')) : {};

if (UPDATE) {
  all[ws] = current;
  all._comment = 'Số lỗi eslint tồn đọng theo từng file. Đây là NỢ KỸ THUẬT, không phải chuẩn mực. '
    + 'Con số chỉ được PHÉP GIẢM. Sửa bớt lỗi rồi chạy: npm run sdlc:lint-baseline';
  mkdirSync(join(ROOT, 'docs', 'evals'), { recursive: true });
  writeFileSync(BASELINE, `${JSON.stringify(all, null, 2)}\n`, 'utf8');
  const total = Object.values(current).reduce((a, b) => a + b, 0);
  console.log(`✔ Đã ghi baseline lint cho ${ws}: ${Object.keys(current).length} file, ${total} lỗi.`);
  process.exit(0);
}

const base = all[ws] ?? {};
const regressions = [];
const improvements = [];

for (const [file, n] of Object.entries(current)) {
  const was = base[file] ?? 0;
  if (n > was) regressions.push({ file, was, now: n });
}
for (const [file, was] of Object.entries(base)) {
  const now = current[file] ?? 0;
  if (now < was) improvements.push({ file, was, now });
}

const total = Object.values(current).reduce((a, b) => a + b, 0);
const baseTotal = Object.values(base).reduce((a, b) => a + b, 0);

if (regressions.length) {
  console.error(`\n✘ ${ws}: lint bị trượt lùi ở ${regressions.length} file`);
  for (const r of regressions.slice(0, 20)) {
    console.error(`    ${r.file}: ${r.was} → ${r.now} lỗi (+${r.now - r.was})`);
  }
  if (regressions.length > 20) console.error(`    … và ${regressions.length - 20} file nữa`);
  console.error('\n  Sửa các lỗi mới. Không nới eslint rule, không thêm eslint-disable để lách.');
  console.error(`  (Nợ tồn đọng đã biết: ${baseTotal} lỗi — không tính vào đây.)\n`);
  process.exit(1);
}

console.log(`✔ ${ws}: không có lỗi lint mới (nợ tồn đọng: ${total}/${baseTotal}).`);
if (improvements.length) {
  const fixed = improvements.reduce((a, i) => a + (i.was - i.now), 0);
  console.log(`  🎉 Đã sửa được ${fixed} lỗi ở ${improvements.length} file — chạy \`npm run sdlc:lint-baseline\` để chốt lại.`);
}
