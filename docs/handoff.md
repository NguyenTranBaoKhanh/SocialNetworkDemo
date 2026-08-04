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
like, follow, feed, **chat realtime 1-1 (SignalR)**. Build sạch, 38 integration test pass,
đã chạy end-to-end qua trình duyệt. Đã push GitHub (commit chat đang chờ, xem cuối file).

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
- `Application/Users/UserService.cs` + `Api/Controllers/UsersController.cs` — GET `/api/users/me`,
  `/api/users/{username}` (kèm isMe/isFollowedByMe), `/api/users/{username}/posts`, `/api/users/suggestions`;
  PUT `/api/users/me` (tên+bio), PUT `/api/users/me/avatar`, POST `/api/users/me/password` (thu hồi refresh token).
- `Api/*` — `Program.cs` (wiring: DI, JWT, CORS, SignalR), `Controllers/*`, `Common/`.
  - **Chat**: `Application/Chat/ChatService.cs` (nhận userId TƯỜNG MINH vì hub không có HttpContext;
    persist + cấp seq + `message_attachments` cho ảnh/video), `Api/Hubs/ChatHub.cs` (SignalR:
    SendMessage/MarkRead, fan-out theo group `user:{id}`, presence), `Api/Hubs/PresenceTracker.cs`
    (online in-memory), `Api/Controllers/ConversationsController.cs` (REST: list, direct, messages, read).
    Endpoint hub: `/hubs/chat` (token qua `?access_token=`).

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
- `Pages/` — Login, Register, Home (feed), PostDetail, **Profile** (`/u/{username}`: header + bio,
  đổi avatar / chỉnh sửa hồ sơ (tên+bio) / đổi mật khẩu nếu là mình; follow nếu người khác; bài của user).
- `Components/EditProfileDialog.razor` — popup sửa tên+bio và đổi mật khẩu.
- `Layout/MainLayout.razor` — **sidebar trái**: avatar + tên (link profile), **gợi ý follow** (nút Follow), đăng xuất.
- `Components/PostCard.razor` — thẻ 1 bài dùng chung feed & profile; tự quản lý like + popup bình luận;
  tên tác giả link tới `/u/{username}`.
- `Components/CreatePostDialog.razor` — **popup** tạo bài (nội dung + ảnh/video), đăng xong thêm bài
  lên đầu feed (trang `/create` cũ đã bỏ).
- `Components/CommentsDialog.razor` — **popup** bình luận + **trả lời đa cấp** (render đệ quy theo `parentId`).
- `Services/UserApi.cs` — me/profile/suggestions/avatar/follow; `PostApi.GetUserPostsAsync` (bài của user).
- **Chat (xem mục "GIẢI THÍCH SÂU" bên dưới)**: `Services/ChatConnection.cs` (kết nối SignalR DÙNG CHUNG
  toàn app), `Services/ChatApi.cs` (REST), `Pages/Messages.razor` (khung chat + infinite scroll + gửi media),
  `Components/ChatDock.razor` (cửa sổ chat nổi khi ở trang khác). Sidebar có badge tổng unread; nút "Nhắn tin"
  ở Profile mở `/messages?c={id}`.
- `ClientSettings.cs` — giữ ApiBaseUrl để ghép URL ảnh/video/avatar tuyệt đối.
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

## 🔵 Chat realtime — GIẢI THÍCH SÂU (phần khó nhất, người dùng muốn học kỹ)

> Session sau: đây là phần trọng tâm cần giải thích chậm, dễ hiểu. Dưới đây là toàn bộ mô hình,
> luồng chạy, và các "bẫy" đáng lưu ý. Trỏ tới code khi giải thích, đừng chỉ đọc lý thuyết.

### 0. REST lo gì, SignalR lo gì (chia việc)
Chat dùng **2 kênh** song song:
- **REST** (`ConversationsController`) — dữ liệu "tĩnh": lấy **danh sách hội thoại**, **lịch sử tin nhắn**,
  **tạo hội thoại**, **đánh dấu đã đọc**. Gọi lúc mở trang / mở hội thoại.
- **SignalR** (`ChatHub`, `/hubs/chat`) — dữ liệu "realtime": **gửi tin** và **nhận tin ngay lập tức**,
  **trạng thái online**. Là kết nối WebSocket giữ mở liên tục.
Giải thích cho người học: REST là "hỏi–đáp một lần", SignalR là "đường dây luôn mở để đẩy tin 2 chiều".

### 1. Các bảng DB liên quan (xem `db/schema.sql`)
- `conversations` — 1 hội thoại. Quan trọng:
  - `type` = 'direct' (1-1) | 'group' (chưa dùng).
  - `direct_key` = `"min(id):max(id)"` của 2 user → **UNIQUE**, để không tạo trùng hội thoại 1-1
    giữa cùng 2 người (dù ai bấm "Nhắn tin" trước).
  - `next_seq` — bộ đếm cấp số thứ tự cho tin nhắn kế tiếp trong hội thoại này.
  - `last_message_at` — để sắp xếp danh sách hội thoại (mới nhất lên đầu).
- `conversation_members` — ai thuộc hội thoại nào. `last_read_seq` = đã đọc tới seq nào (tính unread).
- `messages` — tin nhắn. `seq` = thứ tự TRONG hội thoại (UNIQUE theo `(conversation_id, seq)`),
  `client_msg_id` = id tạm do client sinh (chống gửi trùng).

### 2. Vì sao ordering bằng `seq` chứ không bằng thời gian?
Đồng hồ client không đáng tin, và 2 tin gửi cùng mili-giây có thể lệch. Mỗi hội thoại có bộ đếm
`next_seq` riêng: tin nào vào trước được seq nhỏ hơn → thứ tự ổn định tuyệt đối. `UNIQUE(conversation_id, seq)`
chốt lại ở tầng DB. (Đúng nguyên tắc CLAUDE.md: "ordering bằng sequence per conversation".)

### 3. ⚠️ BẪY LỚN NHẤT: SignalR hub KHÔNG có `HttpContext`
`ICurrentUser` (dùng ở controller) đọc user từ `HttpContext` — nhưng trong **hub method**, mỗi tin nhắn
KHÔNG đi qua HttpContext nên `ICurrentUser.Id` sẽ **null**. Vì vậy:
- `ChatService` **KHÔNG dùng `ICurrentUser`**; mọi method nhận `userId` **tường minh** làm tham số.
- Controller truyền `_current.Id`; hub truyền id đọc từ `Context.User` claims (`ChatHub.UserId`).
Đây là điểm rất dễ sai — giải thích kỹ cho người học.

### 4. Luồng GỬI TIN end-to-end (đọc theo đúng thứ tự này)
1. Người dùng gõ + Gửi → `ChatConnection.SendMessageAsync(convId, text, attachments?)` (service DÙNG CHUNG,
   xem mục 9) → `_hub.InvokeAsync("SendMessage", convId, content, attachments, clientMsgId)`.
2. `ChatHub.SendMessage` (server) → gọi `ChatService.SendMessageAsync(userId = ChatHub.UserId, ...)`.
3. `ChatService.SendMessageAsync`:
   - kiểm tra người gửi là thành viên; cho phép tin CHỈ có media (không cần text);
   - idempotency: `client_msg_id` đã tồn tại → trả lại tin cũ;
   - **cấp seq**: `seq = conv.NextSeq; conv.NextSeq++;` + `last_message_at`;
   - insert `messages` (+ `message_attachments` nếu có ảnh/video); **SaveChanges** (persist TRƯỚC khi ack);
   - trả `SendMessageResult(convPublicId, MessageResponse, memberIds)`.
4. `ChatHub` **fan-out**: với mỗi `memberId`, gửi `MessageReceived` tới group `user:{memberId}` (kể cả người gửi).
5. Mỗi client nhận `MessageReceived` trong **`ChatConnection.OnMessageReceived`** (MỘT chỗ duy nhất):
   - cập nhật `Conversations` (last message, unread, đưa lên đầu) → sidebar badge tự đổi;
   - phát lại sự kiện C# `Changed` (UI re-render) + `MessageArrived` (cho ai đang mở khung chat/popup append).
   Các "người nghe" (trang Messages, ChatDock) tự quyết định append vào khung của mình.

### 5. Fan-out theo group `user:{id}` (vì sao không phải group theo conversation)
Khi 1 connection kết nối, `OnConnectedAsync` cho nó vào group `user:{myId}`. Khi gửi tin, server đẩy
tới group của TỪNG thành viên. Ưu điểm: người nhận thấy tin **dù chưa mở hội thoại đó** (để cập nhật
danh sách + badge). Nếu group theo conversationId thì chỉ ai đang mở hội thoại mới nhận được.

### 6. Presence (trạng thái online) — `PresenceTracker`
- In-memory: `ConcurrentDictionary<userId, số connection>`. `Connect` trả true nếu vừa online (connection
  đầu tiên), `Disconnect` trả true nếu vừa offline (connection cuối).
- `OnConnectedAsync`: gửi `OnlineUsers` (danh sách đang online) cho **riêng người vừa vào**; nếu vừa
  online thì broadcast `PresenceChanged(online=true)` cho **những người khác**.
  (Thứ tự cố ý: gửi OnlineUsers TRƯỚC khi `Connect` để danh sách không chứa chính mình.)
- `OnDisconnectedAsync`: nếu vừa offline → broadcast `PresenceChanged(online=false)`.
- ⚠️ Hạn chế: chỉ đúng **1 instance** (nhiều instance phải chuyển sang Redis — CLAUDE.md). Presence GIỜ
  đã **app-wide**: `MainLayout` gọi `ChatConnection.EnsureStartedAsync()` khi đăng nhập → user online suốt phiên.

### 7. Auth cho SignalR (token qua query string)
WebSocket không gửi header `Authorization` như HTTP thường. Client cấu hình `AccessTokenProvider`
(đọc token từ `TokenStore`) → SignalR gắn token vào `?access_token=`. Server: trong `Program.cs`,
`JwtBearerEvents.OnMessageReceived` đọc `access_token` từ query khi path là `/hubs/chat`. Xem đoạn đó.

### 8. Đánh dấu đã đọc + đếm chưa đọc
`unread` của 1 hội thoại = số message có `seq > last_read_seq` của mình và **không phải mình gửi**.
Mở hội thoại → `MarkRead` (REST hoặc hub) cập nhật `last_read_seq` = seq mới nhất → badge về 0.

### 9. Kiến trúc FRONTEND chat: `ChatConnection` DÙNG CHUNG (điểm mấu chốt — mới)
Kết nối SignalR KHÔNG nằm trong trang Messages nữa mà ở service **`Services/ChatConnection.cs`** (đăng ký
`Scoped` → sống suốt phiên WASM = 1 instance dùng chung cho mọi component). Nó giữ:
- `HubConnection` (mở khi đăng nhập, `WithAutomaticReconnect`, token qua `AccessTokenProvider`),
- `Conversations` (danh sách + unread), `Online` (tập user online),
- `TotalUnread` (tổng unread), `ActiveConversationId` (hội thoại đang mở ở trang Messages — không tính unread).

Phát 2 **sự kiện C#** cho các component đăng ký:
- `Changed` — báo UI re-render (sidebar badge, danh sách hội thoại, chấm online...).
- `MessageArrived(MessageReceived)` — báo có tin mới (khung chat / popup append).

`MainLayout` gọi `EnsureStartedAsync()` khi đăng nhập → kết nối **sống toàn app** (nhờ đó có badge tổng
unread + presence app-wide + cửa sổ nổi). Callback SignalR chạy ngoài vòng render → subscriber phải
`InvokeAsync(StateHasChanged)`. Không optimistic-append khi gửi — tin hiện qua chính `MessageArrived`
(server broadcast cả người gửi) → tránh nhân đôi.

### 9a. Badge tổng chưa đọc (sidebar)
`MainLayout` hiện badge đỏ cạnh "Tin nhắn" = `Chat.TotalUnread` (sum unread mọi hội thoại), subscribe
`Chat.Changed` để cập nhật realtime **kể cả khi đang ở trang khác** (vì kết nối app-wide).

### 9b. Trang `Messages.razor` (khung chat đầy đủ)
Đọc chung `Chat.Conversations` / `Chat.Online`; mở hội thoại → set `Chat.ActiveConversationId`, nạp lịch sử
(REST `GetMessagesAsync`), `MarkRead`; gửi qua `Chat.SendMessageAsync`; append tin mới qua `Chat.MessageArrived`.

### 9c. Cuộn tải tin cũ (infinite scroll ngược)
Mở hội thoại: 30 tin mới nhất, lưu `_olderCursor` (= seq nhỏ nhất). `@onscroll` → cuộn gần đỉnh (≤60px) →
`LoadOlderAsync` tải thêm **5 tin cũ** (`before=cursor`), **chèn lên đầu** và **giữ nguyên vị trí cuộn**
(ghi chiều cao trước/sau qua JS `chatScroll` trong `wwwroot/js/chat.js`). Hết tin → `_olderCursor=null`.

### 9d. Gửi ảnh/video trong chat
Nút 📎 → upload qua `MediaApi` (MinIO, y như post) → `_pending` (preview) → gửi kèm `attachments` (url+mediaType).
Backend lưu `message_attachments`; `MessageResponse.Attachments` trả về; bong bóng render `<img>`/`<video>`.
Tin chỉ-media (không text) vẫn gửi được; danh sách hội thoại hiện "[Đính kèm]".

### 9e. Cửa sổ chat NỔI — `Components/ChatDock.razor`
Render trong `MainLayout` (toàn app). Nghe `Chat.MessageArrived`: có tin đến mà **KHÔNG ở trang /messages**
→ tự bật cửa sổ nổi góc dưới-trái cho hội thoại đó (nạp 20 tin gần đây) → chat trực tiếp không rời trang
(vừa lướt feed vừa nhắn). **Đủ tính năng như trang Messages**: gửi 📎 ảnh/video (preview + attachments),
cuộn lên tải 5 tin cũ (per-dock, giữ vị trí cuộn). Bấm tiêu đề = thu gọn, × = đóng; nhiều hội thoại =
nhiều cửa sổ. Vào /messages thì popup tự đóng (trang tự lo). Mỗi cửa sổ giữ state riêng trên class `Dock`.

### 10. Điểm chưa hoàn hảo (giới hạn để người học biết)
- Cấp seq load→tăng→save: 2 tin ĐỒNG THỜI (hiếm) có thể đụng `UNIQUE(conv,seq)` → lỗi hiếm, client retry.
  Bản chuẩn dùng `UPDATE ... RETURNING` trong transaction.
- Chưa có typing indicator, trạng thái sent/delivered/read, chat nhóm.
- Presence in-memory (1 instance); nhiều instance cần Redis.

### Thứ tự đọc code gợi ý cho người học
1. `db/schema.sql` (conversations / messages / message_attachments) → hiểu dữ liệu.
2. `Application/Chat/ChatService.cs` → nghiệp vụ (đặc biệt `SendMessageAsync` cấp seq + attachments).
3. `Api/Hubs/ChatHub.cs` + `PresenceTracker.cs` → realtime + fan-out + presence.
4. `Api/Controllers/ConversationsController.cs` → REST.
5. **`src/Web/Services/ChatConnection.cs`** → service kết nối DÙNG CHUNG (trái tim frontend chat).
6. `src/Web/Pages/Messages.razor` → khung chat + infinite scroll + gửi media.
7. `src/Web/Components/ChatDock.razor` → cửa sổ nổi.
8. `src/Web/Layout/MainLayout.razor` → khởi động kết nối + badge + render ChatDock.

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
- Chat realtime 1-1 đã chạy (SignalR: gửi/nhận, danh sách hội thoại, online). Chưa có: typing
  indicator, sent/delivered/read, chat nhóm, SignalR toàn app (presence chỉ khi ở trang Messages),
  presence qua Redis (multi-instance).
- Chưa có worker flush counter từ Redis (counter cập nhật trực tiếp trong request).
- Có trang profile + đổi avatar + follow + gợi ý follow. Chưa có màn **liệt kê** follower/following
  (mới có số đếm). Đổi avatar chưa cập nhật ngay ở sidebar (cần reload).
- Media hỗ trợ **ảnh + video** (lưu thẳng). Chưa encode video (FFmpeg qua queue), chưa resize ảnh
  (ImageSharp), chưa CDN, chưa range request cho video (seek) — đúng roadmap, để Phase sau.

## Skill gợi ý cho session sau
- Đi sâu kiến trúc / cải thiện: `improve-codebase-architecture`.
- Lập kế hoạch làm tiếp (chat, profile, media): `hd-planning` hoặc `hd-brainstorming`.
