package com.example.coop;

import com.fasterxml.jackson.databind.JsonNode;
import org.springframework.stereotype.Component;
import org.springframework.web.socket.WebSocketSession;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

//--- 2026-07-01 협동 매칭/룸 관리. 대기자 1명 있으면 다음 접속자와 2인 룸 생성(룸마다 세션 스레드는 Room이 돌림).
@Component
public class RoomManager {

    private final Map<String, Room> roomBySession = new ConcurrentHashMap<>();
    private WebSocketSession waiting;              // 짝을 기다리는 세션(최대 1)
    private final Object lock = new Object();

    public void join(WebSocketSession session) {
        synchronized (lock) {
            if (waiting == null || !waiting.isOpen() || waiting.getId().equals(session.getId())) {
                waiting = session;
                Room.sendRaw(session, "{\"type\":\"waiting\"}");
                return;
            }
            Room room = new Room(waiting, session);
            roomBySession.put(waiting.getId(), room);
            roomBySession.put(session.getId(), room);
            waiting = null;
            room.start();
        }
    }

    public void onReady(WebSocketSession session, JsonNode node) {
        Room room = roomBySession.get(session.getId());
        if (room != null) room.onReady(session, node);
    }

    public void leave(WebSocketSession session) {
        synchronized (lock) {
            if (waiting == session) waiting = null;
        }
        Room room = roomBySession.remove(session.getId());
        if (room != null) {
            // 상대 세션 매핑도 정리
            for (WebSocketSession p : room.getPlayers())
                if (p != null) roomBySession.remove(p.getId());
            room.onLeave(session);
        }
    }
}
