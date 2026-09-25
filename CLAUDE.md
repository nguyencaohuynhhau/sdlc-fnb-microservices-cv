# CLAUDE.md

> **MẪU.** Điền `<…>` cho dự án của bạn. File này là thứ Claude Code đọc đầu tiên ở mỗi
> phiên — giữ nó NGẮN. Chi tiết thuộc về `AGENTS.md`; ở đây chỉ là bản đồ chỉ đường.

Dự án này vận hành theo **AI-Native SDLC** (chuỗi hiện vật `intent.md → spec.md → plan.md`).

## Đọc trước khi làm bất cứ việc gì

1. **[AGENTS.md](AGENTS.md)** — rào cản hành vi, ràng buộc kỹ thuật, chính sách bảo mật,
   Definition of Done. Đây là nguồn sự thật cao nhất.
2. **[docs/AI-NATIVE-SDLC.md](docs/AI-NATIVE-SDLC.md)** — quy trình và cách dùng.
3. **[docs/architecture/STRUCTURE.md](docs/architecture/STRUCTURE.md)** — cấu trúc thư mục,
   đọc để khỏi đi tìm mò.

## Chuỗi hiện vật

Mọi thay đổi mã nguồn đều bắt nguồn từ một thư mục trong `docs/intents/<id>/`:

```
/sdlc:intent   → docs/intents/<id>/intent.md   (phỏng vấn người khởi xướng)
/sdlc:triage   → docs/intents/INDEX.md         (gán nhãn + ưu tiên backlog)
/sdlc:spec     → docs/intents/<id>/spec.md     (đặc tả kỹ thuật)
/sdlc:plan     → docs/intents/<id>/plan.md     (kế hoạch + tự chất vấn)
/sdlc:build    → mã nguồn + CHANGELOG.md       (auto mode, worktree)
/sdlc:verify   → docs/evidence/ + cổng chất lượng   (kiểm thử tự trị)
/sdlc:ship     → PR + rà soát bảo mật
```

**Không viết code khi chưa có `plan.md` đã duyệt.** Nếu người dùng yêu cầu sửa trực tiếp,
hỏi lại: đây là hotfix (bỏ qua chuỗi, ghi lại sau) hay tính năng (chạy `/sdlc:intent` trước)?

## Lệnh hay dùng

```bash
<npm run dev>          # chạy môi trường phát triển
npm run sdlc:verify    # cổng chất lượng (scope-aware theo file đã đổi)
npm run sdlc:verify -- --all --e2e   # chạy toàn bộ, kèm e2e (cần <dịch vụ ngoài>)
npm run sdlc:evals     # bộ đánh giá hồi quy cho chính quy trình
```

Cấu hình cổng nằm ở [sdlc.config.json](sdlc.config.json).
