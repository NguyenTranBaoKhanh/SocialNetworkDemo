// Hỗ trợ cuộn cho khung chat (đọc/đặt vị trí cuộn để giữ chỗ khi chèn tin cũ).
window.chatScroll = {
    getTop: function (el) { return el ? el.scrollTop : 0; },
    getHeight: function (el) { return el ? el.scrollHeight : 0; },
    setTop: function (el, top) { if (el) el.scrollTop = top; },
    toBottom: function (el) { if (el) el.scrollTop = el.scrollHeight; },
    // Cuộn mọi cửa sổ chat nổi xuống đáy (đơn giản cho popup dock).
    docksToBottom: function () {
        document.querySelectorAll('.dock-messages').forEach(function (el) { el.scrollTop = el.scrollHeight; });
    }
};
