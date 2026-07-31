\# Social Network App — .NET



Context cho Claude Code. Project xây dựng một app mạng xã hội: post bài, like, comment,

follow, thông báo realtime và nhắn tin trực tiếp.



\---



\## Tech stack đã chọn



\### Backend

| Thành phần | Công nghệ | Ghi chú |

|---|---|---|

| Web framework | ASP.NET Core (.NET 10 LTS) | Minimal API hoặc Controller đều được, chọn một và giữ nhất quán |

| Realtime | SignalR | Chat + thông báo. Scale-out bắt buộc dùng Redis backplane |

| ORM | EF Core | Dapper cho query feed cần tối ưu tay |

| Auth | ASP.NET Core Identity + JWT | Cân nhắc Duende IdentityServer / Keycloak khi cần OAuth đầy đủ |

| Background job | `BackgroundService` / Hangfire | Hangfire nếu cần dashboard + retry policy |

| Local orchestration | .NET Aspire | Optional, tiện khi chạy nhiều service + Redis + Postgres cùng lúc |



\### Dữ liệu

\- \*\*PostgreSQL\*\* — user, post, comment, like, follow, conversation, message

\- \*\*Redis\*\* — cache feed, counter (like/view), presence (ai online), rate limit, SignalR backplane

\- \*\*S3 / Azure Blob + CDN\*\* — ảnh và video. Xử lý ảnh bằng `SixLabors.ImageSharp`, video bằng FFmpeg

\- \*\*OpenSearch / Elasticsearch\*\* — full-text search user + post (thêm ở phase sau, không phải MVP)



\### Client

\- Web: React / Next.js (hoặc Blazor nếu muốn full .NET)

\- Mobile: .NET MAUI hoặc Flutter

\- Cả hai dùng SignalR client cho realtime



\### Hạ tầng

\- Docker + Docker Compose (dev), Kubernetes (khi cần)

\- Message queue: RabbitMQ (đủ cho phần lớn trường hợp) hoặc Kafka (khi cần event log bền)

\- Observability: OpenTelemetry + Serilog, Grafana/Loki hoặc Seq

\- Push notification: FCM (Android/Web) + APNs (iOS)



\---



\## Nguyên tắc kiến trúc



1\. \*\*Bắt đầu bằng monolith.\*\* Một ASP.NET Core project, tách theo module/folder chứ chưa tách

&#x20;  service. Chỉ tách khi có phần cụ thể cần scale riêng.

2\. \*\*HTTP request phải nhanh.\*\* Mọi việc chậm (fan-out thông báo, encode video, gửi email,

&#x20;  cập nhật search index) đẩy vào queue, worker xử lý sau.

3\. \*\*Cache trước khi tối ưu query.\*\* Redis là tuyến phòng thủ đầu tiên, không phải index thứ 15.

4\. \*\*Không tin dữ liệu từ client.\*\* Timestamp, ordering, quyền truy cập đều xác thực ở server.



\---



\## Ghi chú triển khai từng feature



\### News feed

Bài toán khó nhất. Ba hướng:

\- \*Fan-out on write\* — khi A post, ghi post id vào feed list (Redis) của mọi follower.

&#x20; Đọc rất nhanh. Vấn đề: user có 1 triệu follower → 1 triệu lượt ghi mỗi post.

\- \*Fan-out on read\* — query post của những người user follow lúc mở app. Ghi nhẹ, đọc nặng.

\- \*Hybrid\* (nên dùng) — fan-out on write cho user thường, fan-out on read cho tài khoản

&#x20; có follower vượt ngưỡng (ví dụ > 10k).



MVP: làm fan-out on read trước với `LIMIT`/cursor pagination, đo thời gian, rồi mới tối ưu.



\### Like

\- Bảng `likes(user\_id, post\_id)` với \*\*unique constraint\*\* để chống double-like.

\- \*\*Không\*\* `UPDATE posts SET like\_count = like\_count + 1` mỗi request — sẽ lock contention.

&#x20; Dùng `INCR` trong Redis, worker định kỳ flush xuống DB.

\- Trả về optimistic UI ở client, rollback nếu server báo lỗi.



\### Thông báo

Luồng: API nhận action → publish event vào queue → trả về ngay.

Worker consume event → ghi bảng `notifications` → nếu user online thì bắn qua SignalR,

nếu offline thì gửi FCM/APNs.



Không bao giờ fan-out thông báo trong HTTP request.



\### Chat realtime

\- SignalR hub, group theo `conversationId`.

\- Persist message vào DB \*\*trước\*\* khi ack cho client.

\- Cần xử lý: trạng thái sent/delivered/read, typing indicator, message ordering

&#x20; (dùng server timestamp hoặc sequence number per conversation — không dùng client timestamp).

\- Presence lưu trong Redis với TTL, refresh bằng heartbeat.

\- Khi có nhiều instance: \*\*Redis backplane là bắt buộc\*\*, nếu không user connect vào

&#x20; server A sẽ không nhận được message gửi từ server B.



\---



\## Roadmap theo phase



\*\*Phase 1 — MVP (monolith)\*\*

\- ASP.NET Core + PostgreSQL + Redis + Blob storage

\- Auth, CRUD post, comment, like, follow

\- Feed đơn giản (fan-out on read)

\- Deploy: Docker trên một VPS



\*\*Phase 2 — Realtime\*\*

\- SignalR hub cho chat + thông báo

\- Redis backplane

\- Presence + typing indicator

\- Push notification khi offline



\*\*Phase 3 — Scale\*\*

\- Message queue cho fan-out thông báo và tạo feed

\- Feed cache trong Redis, chuyển sang hybrid fan-out

\- OpenSearch cho search

\- CDN cho media



\*\*Phase 4 — Vận hành\*\*

\- OpenTelemetry tracing, structured logging

\- Kubernetes nếu thực sự cần

\- Read replica cho PostgreSQL, cân nhắc sharding



\---



\## Những thứ nên tránh



\- Microservices từ ngày đầu

\- NoSQL cho dữ liệu quan hệ rõ ràng (user/post/follow là graph quan hệ, Postgres xử lý tốt)

\- Kafka khi RabbitMQ đã đủ

\- Kubernetes khi một VPS đã đủ

\- Query `LIKE '%keyword%'` cho search (không dùng được index)

\- Xử lý upload/encode media đồng bộ trong request



\---



\## Cấu trúc project gợi ý



```

src/

&#x20; Api/                    # ASP.NET Core host, endpoints, SignalR hubs

&#x20; Application/            # use case, DTO, validation

&#x20; Domain/                 # entity, value object, business rule

&#x20; Infrastructure/         # EF Core, Redis, blob storage, queue publisher

&#x20; Worker/                 # background worker consume queue

tests/

&#x20; UnitTests/

&#x20; IntegrationTests/       # dùng Testcontainers cho Postgres + Redis

docker-compose.yml        # postgres, redis, rabbitmq, minio

```



\---



\## Quyết định còn để mở



\- \[ ] Web frontend: React/Next.js hay Blazor?

\- \[ ] Mobile: MAUI hay Flutter?

\- \[ ] Cloud provider: Azure (tích hợp .NET tốt nhất) hay AWS hay self-hosted VPS?

\- \[ ] Kích thước target: mấy nghìn user hay mấy trăm nghìn? Quyết định này ảnh hưởng

&#x20;     rất nhiều tới việc có cần queue/sharding ở phase nào.



\---



\## Tham khảo: stack của các app lớn



| App | Ngôn ngữ | Đặc trưng |

|---|---|---|

| Facebook | Hack/HHVM, C++, Python | MySQL sharded + TAO graph cache, Memcached, RocksDB, React + GraphQL |

| Instagram | Python/Django | PostgreSQL sharded, Cassandra, Celery + RabbitMQ |

| LinkedIn | Java/Scala | Kafka (do LinkedIn tạo), Espresso, Samza, Pinot |

| X (Twitter) | Scala/Java | Finagle RPC, Manhattan KV store, Redis timeline cache |

| Discord | Elixir + Rust | Elixir cho realtime WebSocket, ScyllaDB |

| WhatsApp | Erlang | Đội nhỏ, phục vụ hàng trăm triệu user |



Điểm chung: không ai bắt đầu bằng microservices. Tất cả đều dựa vào cache mạnh,

queue để đẩy việc chậm ra khỏi request, và sharding database khi cần.

