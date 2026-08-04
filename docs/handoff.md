# Handoff — SocialDemo (.NET 10, full-stack)

## Mục tiêu của session kế tiếp
Người dùng muốn **tìm hiểu và được giải thích về cấu trúc code hiện tại** (mục đích học tập).
Vai trò session sau: **giải thích, hướng dẫn đọc code** theo tốc độ người học, tiếng Việt.
Người dùng là beginner/intermediate với .NET — đã hỏi những câu cơ bản ("đây là backend đúng không",
"chạy được chưa", "unit test hay integration test", "lỗi CORS"). Giải thích dễ hiểu, không giả định
kiến thức nâng cao, nhưng không hạ thấp.

## Ngôn ngữ
Trả lời **tiếng Việt** (đủ dấu). Thuật ngữ kỹ thuật + tên định danh giữ nguyên tiếng Anh.

## Dự án là gì
Mạng xã hội đơn giản, **full-stack .NET 10**: backend REST API (monolith Clean Architecture) +
frontend Blazor WebAssembly. Tính năng: đăng ký/đăng nhập (JWT + refresh token), post, comment,
like, follow, feed. Có khung chat SignalR (chưa chạy thật). Build sạch, 38 integration test pass,
đã chạy end-to-end qua trình duyệt. Đã push GitHub.

- Repo: https://github.com/NguyenTranBaoKhanh/SocialNetworkDemo
- Local: `D:\SOFT\Learning\SocialDemo`

## Nơi đọc để hiểu (KHÔNG lặp nội dung — trỏ tới file)
- **CLAUDE.md** — định hướng kiến trúc toàn dự án (stack, roadmap, nguyên tắc).
- **README.md** — cấu trúc, cách chạy 3 phần (Docker/API/Web), bảng API endpoints, cách test.
- **db/schema.sql** — thiết kế schema SQL kèm chú thích lý do (nguồn thiết kế gốc).

### Backend (`src/`)
- `Domain/Entities/*` — entity thuần (User, Post, Comment, Like, Follow, RefreshToken, PostMedia,
  Conversation, ConversationMember, Message, MessageAttachment).
- `Application/*` — use case service (AuthService, PostService, CommentService, LikeService,
  FollowService, FeedService) + abstraction trong `Common/` (IAppDbContext, ICurrentUser,
  IPasswordHasher, IJwtTokenGenerator, AppExceptions, Dtos).
- `Infrastructure/*` — `Persistence/AppDbContext.cs` (EF Core fluent config khớp schema),
  `Security/` (JWT + password hasher + refresh token), `Storage/` (S3StorageService upload/đọc
  ảnh từ MinIO), migrations, `DependencyInjection.cs`.
- `Api/Controllers/MediaController.cs` — `POST /api/media` upload ảnh (≤5MB) hoặc video (≤50MB)
  lên MinIO, `GET /api/media/{key}` phục vụ media cho `<img>`/`<video>` (không cần token).
- `Api/*` — `Program.cs` (wiring: DI, JWT, CORS, SignalR), `Controllers/*`, `Hubs/ChatHub.cs`,
  `Common/` (CurrentUser đọc JWT claim, AppExceptionHandler map lỗi → ProblemDetails).

### Frontend (`src/Web/` — Blazor WebAssembly)
- `Program.cs` — đăng ký DI, 2 HttpClient ("Api" thuần + "AuthorizedApi" có Bearer/refresh),
  chọn ApiBaseUrl theo scheme (http/https) để tránh mixed-content.
- `Auth/TokenStore.cs` — lưu access + refresh token trong localStorage (qua Blazored.LocalStorage).
- `Auth/JwtAuthenticationStateProvider.cs` — parse claim từ JWT để Blazor biết đã đăng nhập chưa.
- `Auth/AuthorizedHandler.cs` — DelegatingHandler: gắn Bearer vào mỗi request, gặp 401 thì tự gọi
  `/api/auth/refresh` rồi thử lại request (điểm hay nhất để giải thích cho người học).
- `Services/AuthApi.cs` (login/register/logout/refresh), `Services/PostApi.cs` (feed/post/like/comment),
  `Services/MediaApi.cs` (upload ảnh/video multipart).
- `Models/ApiModels.cs` — record C# khớp DTO của backend (client tự định nghĩa lại).
- `Pages/` — Login, Register, Home (feed), PostDetail (xem 1 bài qua URL).
- `Components/CreatePostDialog.razor` — **popup** tạo bài (nội dung + ảnh/video), mở từ feed, đăng xong
  thêm bài lên đầu feed (không chuyển trang; trang `/create` cũ đã bỏ).
- `Components/CommentsDialog.razor` — **popup** bình luận + **trả lời đa cấp** (render đệ quy theo `parentId`),
  mở từ nút 💬 ở feed (không chuyển trang).
- `ClientSettings.cs` — giữ ApiBaseUrl để ghép URL ảnh/video tuyệt đối cho `<img>`/`<video>`.
- `Layout/MainLayout.razor` — navbar dùng `<AuthorizeView>` để hiện login/logout theo trạng thái.
- `App.razor` — `<CascadingAuthenticationState>` + `<AuthorizeRouteView>` (chưa login → RedirectToLogin).

## Quyết định thiết kế đáng giải thích cho người học
1. **Clean Architecture / hướng phụ thuộc**: Api → Application → Domain; Infrastructure → Application/Domain.
   Application dùng DB qua interface `IAppDbContext` (định nghĩa ở Application, hiện thực ở Infrastructure)
   để không phụ thuộc ngược. Điểm khó nhất cho beginner — giải thích kỹ.
2. **Backend là API thuần, frontend-agnostic**: Blazor chỉ là 1 client. Thêm React/mobile sau chỉ cần
   thêm origin vào `Cors:AllowedOrigins`. Đây là lý do tách client/server.
3. **Auth 2 lớp token**: access token JWT stateless (15 phút) + refresh token lưu HASH ở DB (7 ngày,
   thu hồi được). Refresh **xoay vòng**; dùng lại token đã revoke → thu hồi toàn bộ token của user
   (phát hiện đánh cắp). Xem `AuthService.RefreshAsync`.
4. **Validate token do framework lo** (middleware JwtBearer trong `Program.cs`), chỉ **sinh** token là code mình.
5. **Like/Follow chống trùng ở tầng DB** bằng khóa kép; `like_count`/`follower_count` chỉ là counter cache.
6. **Feed = fan-out on read** + cursor pagination theo `(CreatedAt, Id)`. Xem `FeedService`.
7. **snake_case**: DbContext tự đổi tên cột sang snake_case khớp quy ước PostgreSQL.
8. **Frontend refresh tự động**: `AuthorizedHandler` bắt 401 → refresh → retry, người dùng không bị đá ra.
9. **Media qua MinIO + proxy**: ảnh/video upload lên MinIO (S3), lưu object key. Phục vụ **qua API**
   (`GET /api/media/{key}`) chứ không tải thẳng từ MinIO — để cùng scheme với API (tránh mixed-content)
   và giữ bucket private. `MediaController` validate loại (ảnh ≤5MB, video ≤50MB) và trả `mediaType`;
   frontend render `<img>` hay `<video>` theo `mediaType`. Lưu ý: MinIO chạy HTTP nên KHÔNG dùng
   `DisablePayloadSigning=true` (SDK v4 bắt HTTPS).

## Test
- `tests/IntegrationTests/` — 38 test xUnit chạy trên **Postgres thật** (Testcontainers), KHÔNG mock
  (chỉ có `TestCurrentUser` là fake). Bao Auth/Follow/Like/Post/Comment/Feed/RefreshToken.
- Đã bắt 1 bug thật: cursor pagination trong `CommentService.ListAsync` (off-by-one).
- Chạy: `dotnet test tests/IntegrationTests` (cần Docker).

## Trạng thái hạ tầng (quan trọng — dễ vấp)
- Docker: **postgres 5433**, **redis 6380** (KHÁC mặc định 5432/6379 vì máy đã có dịch vụ ở cổng chuẩn),
  **minio 9000/9001** (lưu ảnh, user/pass `minioadmin`). Media cần MinIO chạy: `docker compose up -d minio`.
- Backend chạy: http `5273`, https `7068`. Frontend Blazor: http `5073`, https `7163`.
- **CORS**: chỉ cho phép origin `http://localhost:5073` và `https://localhost:7163`. Mở app ở origin
  khác (hoặc lệch scheme) sẽ lỗi CORS — đây là lỗi người dùng vừa gặp.
- Migration đã apply; DB `socialdemo` có bảng users/posts/.../refresh_tokens.
- Ghi chú non-obvious lưu trong memory `socialdemo-setup` (tự nạp ở session sau).

## Cách chạy (3 phần, đúng thứ tự)
1. `docker compose -f "D:/SOFT/Learning/SocialDemo/docker-compose.yml" up -d postgres redis`
2. `dotnet run --project "D:/SOFT/Learning/SocialDemo/src/Api"` (terminal 1)
3. `dotnet run --project "D:/SOFT/Learning/SocialDemo/src/Web"` (terminal 2)
4. Mở trình duyệt `http://localhost:5073` (bản http đơn giản nhất, không dính cert/mixed-content).
- ⚠️ Không build khi app đang chạy (process khóa DLL → lỗi MSB3021/3027, KHÔNG phải lỗi code).

## Chưa làm (nếu chuyển từ học sang làm tiếp)
- ChatHub chưa persist/gửi message thật (mới join/typing) — frontend cũng chưa có màn chat.
- Chưa có worker flush counter từ Redis (counter cập nhật trực tiếp trong request).
- Chưa có màn profile user / list follower-following (backend cũng chưa có endpoint này).
- Media hỗ trợ **ảnh + video** (lưu thẳng). Chưa encode video (FFmpeg qua queue), chưa resize ảnh
  (ImageSharp), chưa CDN, chưa range request cho video (seek) — đúng roadmap, để Phase sau.

## Skill gợi ý cho session sau
- Đi sâu kiến trúc / cải thiện: `improve-codebase-architecture`.
- Lập kế hoạch làm tiếp (chat, profile, media): `hd-planning` hoặc `hd-brainstorming`.
