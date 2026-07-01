package com.example.coop;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.springframework.stereotype.Component;
import org.springframework.web.socket.CloseStatus;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketSession;
import org.springframework.web.socket.handler.TextWebSocketHandler;

//--- 2026-07-01 협동 멀티 메시지 핸들러. join(매칭) / ready(턴 준비+데미지+그리드) / leave. 나머지 로직은 Room/RoomManager.
@Component
public class CoopHandler extends TextWebSocketHandler {

    private final RoomManager rooms;
    private final ObjectMapper mapper = new ObjectMapper();

    public CoopHandler(RoomManager rooms) {
        this.rooms = rooms;
    }

    @Override
    protected void handleTextMessage(WebSocketSession session, TextMessage message) throws Exception {
        JsonNode node = mapper.readTree(message.getPayload());
        String type = node.path("type").asText("");
        switch (type) {
            case "join"  -> rooms.join(session);
            case "ready" -> rooms.onReady(session, node);
            case "leave" -> rooms.leave(session);
            default      -> { /* 무시 */ }
        }
    }

    @Override
    public void afterConnectionClosed(WebSocketSession session, CloseStatus status) {
        rooms.leave(session);
    }
}
