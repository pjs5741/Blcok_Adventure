// 2026-07-01 WebGL용 브라우저 WebSocket 브리지. Unity WebGL은 C# 네이티브 소켓이 안 되므로 브라우저 WebSocket을 사용.
// 수신/상태는 SendMessage로 Unity GameObject "CoopClient"의 메서드를 호출해 전달.
mergeInto(LibraryManager.library, {
  CoopConnect: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    try {
      window.coopWs = new WebSocket(url);
      window.coopWs.onopen    = function ()  { SendMessage('CoopClient', 'OnSocketOpen'); };
      window.coopWs.onmessage = function (e) { SendMessage('CoopClient', 'OnSocketMessage', e.data); };
      window.coopWs.onclose   = function ()  { SendMessage('CoopClient', 'OnSocketClose'); };
      window.coopWs.onerror   = function ()  { SendMessage('CoopClient', 'OnSocketClose'); };
    } catch (err) {
      SendMessage('CoopClient', 'OnSocketClose');
    }
  },

  CoopSend: function (msgPtr) {
    var msg = UTF8ToString(msgPtr);
    if (window.coopWs && window.coopWs.readyState === 1) window.coopWs.send(msg);
  },

  CoopClose: function () {
    if (window.coopWs) {
      try { window.coopWs.close(); } catch (e) {}
      window.coopWs = null;
    }
  }
});
