#!/usr/bin/env node
/**
 * Cổng chất lượng của AI-Native SDLC — "kiểm thử tự trị".
 *
 *   npm run sdlc:verify                # scope-aware: chỉ chạy cho workspace đã đổi
 *   npm run sdlc:verify -- --all       # chạy mọi workspace
 *   npm run sdlc:verify -- --e2e       # ép chạy e2e (thường cần dịch vụ ngoài: DB, queue…)
 *   npm run sdlc:verify -- --since main  # gộp cả thay đổi so với nhánh main
 *
 * Kết quả ghi vào .brain/sdlc-state.json để hook chặn commit đọc lại.
 * Xem AGENTS.md §4 (Definition of Done).
 */
import { execSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import {
  WORKSPACES, E2E_GATES, E2E_TRIGGERS, changedCodeFiles, fingerprint, scopeOf, needsE2E,
  readState, writeState, headSha, STATE_PATH, ROOT,
} from './lib.mjs';
const argv = process.argv.slice(2);
const flag = (n) => argv.includes(`--${n}`);
const opt = (n) => {
  const i = argv.indexOf(`--${n}`);
  return i !== -1 ? argv[i + 1] : undefined;
};

const RUN_ALL = flag('all');
const FORCE_E2E = flag('e2e');
const SINCE = opt('since');

const c = {
  dim: (s) => `\x1b[2m${s}\x1b[0m`,
  red: (s) => `\x1b[31m${s}\x1b[0m`,
  green: (s) => `\x1b[32m${s}\x1b[0m`,
  yellow: (s) => `\x1b[33m${s}\x1b[0m`,
  bold: (s) => `\x1b[1m${s}\x1b[0m`,
};

const files = changedCodeFiles(ROOT, SINCE);
const scope = RUN_ALL ? Object.keys(WORKSPACES) : scopeOf(files);
const wantE2E = FORCE_E2E || needsE2E(files);

console.log(c.bold('\n🔍 SDLC Verify'));
console.log(`   File mã đã đổi : ${files.length}`);
console.log(`   Phạm vi        : ${scope.length ? scope.join(', ') : c.dim('(không có)')}`);
console.log(`   E2E            : ${wantE2E ? 'có' : 'không'}${FORCE_E2E ? c.dim(' (ép bằng --e2e)') : ''}\n`);

if (!scope.length) {
  console.log(files.length
    ? c.yellow(`⚠️  ${files.length} file đã đổi nhưng không nằm trong workspace nào có cổng.`)
    : c.yellow('⚠️  Không có thay đổi mã nguồn — không có cổng nào để chạy.'));
  if (files.length) {
    console.log(c.dim(`   ${files.slice(0, 8).join(', ')}${files.length > 8 ? ` … (+${files.length - 8})` : ''}`));
  }
  console.log(c.dim('   Dùng --all để chạy toàn bộ, hoặc --since main nếu đã commit.\n'));
  writeState(ROOT, {
    ...readState(ROOT),
    lastVerify: {
      at: new Date().toISOString(),
      ok: true,
      head: headSha(ROOT),
      fingerprint: fingerprint(ROOT, files),
      scope: [],
      e2e: false,
      gates: [],
      note: 'không có thay đổi mã nguồn',
    },
  });
  process.exit(0);
}

const results = [];
let failed = false;

for (const ws of scope) {
  const dir = join(ROOT, ws);
  if (!existsSync(join(dir, 'package.json'))) {
    console.log(c.yellow(`⚠️  Bỏ qua ${ws}: không có package.json`));
    continue;
  }

  const gates = { ...WORKSPACES[ws] };
  if (wantE2E && E2E_GATES[ws]) gates.e2e = E2E_GATES[ws];

  for (const [gate, cmd] of Object.entries(gates)) {
    if (failed) {
      results.push({ ws, gate, cmd, ok: null, skipped: true });
      continue;
    }
    process.stdout.write(`▶ ${c.bold(`${ws}:${gate}`)} ${c.dim(cmd)}\n`);
    const t0 = Date.now();
    try {
      execSync(cmd, { cwd: dir, stdio: 'inherit' });
      const ms = Date.now() - t0;
      results.push({ ws, gate, cmd, ok: true, ms });
      console.log(c.green(`✔ ${ws}:${gate}`) + c.dim(` (${(ms / 1000).toFixed(1)}s)\n`));
    } catch {
      const ms = Date.now() - t0;
      results.push({ ws, gate, cmd, ok: false, ms });
      console.log(c.red(`✘ ${ws}:${gate} THẤT BẠI`) + c.dim(` (${(ms / 1000).toFixed(1)}s)\n`));
      failed = true;
    }
  }
}

console.log(c.bold('─'.repeat(52)));
for (const r of results) {
  const mark = r.skipped ? c.dim('–') : r.ok ? c.green('✔') : c.red('✘');
  const tail = r.skipped ? c.dim(' bỏ qua (đã có cổng đỏ trước đó)') : '';
  console.log(`  ${mark} ${r.ws}:${r.gate}${tail}`);
}
console.log(c.bold('─'.repeat(52)));

writeState(ROOT, {
  ...readState(ROOT),
  lastVerify: {
    at: new Date().toISOString(),
    ok: !failed,
    head: headSha(ROOT),
    fingerprint: fingerprint(ROOT, files),
    scope,
    e2e: wantE2E,
    gates: results,
  },
});

if (failed) {
  console.log(c.red('\n❌ Cổng chất lượng ĐỎ — chưa đạt Definition of Done (AGENTS.md §4).'));
  console.log(c.dim('   Sửa nguyên nhân gốc. Không nới lint rule, không skip test, không nới guard.\n'));
  process.exit(1);
}

console.log(c.green('\n✅ Toàn bộ cổng XANH.'));
if (!wantE2E && Object.keys(E2E_GATES).length) {
  console.log(c.dim(`   (Chưa chạy e2e — thay đổi này không chạm: ${E2E_TRIGGERS.join(', ') || '(chưa khai báo e2eTriggers)'})`));
}
console.log(c.dim(`   Đã ghi ${STATE_PATH}. Bước tiếp: thu thập bằng chứng trực quan → /sdlc:verify\n`));
