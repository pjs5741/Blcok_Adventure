package com.example.coop;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.springframework.stereotype.Component;
import org.springframework.web.socket.CloseStatus;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketSession;
import org.springframework.web.socket.handler.TextWebSocketHandler;

//--- 2026-07-03 협동 메시지 핸들러. 연결 수립 시 로비 자동 등록(클라 전송 타이밍 불필요). 메시지는 RoomManager가 처리.
@Component
public class CoopHandler extends TextWebSocketHandler {

    private final RoomManager rooms;
    private final ObjectMapper mapper = new ObjectMapper();

    public CoopHandler(RoomManager rooms) {
        this.rooms = rooms;
    }

    @Override
    public void afterConnectionEstablished(WebSocketSession session) {
        rooms.register(session);   // 연결되는 즉시 로비 등록 → "hello"(내 ID) + 로비 목록 전송
    }

    @Override
    protected void handleTextMessage(WebSocketSession session, TextMessage message) throws Exception {
        JsonNode node = mapper.readTree(message.getPayload());
        rooms.handle(session, node);
    }

    @Override
    public void afterConnectionClosed(WebSocketSession session, CloseStatus status) {
        rooms.unregister(session);
    }
}
