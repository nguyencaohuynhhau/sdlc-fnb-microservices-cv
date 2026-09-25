/**
 * MẪU — eval đặc thù dự án.
 *
 * Đổi tên thành `project-evals.mjs` thì `run.mjs` sẽ tự nạp và chạy cùng các eval phổ quát.
 * Xoá hết ví dụ bên dưới, viết eval cho rào cản THẬT của dự án bạn.
 *
 * Mỗi eval là `{ id, title, run }`. `run()` trả `{ ok, detail? }` (hoặc không trả gì = xanh).
 * Đặt id `P01`, `P02`… để không đụng `E01…` của bộ phổ quát.
 *
 * ── Eval tốt trông như thế nào ─────────────────────────────────────────────
 * Eval KHÔNG thay lint và test. Nó bắt đúng một loại lỗi mà hai thứ kia mù:
 * **Agent lặng lẽ nới một rào cản để công việc của nó trôi.**
 *
 * Nguồn eval tốt nhất là những lần đã bị đau: mỗi khi review bắt được Agent mở CORS,
 * bỏ guard, xoá một test khó, hay thêm @ts-ignore — viết một eval để lần sau máy bắt.
 * Mỗi luật trong AGENTS.md mà bạn có thể kiểm bằng grep thì nên có một eval tương ứng;
 * luật không ai kiểm là luật sẽ bị quên.
 */
import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { ROOT, rel, srcFiles } from './helpers.mjs';

const UPDATE = process.argv.includes('--update-baseline');

export default [
  // ── Mẫu 1: chặn cứng một mẫu code nguy hiểm ────────────────────────────────
  {
    id: 'P01',
    title: 'CORS không dùng wildcard origin',
    run: () => {
      const bad = srcFiles('backend')
        .filter((f) => /origin\s*:\s*['"`]\*['"`]/.test(readFileSync(f, 'utf8')))
        .map(rel);
      return bad.length
        ? { ok: false, detail: `origin: '*' xuất hiện ở: ${bad.join(', ')}` }
        : { ok: true };
    },
  },

  // ── Mẫu 2: một cấu hình an toàn không được phép biến mất ───────────────────
  {
    id: 'P02',
    title: 'ValidationPipe giữ whitelist + forbidNonWhitelisted',
    run: () => {
      const main = join(ROOT, 'backend/src/main.ts');
      if (!existsSync(main)) return { ok: false, detail: 'Không tìm thấy backend/src/main.ts' };
      const s = readFileSync(main, 'utf8');
      const missing = [];
      if (!/whitelist\s*:\s*true/.test(s)) missing.push('whitelist: true');
      if (!/forbidNonWhitelisted\s*:\s*true/.test(s)) missing.push('forbidNonWhitelisted: true');
      return missing.length ? { ok: false, detail: `Thiếu: ${missing.join(', ')}` } : { ok: true };
    },
  },

  // ── Mẫu 3: RATCHET — dự án brownfield đã vi phạm sẵn ở N chỗ ───────────────
  // Cấm tuyệt đối thì cổng đỏ ngay ngày đầu và mọi người sẽ học cách lách nó.
  // Nên: chốt danh sách vi phạm hiện có làm baseline, chỉ đỏ khi có vi phạm MỚI.
  // Danh sách chỉ được phép CO LẠI.
  {
    id: 'P03',
    title: 'Controller có route ghi dữ liệu đều được bảo vệ',
    run: () => {
      const BASELINE = join(ROOT, 'docs/evals/public-endpoints.json');
      const allow = existsSync(BASELINE)
        ? JSON.parse(readFileSync(BASELINE, 'utf8')).allow ?? []
        : [];

      const unguarded = srcFiles('backend')
        .filter((f) => f.endsWith('.controller.ts'))
        .filter((f) => {
          const s = readFileSync(f, 'utf8');
          return /@(Post|Put|Patch|Delete)\s*\(/.test(s) && !/@UseGuards\s*\(/.test(s);
        })
        .map(rel);

      if (UPDATE) {
        writeFileSync(BASELINE, `${JSON.stringify({
          _comment: 'Controller có route ghi dữ liệu nhưng CHƯA có @UseGuards. Đây là NỢ KỸ THUẬT, '
            + 'không phải chuẩn mực. Danh sách chỉ được PHÉP CO LẠI. Thêm mục mới = phải có lý do '
            + 'trong spec.md và được người điều phối duyệt.',
          allow: unguarded,
        }, null, 2)}\n`, 'utf8');
        return { ok: true, detail: `Đã ghi baseline: ${unguarded.length} controller` };
      }

      const fresh = unguarded.filter((f) => !allow.includes(f));
      if (fresh.length) {
        return { ok: false, detail: `Controller mới không có @UseGuards: ${fresh.join(', ')}` };
      }
      const fixed = allow.filter((f) => !unguarded.includes(f));
      return {
        ok: true,
        detail: `${allow.length} ngoại lệ đã biết${fixed.length ? `, ${fixed.length} đã được sửa 🎉 (chạy --update-baseline)` : ''}`,
      };
    },
  },

  // ── Mẫu 4: rào cản quyền riêng tư ──────────────────────────────────────────
  {
    id: 'P04',
    title: 'Không console.log dữ liệu định danh khách hàng',
    run: () => {
      const bad = srcFiles('backend').filter((f) =>
        readFileSync(f, 'utf8').split('\n').some((line) =>
          /console\.(log|info|debug)/.test(line)
          && /\b(phone|phoneNumber|address|email|idNumber)\b/.test(line)));
      return bad.length ? { ok: false, detail: bad.map(rel).join(', ') } : { ok: true };
    },
  },

  // ── Mẫu 5: quy ước công cụ (ở đây: Tailwind v4 bỏ file config) ─────────────
  {
    id: 'P05',
    title: 'Không có tailwind.config.js (Tailwind v4)',
    run: () => {
      const bad = ['web']
        .flatMap((ws) => ['tailwind.config.js', 'tailwind.config.ts', 'tailwind.config.cjs']
          .map((n) => join(ROOT, ws, n)))
        .filter(existsSync)
        .map(rel);
      return bad.length ? { ok: false, detail: bad.join(', ') } : { ok: true };
    },
  },
];
