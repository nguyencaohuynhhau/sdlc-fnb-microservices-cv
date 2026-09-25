/**
 * Eval đặc thù dự án FnB POS. `run.mjs` tự nạp file này.
 * Mỗi eval bắt một kiểu "nới rào cản im lặng" mà lint/test không thấy.
 */
import { readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { ROOT, rel, walk } from './helpers.mjs';

const config = JSON.parse(readFileSync(join(ROOT, 'sdlc.config.json'), 'utf8'));

export default [
  {
    // Thiếu .cs thì cổng chặn commit mù với toàn bộ backend.
    id: 'P01',
    title: 'Cổng chặn commit nhìn thấy mã C# (.cs ∈ codeExtensions)',
    run: () => (config.codeExtensions ?? []).includes('.cs')
      ? { ok: true }
      : { ok: false, detail: 'sdlc.config.json → codeExtensions thiếu ".cs"' },
  },
  {
    // Endpoint public chỉ được nằm ở các file đã duyệt (login, refresh, healthz).
    id: 'P02',
    title: '[AllowAnonymous] / AllowAnonymous() chỉ ở file trong public-endpoints.json',
    run: () => {
      const allowed = JSON.parse(readFileSync(join(ROOT, 'docs/evals/public-endpoints.json'), 'utf8'))
        .allowAnonymousFiles;
      const bad = walk(join(ROOT, 'backend/src'))
        .filter((f) => f.endsWith('.cs') && !/[\/](bin|obj)[\/]/.test(f))
        .filter((f) => /AllowAnonymous/.test(readFileSync(f, 'utf8')))
        .map(rel)
        .filter((f) => !allowed.includes(f));
      return bad.length ? { ok: false, detail: `Endpoint public chưa duyệt: ${bad.join(', ')}` } : { ok: true };
    },
  },
  {
    // Tailwind v4 cấu hình bằng CSS; file config JS là dấu hiệu lệch stack.
    id: 'P03',
    title: 'Không có tailwind.config.* (Tailwind v4 cấu hình trong CSS)',
    run: () => {
      const bad = ['js', 'ts', 'cjs', 'mjs']
        .map((e) => `web/tailwind.config.${e}`)
        .filter((f) => existsSync(join(ROOT, f)));
      return bad.length ? { ok: false, detail: bad.join(', ') } : { ok: true };
    },
  },
];
