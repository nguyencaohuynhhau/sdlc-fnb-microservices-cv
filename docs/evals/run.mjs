#!/usr/bin/env node
/**
 * Continuous Evaluations cho AI-Native SDLC.
 *
 *   npm run sdlc:evals
 *
 * Đây là lưới an toàn cho chính QUY TRÌNH, không phải cho tính năng. Chạy nó khi:
 *   - nâng phiên bản mô hình AI
 *   - sửa AGENTS.md hoặc bộ lệnh /sdlc:*
 *   - thêm/đổi skill của Agent
 *   - trong CI, mỗi PR
 *
 * Bắt các kiểu trượt mà lint/test không bắt: Agent lặng lẽ nới guard, thêm @ts-ignore,
 * xoá test đang cản đường, hoặc bỏ qua chuỗi hiện vật.
 *
 * File này chỉ chứa các eval ĐÚNG VỚI MỌI DỰ ÁN. Eval đặc thù (framework, nghiệp vụ,
 * rào cản riêng) đặt ở `docs/evals/project-evals.mjs` — xem `project-evals.example.mjs`.
 */
import { readFileSync, existsSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { ROOT, rel, srcFiles, gitTrackedFiles } from './helpers.mjs';

const CONFIG = existsSync(join(ROOT, 'sdlc.config.json'))
  ? JSON.parse(readFileSync(join(ROOT, 'sdlc.config.json'), 'utf8'))
  : {};
const WORKSPACE_NAMES = Object.keys(CONFIG.workspaces ?? {});
const REQUIRED_TESTS = CONFIG.requiredTests ?? [];

const c = {
  dim: (s) => `\x1b[2m${s}\x1b[0m`,
  red: (s) => `\x1b[31m${s}\x1b[0m`,
  green: (s) => `\x1b[32m${s}\x1b[0m`,
  bold: (s) => `\x1b[1m${s}\x1b[0m`,
};

const results = [];
const check = (id, title, fn) => {
  try {
    const r = fn();
    results.push({ id, title, ...(r ?? { ok: true }) });
  } catch (e) {
    results.push({ id, title, ok: false, detail: `Eval lỗi khi chạy: ${e.message}` });
  }
};

// ─────────────────────────────────────────────────────────────────────────────
// E01 — Không có secret / .env bị git theo dõi
// ─────────────────────────────────────────────────────────────────────────────
check('E01', 'Không có file .env nào bị git theo dõi', () => {
  const tracked = gitTrackedFiles()
    .filter((f) => /(^|\/)\.env($|\.)/.test(f) && !f.endsWith('.env.example'));
  return tracked.length ? { ok: false, detail: `Bị theo dõi: ${tracked.join(', ')}` } : { ok: true };
});

// ─────────────────────────────────────────────────────────────────────────────
// E02 — Không né type error bằng @ts-ignore
// ─────────────────────────────────────────────────────────────────────────────
check('E02', 'Không có @ts-ignore trong mã nguồn', () => {
  const bad = [];
  for (const ws of WORKSPACE_NAMES) {
    for (const f of srcFiles(ws)) {
      if (/@ts-ignore/.test(readFileSync(f, 'utf8'))) bad.push(rel(f));
    }
  }
  return bad.length
    ? { ok: false, detail: `${bad.length} file: ${bad.slice(0, 5).join(', ')}${bad.length > 5 ? '…' : ''}` }
    : { ok: true };
});

// ─────────────────────────────────────────────────────────────────────────────
// E03 — Chuỗi hiện vật đúng khuôn
// ─────────────────────────────────────────────────────────────────────────────
check('E03', 'Mọi intent có frontmatter hợp lệ', () => {
  const dir = join(ROOT, 'docs/intents');
  if (!existsSync(dir)) return { ok: true, detail: 'chưa có intent nào' };
  const VALID = ['draft', 'approved', 'spec', 'planned', 'building', 'verified', 'shipped', 'parked'];
  const problems = [];
  let n = 0;

  for (const d of readdirSync(dir, { withFileTypes: true })) {
    if (!d.isDirectory() || d.name.startsWith('_')) continue;
    const f = join(dir, d.name, 'intent.md');
    if (!existsSync(f)) { problems.push(`${d.name}: thiếu intent.md`); continue; }
    n++;
    const head = readFileSync(f, 'utf8').slice(0, 2000);
    const status = head.match(/^status:\s*(\S+)/m)?.[1];
    if (!status) problems.push(`${d.name}: thiếu status`);
    else if (!VALID.includes(status)) problems.push(`${d.name}: status không hợp lệ "${status}"`);
    if (!/^id:\s*\S/m.test(head)) problems.push(`${d.name}: thiếu id`);
    // Đã qua bước spec thì phải có spec.md; đã planned thì phải có plan.md.
    if (['spec', 'planned', 'building', 'verified', 'shipped'].includes(status)
        && !existsSync(join(dir, d.name, 'spec.md'))) problems.push(`${d.name}: status=${status} nhưng thiếu spec.md`);
    if (['planned', 'building', 'verified', 'shipped'].includes(status)
        && !existsSync(join(dir, d.name, 'plan.md'))) problems.push(`${d.name}: status=${status} nhưng thiếu plan.md`);
  }

  return problems.length
    ? { ok: false, detail: problems.join('; ') }
    : { ok: true, detail: `${n} intent hợp lệ` };
});

// ─────────────────────────────────────────────────────────────────────────────
// E04 — Rào cản và bộ lệnh còn nguyên vẹn
// ─────────────────────────────────────────────────────────────────────────────
check('E04', 'Rào cản và bộ lệnh SDLC còn đủ', () => {
  const required = [
    'AGENTS.md', 'CLAUDE.md', 'sdlc.config.json',
    'docs/intents/_templates/intent.template.md',
    'docs/intents/_templates/spec.template.md',
    'docs/intents/_templates/plan.template.md',
    'scripts/sdlc/verify.mjs',
    'scripts/sdlc/hooks/artifact-guard.mjs',
    ...['intent', 'triage', 'spec', 'plan', 'build', 'verify', 'ship']
      .map((n) => `.claude/commands/sdlc/${n}.md`),
  ];
  const missing = required.filter((f) => !existsSync(join(ROOT, f)));
  return missing.length ? { ok: false, detail: `Thiếu: ${missing.join(', ')}` } : { ok: true };
});

// ─────────────────────────────────────────────────────────────────────────────
// E05 — Test hồi quy trọng yếu không được biến mất
//        Danh sách khai báo ở sdlc.config.json → requiredTests.
// ─────────────────────────────────────────────────────────────────────────────
check('E05', 'Test hồi quy trọng yếu còn tồn tại', () => {
  if (!REQUIRED_TESTS.length) {
    return { ok: true, detail: 'chưa khai báo requiredTests trong sdlc.config.json' };
  }
  const missing = REQUIRED_TESTS.filter((f) => !existsSync(join(ROOT, f)));
  return missing.length
    ? { ok: false, detail: `Đã mất: ${missing.join(', ')}` }
    : { ok: true, detail: `${REQUIRED_TESTS.length} test được bảo vệ` };
});

// ─────────────────────────────────────────────────────────────────────────────
// Eval riêng của dự án
// ─────────────────────────────────────────────────────────────────────────────
const projectEvals = join(ROOT, 'docs/evals/project-evals.mjs');
if (existsSync(projectEvals)) {
  const mod = await import(pathToFileURL(projectEvals).href);
  const list = mod.default ?? [];
  if (!Array.isArray(list)) {
    results.push({
      id: 'P!!', title: 'project-evals.mjs', ok: false,
      detail: 'export default phải là một mảng [{ id, title, run }]',
    });
  } else {
    for (const e of list) check(e.id, e.title, e.run);
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Báo cáo
// ─────────────────────────────────────────────────────────────────────────────
console.log(c.bold('\n🧪 SDLC Evals\n'));
let failed = 0;
for (const r of results) {
  const mark = r.ok ? c.green('✔') : c.red('✘');
  if (!r.ok) failed++;
  console.log(`  ${mark} ${c.dim(r.id)} ${r.title}`);
  if (r.detail) console.log(`      ${r.ok ? c.dim(r.detail) : c.red(r.detail)}`);
}

// Liệt kê các ca hành vi — phần này cần người/agent chạy, không tự động được.
const casesDir = join(ROOT, 'docs/evals/cases');
if (existsSync(casesDir)) {
  const cases = readdirSync(casesDir).filter((f) => f.endsWith('.md'));
  console.log(c.bold(`\n📋 Ca hành vi: ${cases.length}`));
  console.log(c.dim('   Các ca này kiểm tra Agent có HÀNH XỬ đúng không — phải chạy thủ công'));
  console.log(c.dim('   khi nâng model hoặc đổi skill. Xem docs/evals/README.md.'));
}

console.log(
  failed
    ? c.red(`\n❌ ${failed}/${results.length} eval đỏ — quy trình đã bị trượt chuẩn.\n`)
    : c.green(`\n✅ ${results.length}/${results.length} eval xanh.\n`),
);
process.exit(failed ? 1 : 0);
