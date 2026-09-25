# AGENTS.md — Rào cản hành vi cho Agent (<TÊN DỰ ÁN>)

> **MẪU.** Mọi chỗ `<…>` là phần bạn phải điền. Xoá những mục không áp dụng cho dự án
> của bạn — một rào cản chung chung không ai kiểm được thì tệ hơn là không có.

> **Vai trò của file này:** đây là hiện vật của **Guardrails Designer** trong AI-Native SDLC.
> Mọi Agent (Claude Code, Cursor, sub-agent) PHẢI đọc file này trước khi sinh `spec.md`,
> `plan.md` hoặc chạm vào mã nguồn. Con người sở hữu file này; Agent chỉ đọc.
>
> Quy trình vận hành: [docs/AI-NATIVE-SDLC.md](docs/AI-NATIVE-SDLC.md)

---

## 1. Bối cảnh hệ thống

<Một đoạn: hệ thống này làm gì, phục vụ ai, ranh giới nào cố ý KHÔNG có.
Câu "không có gì" quan trọng ngang câu "có gì" — nó chặn Agent tự suy diễn thêm
multi-tenant, i18n, phân quyền nhiều cấp… mà không ai yêu cầu.>

| Workspace | Stack   | Port    | Vai trò   |
|-----------|---------|---------|-----------|
| `<ws-1>`  | <stack> | <port>  | <vai trò> |
| `<ws-2>`  | <stack> | <port>  | <vai trò> |

Tên workspace ở đây phải khớp với khoá trong [sdlc.config.json](sdlc.config.json).

<Dữ liệu cấu hình chạy nằm ở đâu — ví dụ: "Thông tin cửa hàng nằm ở collection `settings`
(API `/settings`) — **không** hardcode.">

> 📚 **Chi tiết cấu trúc thư mục:** xem [docs/architecture/STRUCTURE.md](docs/architecture/STRUCTURE.md).
> ⚠️ Khi tạo module / thư mục nghiệp vụ / workspace mới, Agent **BẮT BUỘC** cập nhật file đó.

---

## 2. Ràng buộc kỹ thuật bắt buộc

> Viết ở đây những luật mà **vi phạm là chặn merge**, không phải sở thích phong cách.
> Phép thử: nếu bạn không sẵn sàng bắt một PR làm lại vì luật đó, nó thuộc về tài liệu
> phong cách, không thuộc về file này.

### 2.1. Backend

- Cấu trúc module: `<quy ước thư mục>`.
- **Validation:** <mọi payload vào phải qua lớp nào; có được nhận `any` không>.
- **Auth:** <guard/middleware nào>. Endpoint mới mặc định là **được bảo vệ**;
  muốn public phải nêu lý do trong `spec.md`.
- **CORS:** đọc từ biến môi trường. Không thêm origin cứng vào code.
- **Bí mật:** chỉ qua config/`.env`. Không commit `.env`; cập nhật `.env.example` khi thêm biến.
- **Toàn vẹn dữ liệu:** <thao tác nào bắt buộc atomic/transaction — tiền, tồn kho, hạn mức>.
  Test hồi quy bảo vệ nó: `<đường dẫn test>` (khai báo trong `sdlc.config.json → requiredTests`).
- **Tiền tệ:** <đơn vị, kiểu số>. Không dùng float cho tổng tiền.

### 2.2. Frontend

- <Framework + quy ước styling. Ví dụ: Tailwind v4 → `@import "tailwindcss"`, KHÔNG tạo
  `tailwind.config.js`.>
- Phong cách thị giác: <tên>. Theo [docs/design/DESIGN_SYSTEM.md](docs/design/DESIGN_SYSTEM.md)
  — không tự sáng tác token màu/spacing mới.
- Gọi API qua lớp service/client có sẵn của từng app, không `fetch` rải rác trong component.
- TypeScript: **type error = fail**, không dùng `@ts-ignore` để né (eval E02 kiểm điều này).

### 2.3. Ngôn ngữ

- UI, thông báo lỗi hướng người dùng, comment nghiệp vụ: **<ngôn ngữ>**.
- Tên biến/hàm/file, commit message: **tiếng Anh**.

---

## 3. Chính sách bảo mật (không thương lượng)

Agent **KHÔNG** được, trong bất kỳ hoàn cảnh nào:

1. Commit secret, API key, connection string thật, hay file `.env`.
2. Nới lỏng guard xác thực/phân quyền để "cho test chạy qua".
3. Tắt lớp validation đầu vào.
4. Thêm `origin: '*'` vào CORS.
5. Log dữ liệu định danh người dùng (<liệt kê trường cụ thể>) ra console/file log.
6. Chạy migration hay script ghi đè dữ liệu trên DB không phải `<tên db dev>`.
7. Cài package mới mà không nêu trong `plan.md` và không được người duyệt chấp thuận.

Vi phạm bất kỳ điều nào ở trên → dừng lại, báo người điều phối, không tự "sửa cho xong".

Mỗi luật ở mục này mà grep kiểm được thì **nên có một eval tương ứng** trong
`docs/evals/project-evals.mjs` — luật không ai kiểm là luật sẽ bị quên.

---

## 4. Định nghĩa Hoàn thành (Definition of Done)

Một thay đổi mã nguồn CHỈ được coi là xong khi **toàn bộ** các cổng sau đều xanh:

| Cổng           | Cách chạy                              | Áp dụng                     |
|----------------|----------------------------------------|-----------------------------|
| Lint (ratchet) | `scripts/sdlc/lint-ratchet.mjs <ws>`   | workspace có `lintReportCommand` |
| Type/Build     | `npm run build`                        | mọi workspace có đổi        |
| Unit test      | `npm test`                             | workspace có gate `test`    |
| E2E            | gate `e2eGate`                         | khi chạm `e2eTriggers`      |
| Bằng chứng     | ảnh chụp / log trong `docs/evidence/`  | mọi thay đổi có mặt UI      |
| CHANGELOG      | mục mới ở đầu `CHANGELOG.md`           | mọi thay đổi hành vi        |

Cổng nào chạy cho workspace nào là do [sdlc.config.json](sdlc.config.json) quyết định.
Chạy tất cả bằng một lệnh: `npm run sdlc:verify` (xem [scripts/sdlc/verify.mjs](scripts/sdlc/verify.mjs)).

**Về cổng lint.** Dự án brownfield thường đã có sẵn hàng trăm lỗi eslint từ trước khi quy
trình này ra đời. Cấm tuyệt đối thì cổng đỏ ngay ngày đầu và mọi người sẽ học cách lách nó.
Nên cổng lint là một **ratchet**: nó so số lỗi *theo từng file* với
`docs/evals/lint-baseline.json` và chỉ đỏ khi có lỗi **mới**. Con số đó là nợ kỹ thuật, không
phải chuẩn mực — nó chỉ được phép giảm. Sửa bớt được thì chạy `npm run sdlc:lint-baseline`
để chốt lại mức thấp hơn.

⚠️ `lintReportCommand` phải là lệnh **chỉ đọc**. Đừng trỏ nó vào một script có `--fix`: một
cổng kiểm tra đi sửa working tree là cách nhanh nhất để lần verify "xanh" trở nên vô nghĩa.

**Agent phải tự chạy hết các cổng này TRƯỚC khi con người nhìn vào code.** Đây là nguyên tắc
kiểm thử tự trị: con người review logic và kiến trúc, không phải review lỗi cú pháp.

---

## 5. Quy tắc viết test

- Test mới đi kèm code mới, cùng một commit. Không có "sẽ viết test sau".
- Unit test đặt cạnh mã nó kiểm: `<quy ước đường dẫn>`.
- E2E đặt ở `<quy ước đường dẫn>`.
- Test phải fail được: viết test rồi cố tình phá code để xác nhận nó đỏ, sau đó khôi phục.
- Không mock lớp mà mình đang test. Không assert `expect(true).toBe(true)`.
- Test nào mua bằng một sự cố thật thì thêm vào `sdlc.config.json → requiredTests` để
  không ai "dọn dẹp" mất nó.

---

## 6. Quy tắc Git

- Nhánh: `<type>/<intent-id>-<slug>` — ví dụ `feat/01-260902-<slug>`.
- Commit theo Conventional Commits, tiếng Anh, tham chiếu intent id ở footer:
  `Intent: docs/intents/<id>/`
- Một PR = một `intent.md`. PR mô tả phải link tới `intent.md`, `spec.md`, `plan.md`.
- Sub-agent chạy song song PHẢI dùng `git worktree` riêng, không cùng ghi vào working tree chính.

---

## 7. Thứ tự nguồn sự thật khi mâu thuẫn

1. `AGENTS.md` (file này) — rào cản, thắng tất cả.
2. `docs/intents/<id>/spec.md` — đặc tả đã duyệt của tính năng đang làm.
3. `docs/intents/<id>/plan.md` — kế hoạch thực thi.
4. `<tài liệu tham chiếu của dự án: design system, schema, api…>`.
5. Code hiện có.

Nếu `spec.md` mâu thuẫn với `AGENTS.md` → **dừng**, báo người điều phối. Không tự chọn bên.
