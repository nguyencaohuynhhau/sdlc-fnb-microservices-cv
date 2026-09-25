#!/usr/bin/env node
/**
 * Stop hook — nhắc bước kế tiếp trong chuỗi hiện vật khi Agent dừng lượt.
 *
 * Im lặng khi không có gì đáng nói (không sửa code, hoặc đã verify xong).
 * Không bao giờ chặn — chỉ hiện một dòng systemMessage.
 */
import { readdirSync, readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { changedCodeFiles, fingerprint, readState } from '../lib.mjs';

const ROOT = process.env.CLAUDE_PROJECT_DIR || process.cwd();

function say(msg) {
  console.log(JSON.stringify({ systemMessage: msg, suppressOutput: true }));
  process.exit(0);
}

function quiet() {
  process.exit(0);
}

/** Đọc `status:` trong frontmatter YAML mà không cần thư viện parser. */
function statusOf(file) {
  try {
    const head = readFileSync(file, 'utf8').slice(0, 2000);
    return head.match(/^status:\s*([a-z]+)\s*$/m)?.[1] ?? null;
  } catch {
    return null;
  }
}

try {
  let files = [];
  try {
    files = changedCodeFiles(ROOT);
  } catch {
    quiet();
  }

  const intentsDir = join(ROOT, 'docs/intents');
  const active = [];
  if (existsSync(intentsDir)) {
    for (const d of readdirSync(intentsDir, { withFileTypes: true })) {
      if (!d.isDirectory() || d.name.startsWith('_')) continue;
      const intent = join(intentsDir, d.name, 'intent.md');
      if (!existsSync(intent)) continue;
      const st = statusOf(intent);
      if (['approved', 'spec', 'planned', 'building'].includes(st)) {
        active.push({ id: d.name, status: st });
      }
    }
  }

  // Có sửa code → nhắc cổng chất lượng.
  if (files.length > 0) {
    const v = readState(ROOT).lastVerify;
    const stale = !v || !v.ok || v.fingerprint !== fingerprint(ROOT, files);
    if (stale) {
      const why = !v ? 'chưa verify lần nào' : !v.ok ? 'verify gần nhất ĐỎ' : 'code đã đổi sau verify';
      say(`🔍 SDLC: ${files.length} file mã đã đổi, ${why} → chạy \`npm run sdlc:verify\` (commit sẽ bị chặn tới khi xanh).`);
    }
    if (active.length) {
      const a = active[0];
      say(`✅ SDLC: cổng xanh. Intent \`${a.id}\` đang ở \`${a.status}\` → thu bằng chứng vào docs/evidence/ rồi \`/sdlc:ship ${a.id}\`.`);
    }
    quiet();
  }

  // Không sửa code — nhắc chốt chặn con người nếu có intent đang chờ.
  const waiting = active.find((a) => a.status === 'approved') || active.find((a) => a.status === 'spec');
  if (waiting) {
    const next = waiting.status === 'approved' ? 'spec' : 'plan';
    say(`📋 SDLC: intent \`${waiting.id}\` ở trạng thái \`${waiting.status}\` → bước kế: \`/sdlc:${next} ${waiting.id}\`.`);
  }
} catch {
  // Hook nhắc việc không bao giờ được làm hỏng một lượt làm việc.
}
quiet();
