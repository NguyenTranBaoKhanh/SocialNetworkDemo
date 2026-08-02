# SocialDemo — Mạng xã hội (.NET 10)

Skeleton monolith theo `CLAUDE.md`. Phase 1 (post/comment/like/follow) + chat realtime (SignalR).

## Cấu trúc

```
src/
  Api/              # ASP.NET Core host, SignalR ChatHub, JWT auth, Program.cs
  Application/      # use case, DTO, validation (đang trống — bước tiếp theo)
  Domain/           # entity (User, Post, Comment, Like, Follow, Conversation, Message...)
  Infrastructure/   # EF Core AppDbContext, Npgsql, Redis, migrations
db/schema.sql       # schema SQL tham chiếu (nguồn thiết kế)
docker-compose.yml  # postgres (5433) + redis (6380) + minio (9000/9001)
```

> Port đã đổi sang **5433** (postgres) và **6380** (redis) để tránh đụng dịch vụ chạy sẵn trên máy.

## Chạy

1. Hạ tầng:

```bash
docker compose up -d postgres redis
```

2. Tạo schema (apply migration):

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

3. Chạy API:

```bash
dotnet run --project src/Api
```

Kiểm tra: `GET /health` → `{"status":"ok"}`. SignalR hub tại `/hubs/chat`.

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
| POST | `/api/auth/login` | | Đăng nhập → JWT |
| POST | `/api/posts` | ✓ | Tạo post (kèm media) |
| GET | `/api/posts/{id}` | | Xem post |
| DELETE | `/api/posts/{id}` | ✓ | Xóa post (soft, chỉ tác giả) |
| POST | `/api/posts/{id}/comments` | ✓ | Bình luận / reply (parentId) |
| GET | `/api/posts/{id}/comments` | | List comment (cursor) |
| DELETE | `/api/comments/{id}` | ✓ | Xóa comment (soft) |
| POST/DELETE | `/api/posts/{id}/like` | ✓ | Like / unlike (idempotent) |
| POST/DELETE | `/api/users/{username}/follow` | ✓ | Follow / unfollow (chống tự-follow) |
| GET | `/api/feed?cursor=&limit=` | ✓ | Feed fan-out on read, cursor pagination |

> Gửi token qua header `Authorization: Bearer <token>`.

## Test

Integration test dùng **Testcontainers** (bật Postgres thật trong Docker, apply migration, test
service Application against DB thật — bắt được cả constraint/index/PublicId sinh bởi DB).

```bash
dotnet test tests/IntegrationTests
```

> Cần Docker đang chạy. Test tự tạo container Postgres riêng (`socialdemo_test`), không đụng DB dev.

Bao phủ: Auth (register/login, trùng username, citext, validation), Follow (idempotent, tự-follow,
counter), Like (idempotent, unlike), Post (tạo/media/xóa 403/soft delete), Comment (reply, cursor),
Feed (fan-out on read, thứ tự, cursor). Tổng **32 test**.

## Bước tiếp theo (chưa làm)

- [ ] Nối `ChatHub.SendMessage` vào service persist (cấp seq trong transaction) rồi mới broadcast.
- [ ] Worker flush like counter từ Redis xuống DB (hiện counter cập nhật trực tiếp trong request).
- [ ] Endpoint profile user + list follower/following.
- [ ] Upload media thật lên MinIO (hiện chỉ nhận URL).
- [ ] Frontend (React/Blazor).
