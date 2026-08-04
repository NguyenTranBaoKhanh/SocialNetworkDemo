# SocialDemo — Mạng xã hội (.NET 10)

Full-stack .NET theo `CLAUDE.md`: backend REST API (monolith Clean Architecture) + frontend
Blazor WebAssembly. Auth (JWT + refresh token), post/comment/like/follow, feed. Khung chat SignalR.

## Cấu trúc

```
src/
  Api/              # ASP.NET Core host, SignalR ChatHub, JWT auth, CORS, Program.cs
  Application/      # use case service, DTO, validation
  Domain/           # entity (User, Post, Comment, Like, Follow, RefreshToken, Message...)
  Infrastructure/   # EF Core AppDbContext, Npgsql, Redis, migrations, security
  Web/              # Frontend Blazor WebAssembly (Auth + Feed + Post)
db/schema.sql       # schema SQL tham chiếu (nguồn thiết kế)
docker-compose.yml  # postgres (5433) + redis (6380) + minio (9000/9001)
```

> Backend là REST API thuần (JSON + JWT + SignalR) — frontend-agnostic. Blazor hiện tại chỉ là
> một client; sau này thêm React/mobile chỉ cần thêm origin vào `Cors:AllowedOrigins`.

> Port đã đổi sang **5433** (postgres) và **6380** (redis) để tránh đụng dịch vụ chạy sẵn trên máy.

## Cài đặt & chạy từ đầu (fresh machine)

Làm tuần tự các bước dưới đây là chạy được app.

### 0. Yêu cầu (cài một lần)

| Công cụ | Kiểm tra / Cài |
|---|---|
| **.NET 10 SDK** | `dotnet --version` (phải ≥ 10.0). Tải: https://dotnet.microsoft.com/download |
| **Docker Desktop** | `docker --version`. Phải **đang chạy** (mở app Docker Desktop). |
| **dotnet-ef** (tạo schema) | `dotnet tool install --global dotnet-ef` (nếu `dotnet ef` báo not found) |

> Không cần cài NuGet source gì thêm: repo có `nuget.config` chỉ dùng nuget.org.

### 1. Lấy code

```bash
git clone https://github.com/NguyenTranBaoKhanh/SocialNetworkDemo.git
```

### 2. Bật hạ tầng (PostgreSQL + Redis)

Chạy tại thư mục gốc dự án:

```bash
docker compose up -d postgres redis minio
```

> Postgres map ra cổng **5433**, Redis **6380** (khác mặc định để tránh đụng dịch vụ sẵn có).
> **MinIO** (lưu ảnh) ở cổng **9000** (API) / **9001** (console `minioadmin`/`minioadmin`).
> Connection string đã trỏ đúng, không cần chỉnh.

### 3. Tạo schema database (apply migration)

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

Lệnh này tạo toàn bộ bảng (users, posts, refresh_tokens...) trong DB `socialdemo`. Chỉ cần chạy
lại khi có migration mới.

### 4. Chạy Backend API — terminal 1

```bash
dotnet run --project src/Api
```

Kiểm tra: mở `http://localhost:5273/health` thấy `{"status":"ok"}`.

### 5. Chạy Frontend Blazor — terminal 2 (mở SONG SONG, không tắt terminal 1)

```bash
dotnet run --project src/Web
```

### 6. Dùng app

Mở trình duyệt vào **`http://localhost:5073`** → **Đăng ký** một tài khoản → dùng feed, tạo bài, like, comment.

---

### Chạy bằng Visual Studio (thay cho bước 4–5)

1. Vẫn phải làm **bước 2–3** trước (Docker + migration) — VS không tự bật Postgres.
2. Chuột phải **Solution** → **Configure Startup Projects** → **Multiple startup projects**.
3. Đặt **Action = Start** cho **Api** và **Web** (còn lại **None**) → **OK**.
4. Bấm **F5**. VS mở trình duyệt ở bản https (`https://localhost:7163`).

### Những lỗi hay gặp

| Triệu chứng | Nguyên nhân & cách xử lý |
|---|---|
| Build lỗi `MSB3021/MSB3027 ... file is locked` | App đang chạy khóa DLL. **Dừng app trước khi build** (Ctrl+C hoặc Shift+F5). **Không phải lỗi code.** |
| Đăng nhập lỗi **CORS** | Mở app ở origin không được khai. Dùng đúng `http://localhost:5073` hoặc `https://localhost:7163` (đã khai trong `Cors:AllowedOrigins`). |
| Bản https báo lỗi cert/kết nối | Chạy `dotnet dev-certs https --trust` một lần, rồi mở lại. Hoặc dùng bản http cho gọn. |
| `dotnet ef` not found | Cài: `dotnet tool install --global dotnet-ef`. |
| API không kết nối được DB | Docker chưa chạy. `docker compose up -d postgres redis`. |

### Dừng

- API/Web: **Ctrl+C** trong từng terminal (hoặc **Shift+F5** trong VS).
- Hạ tầng: `docker compose down` (thêm `-v` nếu muốn xóa sạch dữ liệu DB).

## Kiến trúc đã áp dụng (theo CLAUDE.md)

- **Clean layering**: Api → Application → Domain; Infrastructure → Application/Domain.
- **Like**: bảng `likes` khóa kép chống double-like; `like_count` chỉ là cache (flush từ Redis).
- **Follow**: khóa kép + `CHECK` chống tự-follow.
- **Chat**: ordering bằng `seq` per conversation (không dùng client timestamp); `client_msg_id`
  chống gửi trùng; presence/typing để ở Redis (không DB); Redis backplane bật sẵn khi có >1 instance.
- **Feed**: partial index `idx_posts_author_created ... WHERE deleted_at IS NULL` cho fan-out on read.

## API endpoints (đã có)

| Method | Route | Auth | Chức năng |
|---|---|:---:|---|
| POST | `/api/auth/register` | | Đăng ký (hash password, chống trùng username/email) |
| POST | `/api/auth/login` | | Đăng nhập → access token + refresh token |
| POST | `/api/auth/refresh` | | Đổi refresh token lấy cặp token mới (xoay vòng) |
| POST | `/api/auth/logout` | | Thu hồi refresh token |
| POST | `/api/media` | ✓ | Upload ảnh (≤5MB) hoặc video (≤50MB) → lưu MinIO, trả url + loại |
| GET | `/api/media/{key}` | | Phục vụ ảnh/video (để thẻ `<img>`/`<video>` tải, không cần token) |
| POST | `/api/posts` | ✓ | Tạo post (kèm media url) |
| GET | `/api/posts/{id}` | | Xem post |
| DELETE | `/api/posts/{id}` | ✓ | Xóa post (soft, chỉ tác giả) |
| POST | `/api/posts/{id}/comments` | ✓ | Bình luận / reply (parentId) |
| GET | `/api/posts/{id}/comments` | | List comment (cursor) |
| DELETE | `/api/comments/{id}` | ✓ | Xóa comment (soft) |
| POST/DELETE | `/api/posts/{id}/like` | ✓ | Like / unlike (idempotent) |
| POST/DELETE | `/api/users/{username}/follow` | ✓ | Follow / unfollow (chống tự-follow) |
| GET | `/api/feed?cursor=&limit=` | ✓ | Feed fan-out on read, cursor pagination |

> Gửi access token qua header `Authorization: Bearer <token>`.

**Chiến lược token:** access token (JWT stateless, sống ngắn **15 phút**) + refresh token
(sống dài **7 ngày**, lưu hash ở bảng `refresh_tokens` nên **thu hồi được**). Khi access token
hết hạn, client gọi `/api/auth/refresh`. Mỗi lần refresh **xoay vòng** (revoke token cũ, cấp mới);
dùng lại token đã revoke bị coi là dấu hiệu bị đánh cắp → thu hồi toàn bộ token của user.

## Frontend (Blazor WebAssembly)

Chạy (cần backend chạy trước):

```bash
dotnet run --project src/Web
```

Mở **`http://localhost:5073`**. Đã có: đăng ký/đăng nhập, feed (cursor pagination + like),
tạo bài **kèm ảnh/video** (upload lên MinIO), chi tiết bài + comment. Đổi địa chỉ API trong
`src/Web/wwwroot/appsettings.json` (`ApiBaseUrl` / `ApiBaseUrlHttps`) — không cần build lại.

> Upload media cần **MinIO** đang chạy (`docker compose up -d minio`). Ảnh/video phục vụ qua API
> (`/api/media/{key}`) nên hiển thị cùng scheme, không dính mixed-content.

### Cấu trúc frontend (đọc theo thứ tự để hiểu)

| File | Vai trò |
|---|---|
| `Program.cs` | Đăng ký DI + 2 HttpClient: **"Api"** (thuần, cho login/register/refresh) và **"AuthorizedApi"** (tự gắn Bearer + refresh). Chọn API base theo scheme trang (tránh mixed-content). |
| `Models/ApiModels.cs` | Record C# khớp DTO backend (client tự định nghĩa lại — vì là "hợp đồng" giữa 2 bên). |
| `Auth/TokenStore.cs` | Lưu access + refresh token trong **localStorage** trình duyệt. |
| `Auth/JwtAuthenticationStateProvider.cs` | Đọc claim từ JWT → Blazor biết đã đăng nhập chưa (điều khiển `<AuthorizeView>`). |
| `Auth/AuthorizedHandler.cs` | **Điểm cốt lõi**: gắn `Bearer` vào mỗi request; gặp **401** thì tự gọi `/api/auth/refresh`, lấy token mới rồi **thử lại request** — người dùng không bị đá ra. |
| `Services/AuthApi.cs`, `Services/PostApi.cs` | Gọi API auth / post-feed-like-comment. |
| `Pages/*.razor` | Login, Register, Home (feed), CreatePost, PostDetail. |
| `App.razor` | `<CascadingAuthenticationState>` + `<AuthorizeRouteView>`: trang có `[Authorize]` mà chưa login → tự chuyển về `/login`. |

### Luồng đăng nhập (tóm tắt)

```
Login/Register → API trả (accessToken, refreshToken)
   → TokenStore lưu vào localStorage
   → AuthenticationStateProvider.NotifyChanged() → navbar đổi sang trạng thái đã đăng nhập
   → gọi API có [Authorize]: AuthorizedHandler gắn Bearer
       → nếu 401 (access hết hạn): tự refresh → thử lại → người dùng không hay biết
```

### ⚠️ CORS & port (nguồn lỗi hay gặp)

Backend chỉ cho phép **origin** frontend đã khai trong `src/Api/appsettings.json` → `Cors:AllowedOrigins`
(hiện: `http://localhost:5073` và `https://localhost:7163`). Mở app ở origin/scheme khác → **lỗi CORS**.
Trang **https** phải gọi API **https** (không thì dính mixed-content) — `Program.cs` đã tự chọn đúng scheme.
Đơn giản nhất khi học: mở bản **http** `http://localhost:5073`.

## Test

Integration test dùng **Testcontainers** (bật Postgres thật trong Docker, apply migration, test
service Application against DB thật — bắt được cả constraint/index/PublicId sinh bởi DB).

```bash
dotnet test tests/IntegrationTests
```

> Cần Docker đang chạy. Test tự tạo container Postgres riêng (`socialdemo_test`), không đụng DB dev.

Bao phủ: Auth (register/login, trùng username, citext, validation), Follow (idempotent, tự-follow,
counter), Like (idempotent, unlike), Post (tạo/media/xóa 403/soft delete), Comment (reply, cursor),
Feed (fan-out on read, thứ tự, cursor), RefreshToken (xoay vòng, revoke, phát hiện tái sử dụng).
Tổng **38 test**.

## Bước tiếp theo (chưa làm)

- [ ] Nối `ChatHub.SendMessage` vào service persist (cấp seq trong transaction) rồi mới broadcast +
      màn chat ở frontend.
- [ ] Worker flush like counter từ Redis xuống DB (hiện counter cập nhật trực tiếp trong request).
- [ ] Endpoint + màn profile user, list follower/following.
- [ ] Media: encode video bằng FFmpeg qua queue (hiện lưu thẳng); resize ảnh (ImageSharp); CDN;
      range request cho video (seek). (Ảnh + video cơ bản đã xong.)
