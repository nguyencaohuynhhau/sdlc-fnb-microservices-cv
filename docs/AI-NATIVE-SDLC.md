# AI-Native SDLC

> **MẪU.** File này mô tả quy trình và gần như dùng được nguyên xi. Chỗ cần sửa cho dự án
> của bạn được đánh dấu `<…>` — chủ yếu ở §4 (bảng cổng) và §7 (ví dụ).

Quy trình phát triển của dự án này được tổ chức quanh **chuỗi hiện vật**
(`intent.md → spec.md → plan.md`) thay vì quanh các buổi họp và ticket.

Lý do: khi Agent viết code nhanh gấp nhiều lần, **lập trình không còn là điểm nghẽn — quy trình
mới là điểm nghẽn**. Thời gian giờ trôi vào chỗ khác: mô tả yêu cầu thiếu, chuyển giao mất bối
cảnh, chờ QA, phát hiện lỗi bảo mật muộn. Chuỗi hiện vật tấn công đúng những chỗ đó.

---

## 1. Sơ đồ vận hành

```
   Con người: Vấn đề
        │
        ▼
   Agent 1: Khám phá ────────► sub-agent nghiên cứu (Explore)
        │  /sdlc:intent
        ▼
   intent.md  ◄══════════════ 🧍 CHỐT CHẶN 1: người khởi xướng duyệt
        │
        ▼
   Agent 2/3: Đặc tả + Kế hoạch
        │  /sdlc:spec → /sdlc:plan
        ▼
   spec.md, plan.md  ◄═══════ 🧍 CHỐT CHẶN 2: người điều phối duyệt (đọc mục Tự chất vấn)
        │
        ▼
   Agent 4: Xây dựng ─────────► sub-agent worktree 1, worktree 2 (song song)
        │  /sdlc:build  →  mã nguồn + test + check-off plan.md + mục mới trong CHANGELOG.md
        ▼
   Agent 5: Kiểm thử tự trị
        │  /sdlc:verify  →  lint, build, unit, e2e, ảnh chụp màn hình
        ▼
   🧍 Con người rà soát ◄═════ CHỐT CHẶN 3: review logic & kiến trúc
        │
        ▼
   Agent 5: Pull Request  →  Agent 6: CI bảo mật
        │  /sdlc:ship          .github/workflows/ci.yml
        ▼
   Merge  →  vòng lặp mới
```

Ba chốt chặn con người là **cố ý**. Mọi thứ giữa chúng nên tự động hoá tối đa.

---

## 2. Bộ lệnh

| Lệnh | Vào | Ra | Chốt chặn sau đó |
|------|-----|----|------------------|
| `/sdlc:intent` | mô tả bằng lời | `intent.md` (status `draft`) | 🧍 duyệt → `approved` |
| `/sdlc:triage` | mọi intent | `INDEX.md`, nhãn, ưu tiên | – |
| `/sdlc:spec` | `intent.md` + `AGENTS.md` | `spec.md` | – |
| `/sdlc:plan` | `spec.md` | `plan.md` + tự chất vấn | 🧍 duyệt → `planned` |
| `/sdlc:build` | `plan.md` | mã nguồn + test + mục mới trong `CHANGELOG.md` | – |
| `/sdlc:verify` | mã nguồn | cổng xanh + `docs/evidence/` | 🧍 review code |
| `/sdlc:ship` | docs/evidence | commit + PR | 🧍 duyệt PR |

Chi tiết mỗi lệnh nằm trong `.claude/commands/sdlc/*.md`.

---

## 3. Vì sao thiết kế như vậy

**`intent.md` không nhắc tên file.** Người khởi xướng có thể không phải dân kỹ thuật. Bắt họ viết PRD là
rào cản; để họ nói chuyện với Agent thì không. Nhưng file kết quả **phải do họ đọc và sửa** —
đó là chỗ duy nhất trong chuỗi mà "hiểu sai nghiệp vụ" còn rẻ để sửa.

**`spec.md` có mục ánh xạ ngược.** Mỗi tiêu chí chấp nhận phải chỉ ra thành phần kỹ thuật nào
đáp ứng và test nào chứng minh. Tiêu chí không ánh xạ được là dấu hiệu spec đang thiếu, hoặc
tiêu chí mơ hồ — bắt được ở đây rẻ hơn bắt lúc review PR rất nhiều.

**`plan.md` phải tự chứa.** Cửa sổ ngữ cảnh có hạn và Agent mất tập trung trong hội thoại dài.
Bài kiểm tra: đưa `AGENTS.md` + `spec.md` + `plan.md` cho một agent hoàn toàn mới — nó làm được
không? Đây cũng là điều kiện để chạy sub-agent song song trên nhiều git worktree.

**Mục "Tự chất vấn" là phần đáng giá nhất.** Trước khi duyệt plan, Agent phải trả lời:
*"Thay đổi nào có nguy cơ làm hỏng hệ thống hoặc xung đột với tính năng hiện có?"* — và trả lời
bằng cách đi `grep` trong code, không phải bằng cách đoán. Lỗi logic bắt được ở đây tốn 0 dòng code.

**Kiểm thử tự trị đi trước con người.** Agent tự chạy lint, build, unit, e2e, tự thao tác UI và
chụp màn hình **trước khi** ai đó mở PR. Người review dành thời gian cho kiến trúc và đánh đổi
nghiệp vụ — thứ con người giỏi hơn — chứ không phải bắt lỗi cú pháp.

**`CHANGELOG.md` viết lúc build, không viết lúc ship.** Lúc `/sdlc:build` vừa xong, Agent còn
nhớ đã đổi gì và vì sao *không* chọn phương án khác. Đợi tới lúc mở PR thì phần "vì sao" đã bay
mất, còn lại mỗi danh sách file — thứ `git log` vốn đã có. Mục Notes mới là phần đáng giá.

**Evals bảo vệ chính quy trình.** Nâng model hoặc sửa `AGENTS.md` có thể âm thầm làm Agent hành
xử khác đi. `npm run sdlc:evals` bắt các kiểu trượt chuẩn mà lint và test không thấy.

---

## 4. Tầng tự động hoá

### Hooks (`.claude/settings.json`)

| Hook | Khi nào | Làm gì |
|------|---------|--------|
| `PostToolUse` (Edit/Write) | Agent sửa file | Ghi vết vào `.brain/sdlc-state.json` |
| `PreToolUse` (Bash) | trước `git commit` | **Chặn** nếu `sdlc:verify` chưa chạy / đỏ / đã cũ |
| `Stop` | Agent dừng lượt | Nhắc bước kế tiếp trong chuỗi |

Cổng chặn commit dùng **dấu vân tay nội dung**: băm nội dung mọi file mã đã đổi. Sửa thêm một
dòng sau khi verify là dấu vân tay lệch → phải verify lại. Không lách được bằng `git add`.

Lối thoát khẩn cho hotfix: `SDLC_SKIP_VERIFY_GATE=1`. Dùng nó thì phải giải thích trong PR.

### Cổng chất lượng (`npm run sdlc:verify`)

Scope-aware — chỉ chạy cho workspace thật sự có thay đổi. Workspace nào có cổng nào là do
[sdlc.config.json](../sdlc.config.json) quyết định; script không hardcode tên dự án:

```jsonc
"workspaces": {
  "<ws>": {
    "gates": { "lint": "node ../scripts/sdlc/lint-ratchet.mjs <ws>", "build": "npm run build", "test": "npm test" },
    "e2eGate": "npm run test:e2e",              // chỉ chạy khi chạm e2eTriggers
    "lintReportCommand": "npm run lint -- -f json"  // PHẢI chỉ đọc, không --fix
  }
}
```

Dự án chỉ có một `package.json` ở gốc: dùng khoá `"."` làm tên workspace.

```bash
npm run sdlc:verify                    # scope-aware
npm run sdlc:verify -- --all --e2e     # toàn bộ (thường cần dịch vụ ngoài: DB, queue…)
npm run sdlc:verify -- --since main    # gộp cả thay đổi đã commit
npm run sdlc:lint-baseline             # chốt lại mức nợ lint sau khi sửa bớt
```

**Vì sao lint là ratchet.** Dự án brownfield khi mới gắn quy trình này thường đã có sẵn hàng
trăm lỗi eslint. Một cổng đòi lint sạch tuyệt đối sẽ đỏ ngay ngày đầu, và hệ quả duy nhất là
mọi người đặt `SDLC_SKIP_VERIFY_GATE=1` — cổng chặn mất sạch tác dụng. Nên cổng so
số lỗi *theo từng file* với `docs/evals/lint-baseline.json` và chỉ đỏ khi có lỗi **mới**. So theo
từng file chứ không theo tổng, để không thể "sửa 5 lỗi ở A rồi thêm 5 lỗi ở B".

Nợ đó chỉ được phép giảm. Mỗi lần sửa bớt, chạy `npm run sdlc:lint-baseline` để chốt mức thấp hơn.

### CI (`.github/workflows/ci.yml`)

4 cổng: evals → bảo mật (secret scan, `npm audit`) → chất lượng (matrix workspace, cộng dịch
vụ ngoài nếu có e2e) → chuỗi hiện vật (PR phải tham chiếu `docs/intents/<id>/` và cả 3 file phải
tồn tại; `hotfix:`/`revert:`/`chore(deps)` được miễn).

Sửa `matrix.workspace` trong file đó cho khớp `sdlc.config.json`.

---

## 5. Quan hệ với các bộ lệnh khác

AI-Native SDLC là **lớp điều phối**, không phải bộ công cụ thực thi. Nếu nhóm bạn đã có sẵn
một bộ lệnh riêng (nghiên cứu, thiết kế, debug, deploy…), giữ nguyên và gọi nó ở bước tương ứng:

| Bước SDLC | Gọi lại công cụ sẵn có khi |
|-----------|----------------------------|
| `/sdlc:intent` | cần nghiên cứu thị trường/đối thủ |
| `/sdlc:spec` | cần thiết kế DB/API sâu, cần mockup UI |
| `/sdlc:verify` | cổng đỏ, tắc quá 2 vòng → lệnh debug |
| `/sdlc:ship` | cần rà soát bảo mật sâu, cần deploy |

Điều duy nhất SDLC đòi hỏi: **hiện vật chính thức nằm ở `docs/intents/<id>/`**. Công cụ khác
sinh ra output ở đâu cũng được, miễn kết luận được chốt lại vào chuỗi này.

---

## 6. Vai trò con người

Ba vai, có thể cùng một người ở dự án nhỏ:

**Người khởi xướng (Originator).** Đặt bài toán nghiệp vụ, trả lời phỏng vấn, **duyệt `intent.md`**.
Không cần biết kỹ thuật. Trách nhiệm: đảm bảo ta đang giải đúng vấn đề.

**Nhà thiết kế rào cản (Guardrails Designer).** Sở hữu [AGENTS.md](../AGENTS.md), `DESIGN_SYSTEM.md`,
và các eval. Trách nhiệm: Agent không thể đi chệch dù bị thúc. Đây là vai đầu tư một lần, sinh
lợi mãi — mỗi luật viết vào `AGENTS.md` là một lần không phải nhắc lại.

**Nhà điều phối & phê duyệt (Orchestrator & Reviewer).** Chất vấn `plan.md` (đọc mục Tự chất vấn
trước tiên), duyệt PR, quyết định deploy. Trách nhiệm: bắt lỗi phán đoán mà máy không bắt được.

---

## 7. Bắt đầu

```bash
# 1. Đọc rào cản
cat AGENTS.md

# 2. Kiểm tra quy trình còn nguyên vẹn
npm run sdlc:evals

# 3. Tạo intent đầu tiên
#    /sdlc:intent  <mô tả vấn đề nghiệp vụ bằng lời thường, không nhắc tên file>
```

## 8. Khi nào ĐƯỢC bỏ qua chuỗi

Chuỗi hiện vật dành cho thay đổi có rủi ro. Không bắt buộc cho:

- **Hotfix P0 đang chặn người dùng** — sửa trước, viết `intent.md` hồi tố trong 24h.
  Đặt tiền tố PR là `hotfix:`.
- **Sửa lỗi chính tả, đổi tên biến, cập nhật tài liệu** — commit thẳng.
- **Bump dependency** — tiền tố `chore(deps)`.

Mọi thứ khác đi qua chuỗi. Nếu bạn thấy mình hay xin ngoại lệ, đó là tín hiệu chuỗi đang quá
nặng — sửa quy trình, đừng sửa thói quen bỏ qua nó.
