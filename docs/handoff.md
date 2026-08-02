# Handoff — SocialDemo (.NET 10 backend)

## Mục tiêu của session kế tiếp
Người dùng muốn **tìm hiểu và được giải thích về cấu trúc code hiện tại** (mục đích học tập).
Đây KHÔNG phải phiên viết thêm tính năng. Vai trò session sau: **giải thích, hướng dẫn đọc code**
theo tốc độ người học, tiếng Việt. Người dùng là beginner/intermediate với .NET backend —
đã hỏi những câu cơ bản ("đây là backend đúng không", "chạy được chưa", "test bằng Postman thế nào").
Giải thích cần dễ hiểu, tránh giả định kiến thức nâng cao, nhưng không hạ thấp.

## Ngôn ngữ
Trả lời **tiếng Việt** (có dấu đầy đủ). Thuật ngữ kỹ thuật + tên định danh giữ nguyên tiếng Anh.

## Dự án là gì
Backend mạng xã hội đơn giản: post / comment / like / follow / feed + khung chat realtime.
Monolith theo Clean Architecture. Đã build sạch (0 warning/error), chạy được, test end-to-end
17 case qua HTTP đều pass. Đã push lên GitHub.

- Repo: https://github.com/NguyenTranBaoKhanh/SocialNetworkDemo
- Thư mục local: `D:\SOFT\Learning\SocialDemo`

## Nơi đọc để hiểu (KHÔNG lặp lại nội dung ở đây — trỏ tới file)
- **CLAUDE.md** — bối cảnh & định hướng kiến trúc toàn dự án (tech stack, roadmap phase, nguyên tắc).
- **README.md** — cấu trúc thư mục, cách chạy, bảng API endpoints đã có.
- **db/schema.sql** — thiết kế schema SQL kèm chú thích lý do từng quyết định (nguồn thiết kế gốc).
- Code theo tầng:
  - `src/Domain/Entities/*` — entity thuần (User, Post, Comment, Like, Follow, Conversation, ConversationMember, Message, MessageAttachment, PostMedia).
  - `src/Application/*` — use case services (AuthService, PostService, CommentService, LikeService, FollowService, FeedService) + abstractions trong `Common/` (IAppDbContext, ICurrentUser, IPasswordHasher, IJwtTokenGenerator, AppExceptions, Dtos).
  - `src/Infrastructure/*` — `Persistence/AppDbContext.cs` (EF Core fluent config khớp schema), `Security/` (JWT + password hasher), `DependencyInjection.cs`.
  - `src/Api/*` — `Program.cs` (wiring), `Controllers/*`, `Hubs/ChatHub.cs`, `Common/` (CurrentUser đọc JWT, AppExceptionHandler map lỗi → ProblemDetails).

## Các quyết định thiết kế đáng giải thích cho người học (đã áp dụng trong code)
1. **Clean Architecture / hướng phụ thuộc**: Api → Application → Domain; Infrastructure → Application/Domain.
   Application dùng DB qua interface `IAppDbContext` (định nghĩa ở Application, hiện thực ở Infrastructure)
   để không phụ thuộc ngược vào Infrastructure. Đây là điểm dễ gây khó hiểu nhất cho beginner — nên giải thích kỹ.
2. **Like/Follow chống trùng ở tầng DB** bằng khóa kép (composite key); `like_count`/`follower_count`
   chỉ là "counter cache", nguồn sự thật là bảng `likes`/`follows`.
3. **Feed = fan-out on read** (query lúc đọc) + cursor pagination theo `(CreatedAt, Id)`. Xem `FeedService.cs`.
4. **Chat** (mới có khung, chưa chạy thật): ordering bằng `seq` per conversation (không dùng client timestamp),
   `client_msg_id` chống gửi trùng, presence/typing để ở Redis. Xem chú thích trong `ChatHub.cs` và `db/schema.sql`.
5. **Auth**: password hash bằng PasswordHasher của ASP.NET Core Identity (PBKDF2); JWT tự sinh trong
   `JwtTokenGenerator`. Lỗi nghiệp vụ ném `AppException` → `AppExceptionHandler` map sang HTTP status.
6. **snake_case**: DbContext tự đổi tên cột sang snake_case để khớp quy ước PostgreSQL trong schema.sql.

## Trạng thái hạ tầng (quan trọng — dễ vấp)
- Docker: **postgres cổng 5433**, **redis cổng 6380** (KHÁC mặc định 5432/6379, vì máy đã có postgres/valkey
  chạy sẵn ở cổng chuẩn). Connection string trong `src/Api/appsettings.json` và `AppDbContextFactory.cs` đã trỏ 5433/6380.
- Migration `InitialCreate` đã apply; DB `socialdemo` có 10 bảng + extension uuid-ossp/citext.
- Có sẵn user test `alice`/`bob` trong DB (password `secret123`) nếu chưa `docker compose down -v`.
- Ghi chú non-obvious này đã lưu trong memory `socialdemo-setup` (tự nạp ở session sau cùng project).

## Chưa làm (nếu người dùng chuyển từ học sang làm tiếp)
- ChatHub chưa persist/gửi message thật (mới join/typing).
- Chưa có worker flush counter từ Redis; hiện counter cập nhật trực tiếp trong request.
- Chưa có endpoint profile user / list follower-following.
- Chưa upload media thật lên MinIO (mới nhận URL).
- **Chưa có frontend** (toàn bộ mới là backend trả JSON).

## Cách chạy nhanh (để vừa đọc code vừa thử)
1. `docker compose -f "D:/SOFT/Learning/SocialDemo/docker-compose.yml" up -d postgres redis`
2. `dotnet run --project "D:/SOFT/Learning/SocialDemo/src/Api"` (cổng http mặc định 5273)
3. Test: import `SocialDemo.postman_collection.json` vào Postman, hoặc `GET http://localhost:5273/health`.
4. Xem DB: pgAdmin4 nối `localhost:5433` / db `socialdemo` / `postgres`/`postgres`.

## Skill gợi ý cho session sau
- Không bắt buộc skill nào. Nếu người dùng muốn đi sâu kiến trúc và cải thiện: `improve-codebase-architecture`.
- Nếu chuyển sang lập kế hoạch làm tiếp (chat/frontend): `hd-planning` hoặc `hd-brainstorming`.
