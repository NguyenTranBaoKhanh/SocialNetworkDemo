-- ============================================================================
-- Social Network — MVP schema (PostgreSQL)
-- Phase 1: users, posts, comments, likes, follows
-- Chuẩn quan hệ, dễ mở rộng sang notification/chat ở phase sau.
-- Quy ước:
--   - bigint identity làm PK; public_id uuid cho entity lộ ra client/URL.
--   - timestamptz + now() do server quyết định (không tin client).
--   - unique constraint chống double-like / double-follow ở tầng DB.
--   - like_count/comment_count là cache đọc, nguồn sự thật là bảng likes/comments.
-- ============================================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS citext;      -- so sánh email/username không phân biệt hoa thường

-- ---------------------------------------------------------------------------
-- USERS
-- Auth do ASP.NET Core Identity quản lý; bảng này giữ profile của mạng xã hội.
-- Nếu để Identity tự tạo AspNetUsers, có thể map identity_id sang bảng này.
-- ---------------------------------------------------------------------------
CREATE TABLE users (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    public_id       uuid        NOT NULL DEFAULT uuid_generate_v4(),
    username        citext      NOT NULL,                 -- @handle, duy nhất
    email           citext      NOT NULL,
    display_name    text        NOT NULL,
    bio             text        NOT NULL DEFAULT '',
    avatar_url      text,
    password_hash   text        NOT NULL,                 -- do Identity ghi
    -- Counter cache (nguồn sự thật là bảng follows). Worker/trigger cập nhật.
    follower_count  integer     NOT NULL DEFAULT 0,
    following_count integer     NOT NULL DEFAULT 0,
    post_count      integer     NOT NULL DEFAULT 0,
    is_active       boolean     NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT uq_users_username UNIQUE (username),
    CONSTRAINT uq_users_email    UNIQUE (email),
    CONSTRAINT uq_users_publicid UNIQUE (public_id)
);

-- ---------------------------------------------------------------------------
-- REFRESH TOKENS
-- Access token (JWT) stateless, sống ngắn. Refresh token sống dài, lưu ở DB để
-- THU HỒI được (logout, đổi mật khẩu). Chỉ lưu HASH (SHA-256), không lưu plaintext.
-- ---------------------------------------------------------------------------
CREATE TABLE refresh_tokens (
    id                     bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id                bigint      NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash             text        NOT NULL,             -- SHA-256 của token thô
    expires_at             timestamptz NOT NULL,
    revoked_at             timestamptz,                      -- NULL = còn hiệu lực
    replaced_by_token_hash text,                             -- token thay thế khi xoay vòng
    created_at             timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT uq_refresh_tokens_hash UNIQUE (token_hash)
);

CREATE INDEX idx_refresh_tokens_user ON refresh_tokens (user_id);

-- ---------------------------------------------------------------------------
-- POSTS
-- ---------------------------------------------------------------------------
CREATE TABLE posts (
    id            bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    public_id     uuid        NOT NULL DEFAULT uuid_generate_v4(),
    author_id     bigint      NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    content       text        NOT NULL DEFAULT '',
    -- Counter cache; flush từ Redis INCR xuống định kỳ.
    like_count    integer     NOT NULL DEFAULT 0,
    comment_count integer     NOT NULL DEFAULT 0,
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now(),
    deleted_at    timestamptz,                            -- soft delete

    CONSTRAINT uq_posts_publicid UNIQUE (public_id)
);

-- Feed "post của những người tôi follow": lọc theo author + sắp theo thời gian.
-- deleted_at IS NULL -> partial index để bỏ qua post đã xóa.
CREATE INDEX idx_posts_author_created
    ON posts (author_id, created_at DESC)
    WHERE deleted_at IS NULL;

-- ---------------------------------------------------------------------------
-- MEDIA (ảnh/video đính kèm post) — nhiều media cho một post
-- ---------------------------------------------------------------------------
CREATE TABLE post_media (
    id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    post_id     bigint      NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    url         text        NOT NULL,                     -- key trên S3/MinIO
    media_type  text        NOT NULL DEFAULT 'image',     -- 'image' | 'video'
    width       integer,
    height      integer,
    position    smallint    NOT NULL DEFAULT 0,           -- thứ tự hiển thị
    created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX idx_post_media_post ON post_media (post_id, position);

-- ---------------------------------------------------------------------------
-- COMMENTS — hỗ trợ trả lời lồng nhau (parent_id tự tham chiếu)
-- ---------------------------------------------------------------------------
CREATE TABLE comments (
    id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    post_id     bigint      NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    author_id   bigint      NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    parent_id   bigint      REFERENCES comments(id) ON DELETE CASCADE,  -- NULL = comment gốc
    content     text        NOT NULL,
    like_count  integer     NOT NULL DEFAULT 0,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now(),
    deleted_at  timestamptz
);

CREATE INDEX idx_comments_post_created
    ON comments (post_id, created_at)
    WHERE deleted_at IS NULL;

CREATE INDEX idx_comments_parent
    ON comments (parent_id)
    WHERE parent_id IS NOT NULL;

-- ---------------------------------------------------------------------------
-- LIKES — chống double-like bằng unique (user_id, post_id)
-- Bảng này là NGUỒN SỰ THẬT; posts.like_count chỉ là cache.
-- ---------------------------------------------------------------------------
CREATE TABLE likes (
    user_id    bigint      NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    post_id    bigint      NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    created_at timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT pk_likes PRIMARY KEY (user_id, post_id)
);

-- Đếm/liệt kê ai đã like một post.
CREATE INDEX idx_likes_post ON likes (post_id);

-- ---------------------------------------------------------------------------
-- FOLLOWS — quan hệ có hướng, chống double-follow, chống tự follow
-- ---------------------------------------------------------------------------
CREATE TABLE follows (
    follower_id  bigint      NOT NULL REFERENCES users(id) ON DELETE CASCADE, -- người đi follow
    followee_id  bigint      NOT NULL REFERENCES users(id) ON DELETE CASCADE, -- người được follow
    created_at   timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT pk_follows PRIMARY KEY (follower_id, followee_id),
    CONSTRAINT chk_follows_no_self CHECK (follower_id <> followee_id)
);

-- "Tôi đang follow ai" (build feed) và "ai follow tôi".
CREATE INDEX idx_follows_follower ON follows (follower_id);
CREATE INDEX idx_follows_followee ON follows (followee_id);

-- ============================================================================
-- CHAT
-- Nguyên tắc (theo CLAUDE.md):
--   - Persist message vào DB TRƯỚC khi ack cho client.
--   - Ordering trong 1 hội thoại bằng `seq` (số tăng dần per conversation),
--     KHÔNG dùng client timestamp.
--   - Trạng thái sent/delivered/read: theo dõi bằng last_read_seq/last_delivered_seq
--     trên từng thành viên (đủ cho cả 1-1 lẫn group, rẻ hơn ghi receipt mỗi message).
--   - Presence (ai online) + typing indicator: để trong REDIS với TTL, KHÔNG lưu DB.
-- ============================================================================

-- ---------------------------------------------------------------------------
-- CONVERSATIONS — 1-1 ('direct') hoặc nhóm ('group')
-- next_seq: bộ đếm sinh seq cho message kế tiếp trong hội thoại này.
-- ---------------------------------------------------------------------------
CREATE TABLE conversations (
    id               bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    public_id        uuid        NOT NULL DEFAULT uuid_generate_v4(),
    type             text        NOT NULL DEFAULT 'direct',   -- 'direct' | 'group'
    title            text,                                    -- chỉ dùng cho group
    -- Khóa duy nhất cho hội thoại 1-1 để không tạo trùng 2 conversation giữa
    -- cùng một cặp user. Quy ước: 'min(userA,userB):max(userA,userB)'. NULL với group.
    direct_key       text,
    next_seq         bigint      NOT NULL DEFAULT 1,          -- seq kế tiếp sẽ cấp
    last_message_at  timestamptz,                             -- để sắp xếp inbox
    created_at       timestamptz NOT NULL DEFAULT now(),
    updated_at       timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT uq_conversations_publicid  UNIQUE (public_id),
    CONSTRAINT uq_conversations_directkey UNIQUE (direct_key),
    CONSTRAINT chk_conversations_type     CHECK (type IN ('direct', 'group'))
);

-- Sắp xếp danh sách hội thoại theo tin nhắn mới nhất.
CREATE INDEX idx_conversations_last_message
    ON conversations (last_message_at DESC NULLS LAST);

-- ---------------------------------------------------------------------------
-- CONVERSATION_MEMBERS — ai thuộc hội thoại nào + con trỏ đọc/nhận
-- last_read_seq / last_delivered_seq đóng vai trò trạng thái read/delivered:
--   unread = messages có seq > last_read_seq của thành viên đó.
-- ---------------------------------------------------------------------------
CREATE TABLE conversation_members (
    conversation_id    bigint      NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
    user_id            bigint      NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role               text        NOT NULL DEFAULT 'member',  -- 'owner' | 'admin' | 'member'
    last_read_seq      bigint      NOT NULL DEFAULT 0,         -- đã đọc tới seq này
    last_delivered_seq bigint      NOT NULL DEFAULT 0,         -- đã nhận tới seq này
    is_muted           boolean     NOT NULL DEFAULT false,
    joined_at          timestamptz NOT NULL DEFAULT now(),
    left_at            timestamptz,                            -- NULL = còn trong hội thoại

    CONSTRAINT pk_conversation_members PRIMARY KEY (conversation_id, user_id)
);

-- "Những hội thoại của user X" (dựng inbox).
CREATE INDEX idx_conv_members_user
    ON conversation_members (user_id)
    WHERE left_at IS NULL;

-- ---------------------------------------------------------------------------
-- MESSAGES — ordering bằng seq trong phạm vi conversation
-- ---------------------------------------------------------------------------
CREATE TABLE messages (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    conversation_id bigint      NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
    sender_id       bigint      NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    seq             bigint      NOT NULL,                     -- thứ tự trong hội thoại
    content         text        NOT NULL DEFAULT '',
    -- client_msg_id: id tạm do client sinh, dùng để chống gửi trùng (idempotency)
    -- và map optimistic UI khi server ack.
    client_msg_id   uuid,
    edited_at       timestamptz,
    deleted_at      timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now(),

    -- Không có 2 message cùng seq trong một hội thoại -> ordering ổn định.
    CONSTRAINT uq_messages_conv_seq UNIQUE (conversation_id, seq)
);

-- Chống gửi trùng khi client retry (idempotency theo từng người gửi).
CREATE UNIQUE INDEX uq_messages_client_id
    ON messages (conversation_id, sender_id, client_msg_id)
    WHERE client_msg_id IS NOT NULL;

-- Đọc lịch sử hội thoại theo seq (phân trang cursor bằng seq).
CREATE INDEX idx_messages_conv_seq
    ON messages (conversation_id, seq DESC)
    WHERE deleted_at IS NULL;

-- ---------------------------------------------------------------------------
-- MESSAGE_ATTACHMENTS — ảnh/file đính kèm tin nhắn (tùy chọn)
-- ---------------------------------------------------------------------------
CREATE TABLE message_attachments (
    id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    message_id  bigint      NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
    url         text        NOT NULL,                         -- key trên S3/MinIO
    media_type  text        NOT NULL DEFAULT 'image',         -- 'image' | 'video' | 'file'
    file_name   text,
    file_size   bigint,
    created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX idx_message_attachments_message ON message_attachments (message_id);

-- ============================================================================
-- CẤP SEQ AN TOÀN KHI CHÈN MESSAGE (tránh race giữa nhiều instance):
--
--   -- Trong 1 transaction:
--   UPDATE conversations
--      SET next_seq = next_seq + 1,
--          last_message_at = now(),
--          updated_at = now()
--    WHERE id = :conversationId
--   RETURNING next_seq - 1 AS assigned_seq;
--
--   INSERT INTO messages (conversation_id, sender_id, seq, content, client_msg_id)
--   VALUES (:conversationId, :senderId, :assignedSeq, :content, :clientMsgId);
--
-- UPDATE ... RETURNING khóa hàng conversation nên hai message đồng thời không thể
-- nhận cùng seq. Chỉ SAU khi commit thành công mới ack cho client và fan-out
-- qua SignalR (group theo conversationId; nhiều instance -> BẮT BUỘC Redis backplane).
--
-- CHỪA SẴN CHO PHASE SAU:
--   notifications(id, recipient_id, actor_id, type, entity_id, is_read, created_at)
-- ============================================================================
