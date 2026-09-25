#!/usr/bin/env node
/**
 * PreToolUse hook — chặn `git commit` khi cổng chất lượng chưa xanh và còn tươi.
 *
 * Hiện thực nguyên tắc "kiểm thử tự trị": Agent phải tự hoàn tất kiểm định TRƯỚC khi
 * mã nguồn được đóng gói lại cho con người xem.
 *
 * Chặn khi: có file mã nguồn thay đổi VÀ (chưa từng verify | verify đỏ | code đã đổi sau verify).
 * Cho qua khi: commit chỉ đụng tài liệu, hoặc verify xanh và dấu vân tay còn khớp.
 *
 * Lối thoát khẩn: SDLC_SKIP_VERIFY_GATE=1 (dùng cho hotfix; phải giải thích trong PR).
 */
import { changedCodeFiles, fingerprint, readState } from '../lib.mjs';

const ROOT = process.env.CLAUDE_PROJECT_DIR || process.cwd();

function allow() {
  process.exit(0);
}

function deny(reason) {
  console.log(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: 'PreToolUse',
      permissionDecision: 'deny',
      permissionDecisionReason: reason,
    },
  }));
  process.exit(0);
}

let input = '';
process.stdin.on('data', (d) => { input += d; });
process.stdin.on('end', () => {
  if (process.env.SDLC_SKIP_VERIFY_GATE === '1') allow();

  let cmd = '';
  try {
    cmd = JSON.parse(input || '{}')?.tool_input?.command ?? '';
  } catch {
    allow();
  }

  // Chỉ quan tâm lệnh tạo commit thật sự.
  if (!/\bgit\b[^|;&]*\bcommit\b/.test(cmd)) allow();
  if (/--dry-run|--amend\s+--no-edit/.test(cmd)) allow();

  let files;
  try {
    files = changedCodeFiles(ROOT);
  } catch {
    allow(); // không phải git repo, hoặc git lỗi — không chặn oan
  }

  if (files.length === 0) allow(); // commit chỉ đụng tài liệu/hiện vật

  const v = readState(ROOT).lastVerify;

  if (!v) {
    deny(
      'Cổng SDLC: chưa chạy kiểm thử tự trị cho thay đổi này.\n' +
      `Có ${files.length} file mã nguồn đã đổi nhưng chưa có kết quả verify nào.\n` +
      'Chạy `npm run sdlc:verify` rồi commit lại. (AGENTS.md §4 — Definition of Done)'
    );
  }

  if (!v.ok) {
    const red = (v.gates || []).filter((g) => g.ok === false).map((g) => `${g.ws}:${g.gate}`);
    deny(
      'Cổng SDLC: lần verify gần nhất ĐỎ — không được commit.\n' +
      `Cổng đỏ: ${red.join(', ') || '(không rõ)'}\n` +
      'Sửa nguyên nhân gốc rồi chạy lại `npm run sdlc:verify`. ' +
      'Không nới lint rule, không skip test, không nới guard để lách cổng này.'
    );
  }

  if (v.fingerprint !== fingerprint(ROOT, files)) {
    deny(
      'Cổng SDLC: mã nguồn đã thay đổi sau lần verify gần nhất — kết quả cũ không còn hiệu lực.\n' +
      `Verify lúc: ${v.at} (phạm vi: ${(v.scope || []).join(', ') || 'trống'})\n` +
      'Chạy lại `npm run sdlc:verify` rồi commit.'
    );
  }

  allow();
});
