#!/usr/bin/env node
/**
 * PostToolUse hook (Edit|Write|MultiEdit) — ghi nhận file vừa bị Agent sửa.
 *
 * Dùng để: (1) hook Stop biết nên nhắc gì, (2) người điều phối truy vết được Agent
 * đã chạm những đâu trong phiên. Chạy im lặng, không bao giờ chặn.
 */
import { readState, writeState, isCodeFile } from '../lib.mjs';
import { relative, isAbsolute } from 'node:path';

const ROOT = process.env.CLAUDE_PROJECT_DIR || process.cwd();
const MAX = 200;

let input = '';
process.stdin.on('data', (d) => { input += d; });
process.stdin.on('end', () => {
  try {
    const payload = JSON.parse(input || '{}');
    const raw = payload?.tool_response?.filePath || payload?.tool_input?.file_path;
    if (!raw) process.exit(0);

    let p = isAbsolute(raw) ? relative(ROOT, raw) : raw;
    p = p.split('\\').join('/');
    if (p.startsWith('..')) process.exit(0); // ngoài repo

    const state = readState(ROOT);
    const touched = state.touched || [];
    const entry = { file: p, at: new Date().toISOString(), code: isCodeFile(p) };

    const kept = touched.filter((t) => t.file !== p);
    kept.push(entry);

    writeState(ROOT, { ...state, touched: kept.slice(-MAX) });
  } catch {
    // Hook theo dõi không bao giờ được làm hỏng một lượt làm việc.
  }
  process.exit(0);
});
