# sdlc-base — Bộ khung AI-Native SDLC

Bộ khung dùng lại được cho quy trình **AI-Native SDLC**: chuỗi hiện vật
`intent.md → spec.md → plan.md → code`, ba chốt chặn con người, cổng chất lượng tự động và
một hook chặn commit khi cổng chưa xanh.

Đây là **khung rỗng có mẫu**, không phải thư viện. Bạn copy nó vào dự án rồi điền vào.

---

## Cài vào một dự án

```bash
# 1. Copy bộ khung (đứng ở gốc repo đích)
cp -r /đường/dẫn/sdlc-base/{.claude,.github,scripts,docs,AGENTS.md,CLAUDE.md,sdlc.config.json,CHANGELOG.md} .

# 2. Thêm script vào package.json ở gốc
#    "sdlc:verify":        "node scripts/sdlc/verify.mjs"
#    "sdlc:evals":         "node docs/evals/run.mjs"
#    "sdlc:lint-baseline": "node scripts/sdlc/lint-ratchet.mjs --all --update-baseline"

# 3. Thêm .brain/ vào .gitignore

# 4. Khai báo workspace và cổng
$EDITOR sdlc.config.json

# 5. Chốt mức nợ lint hiện tại làm baseline
npm run sdlc:lint-baseline

# 6. Kiểm tra bộ khung đã chạy
npm run sdlc:evals
npm run sdlc:verify
```

Repo đích đã có `.claude/settings.json` riêng thì **merge** phần `hooks`, đừng ghi đè.

---

## Phải điền những gì

| File | Việc phải làm | Bắt buộc |
|------|---------------|----------|
| `sdlc.config.json` | Khai báo workspace, cổng, `e2eTriggers`, `requiredTests` | ✅ |
| `AGENTS.md` | Điền mọi chỗ `<…>`: bối cảnh, ràng buộc kỹ thuật, chính sách bảo mật, DoD | ✅ |
| `CLAUDE.md` | Điền lệnh dev và dịch vụ ngoài | ✅ |
| `docs/architecture/STRUCTURE.md` | Cấu trúc thư mục thật | ✅ |
| `docs/evals/project-evals.mjs` | Chép từ `project-evals.example.mjs`, viết eval cho rào cản thật | nên có |
| `.github/workflows/ci.yml` | Sửa `matrix.workspace`, bỏ comment khối `services` nếu cần e2e | nếu dùng GH Actions |
| `docs/evals/cases/*.md` | Thay 2 ca mẫu bằng ca thật của dự án | dần dần |
| `docs/AI-NATIVE-SDLC.md` | Hầu như dùng nguyên; chỉ sửa chỗ `<…>` | – |

Chưa điền `AGENTS.md` thì bộ khung vẫn chạy — nhưng nó chỉ chặn được những gì bạn đã viết ra.
Rào cản không viết ra là rào cản không tồn tại.

---

## Có gì trong này

```
.claude/
├── settings.json              # 3 hook: track-changes, artifact-guard, next-step
└── commands/sdlc/*.md         # 7 lệnh: intent, triage, spec, plan, build, verify, ship
scripts/sdlc/
├── lib.mjs                    # đọc sdlc.config.json; định nghĩa "file mã đã đổi" + fingerprint
├── verify.mjs                 # cổng chất lượng scope-aware
├── lint-ratchet.mjs           # cổng lint kiểu bánh cóc (chỉ đỏ khi có lỗi MỚI)
└── hooks/                     # 3 hook nói trên
docs/
├── AI-NATIVE-SDLC.md          # quy trình: sơ đồ, bộ lệnh, vì sao thiết kế như vậy
├── intents/                   # chuỗi hiện vật (thư mục <NN>-<YYMMDD>-<slug>/) + 3 template + INDEX.md
├── evals/                     # eval phổ quát + chỗ cắm eval riêng + ca hành vi
├── evidence/                  # ảnh chụp và log (lưu theo thư mục <NN>-<YYMMDD>-<slug>/)
└── architecture/STRUCTURE.md  # bản đồ thư mục
AGENTS.md                      # rào cản hành vi — nguồn sự thật cao nhất
sdlc.config.json               # điểm cấu hình DUY NHẤT của phần project-specific
```

Mọi thứ đặc thù dự án nằm trong `sdlc.config.json` và những chỗ `<…>` trong tài liệu.
Các script **không hardcode** tên workspace, framework hay đường dẫn nào.

---

## Hai cơ chế đáng hiểu trước khi dùng

**Cổng chặn commit dùng dấu vân tay nội dung.** Hook `PreToolUse` băm nội dung mọi file mã đã
đổi. Sửa thêm một dòng sau khi verify là vân tay lệch → phải verify lại. Không lách được bằng
`git add`. Lối thoát khẩn cho hotfix: `SDLC_SKIP_VERIFY_GATE=1`, và phải giải thích trong PR.

**Mọi cổng đều là ratchet, không phải tường.** Dự án brownfield đã vi phạm sẵn ở hàng trăm chỗ.
Cổng cấm tuyệt đối sẽ đỏ ngay ngày đầu và hệ quả duy nhất là mọi người tắt nó đi. Nên: chốt mức
vi phạm hiện tại làm baseline, chỉ đỏ khi có vi phạm **mới**, và baseline chỉ được phép co lại.
`lint-baseline.json` làm vậy cho lint; mẫu P03 trong `project-evals.example.mjs` làm vậy cho guard.

---

## Dùng hàng ngày

```
/sdlc:intent   <mô tả vấn đề bằng lời thường>
/sdlc:triage   → cập nhật docs/intents/INDEX.md
/sdlc:spec     → spec.md
/sdlc:plan     → plan.md   🧍 người điều phối duyệt, đọc mục "Tự chất vấn" trước
/sdlc:build    → code + test + mục mới trong CHANGELOG.md
/sdlc:verify   → cổng xanh + docs/evidence/
/sdlc:ship     → PR
```

Chi tiết: [docs/AI-NATIVE-SDLC.md](docs/AI-NATIVE-SDLC.md).
