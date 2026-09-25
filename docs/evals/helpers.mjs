/**
 * Tiện ích cho eval. `run.mjs` và `project-evals.mjs` cùng dùng, để một eval của dự án
 * không phải viết lại hàm duyệt cây thư mục.
 */
import { readdirSync, statSync, existsSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { join, relative, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

/** Gốc repo, suy từ vị trí file này (docs/evals/helpers.mjs) — không phụ thuộc cwd. */
export const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

const SKIP_DIRS = ['node_modules', 'dist', '.next', 'build', 'coverage', '.git', 'target', '__pycache__'];

/** Liệt kê mọi file dưới `dir` (đệ quy), bỏ qua thư mục build/vendor. */
export function walk(dir, out = []) {
  if (!existsSync(dir)) return out;
  for (const name of readdirSync(dir)) {
    if (SKIP_DIRS.includes(name)) continue;
    const p = join(dir, name);
    if (statSync(p).isDirectory()) walk(p, out);
    else out.push(p);
  }
  return out;
}

/** Đường dẫn tương đối so với gốc repo, luôn dùng dấu `/` kể cả trên Windows. */
export const rel = (p) => relative(ROOT, p).split('\\').join('/');

/**
 * File mã nguồn của một workspace. Thử `<ws>/src` trước, không có thì quét cả `<ws>`.
 * Workspace "." = dự án một package ở gốc repo.
 */
export function srcFiles(ws, exts = ['.ts', '.tsx']) {
  const base = ws === '.' ? ROOT : join(ROOT, ws);
  const src = join(base, 'src');
  return walk(existsSync(src) ? src : base).filter((f) => exts.some((e) => f.endsWith(e)));
}

/** Danh sách file đang được git theo dõi. */
export function gitTrackedFiles() {
  return execFileSync('git', ['ls-files'], { cwd: ROOT, encoding: 'utf8' })
    .split('\n')
    .filter(Boolean);
}
