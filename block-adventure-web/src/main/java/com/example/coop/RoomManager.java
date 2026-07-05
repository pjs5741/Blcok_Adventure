package com.example.coop;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.springframework.stereotype.Component;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketSession;

import java.io.IOException;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicInteger;

//--- 2026-07-03 로비 매니저. 연결=로비 등록. 방 목록/생성/참가/퇴장 + 로비 채팅. 방/전투는 Room이 담당.
@Component
public class RoomManager {

    private final ObjectMapper mapper = new ObjectMapper();
    private final AtomicInteger counter = new AtomicInteger(1);
    private final Object lock = new Object();

    private final Map<String, WebSocketSession> sessions = new ConcurrentHashMap<>(); // sessionId -> session
    private final Map<String, String> shortIds = new ConcurrentHashMap<>();            // sessionId -> 표시용 짧은 ID
    private final Map<String, Room> rooms = new ConcurrentHashMap<>();                 // roomId -> Room
    private final Map<String, Room> roomBySession = new ConcurrentHashMap<>();          // sessionId -> Room

    // 연결 수립(afterConnectionEstablished) 시 로비 등록 — 클라의 전송 타이밍과 무관
    public void register(WebSocketSession s) {
        sessions.put(s.getId(), s);
        String sid = "P" + counter.getAndIncrement();
        shortIds.put(s.getId(), sid);
        send(s, obj("type", "hello", "id", sid));
        sendLobby(s);
    }

    public void unregister(WebSocketSession s) {
        synchronized (lock) {
            sessions.remove(s.getId());
            Room r = roomBySession.remove(s.getId());
            if (r != null) {
                r.onLeave(s);
                if (r.isEmpty()) rooms.remove(r.getId());
            }
        }
        broadcastLobby();
    }

    public void handle(WebSocketSession s, JsonNode node) {
        String type = node.path("type").asText("");
        switch (type) {
            case "createRoom" -> createRoom(s);
            case "joinRoom"   -> joinRoom(s, node.path("roomId").asText(""));
            case "leaveRoom"  -> leaveRoom(s);
            case "lobbyChat"  -> lobbyChat(s, node.path("text").asText(""));
            case "roomChat"   -> withRoom(s, r -> r.chat(s, node.path("text").asText("")));
            case "ready"      -> withRoom(s, r -> r.setReady(s, node.path("ready").asBoolean(false)));
            case "startGame"  -> withRoom(s, r -> r.startByHost(s));
            case "mapSelect"  -> withRoom(s, r -> r.onMapSelect(s, node));   // 방장 노드 선택
            case "mapEmote"   -> withRoom(s, r -> r.onMapEmote(s, node));    // 참여자 의견
            case "turnReady"  -> withRoom(s, r -> r.onTurnReady(s, node));
            case "grid"       -> withRoom(s, r -> r.onGrid(s, node));
            default -> { /* 무시 */ }
        }
    }

    // ---------- 방 ----------
    private void createRoom(WebSocketSession s) {
        synchronized (lock) {
            if (roomBySession.containsKey(s.getId())) return;
            String id = shortIds.get(s.getId());   // 방 ID = 방장 표시 ID
            Room r = new Room(id, s, id, mapper);
            rooms.put(id, r);
            roomBySession.put(s.getId(), r);
        }
        broadcastLobby();
    }

    private void joinRoom(WebSocketSession s, String roomId) {
        synchronized (lock) {
            if (roomBySession.containsKey(s.getId())) return;
            Room r = rooms.get(roomId);
            if (r == null || r.isFull()) { send(s, obj("type", "joinFail")); return; }
            r.addGuest(s, shortIds.get(s.getId()));
            roomBySession.put(s.getId(), r);
        }
        broadcastLobby();
    }

    private void leaveRoom(WebSocketSession s) {
        synchronized (lock) {
            Room r = roomBySession.remove(s.getId());
            if (r != null) {
                r.onLeave(s);
                if (r.isEmpty()) rooms.remove(r.getId());
            }
        }
        sendLobby(s);
        broadcastLobby();
    }

    private void withRoom(WebSocketSession s, java.util.function.Consumer<Room> action) {
        Room r = roomBySession.get(s.getId());
        if (r != null) action.accept(r);
    }

    // ---------- 로비 채팅 ----------
    private void lobbyChat(WebSocketSession s, String text) {
        if (text == null || text.isBlank()) return;
        Map<String, Object> msg = obj("type", "lobbyChat", "from", shortIds.get(s.getId()), "text", text);
        for (WebSocketSession p : sessions.values()) send(p, msg);
    }

    // ---------- 로비 상태 ----------
    private List<Map<String, Object>> roomList() {
        List<Map<String, Object>> list = new ArrayList<>();
        for (Room r : rooms.values())
            list.add(obj("id", r.getId(), "host", r.getHostId(), "count", r.playerCount(), "started", r.isStarted()));
        return list;
    }

    private void sendLobby(WebSocketSession s) {
        Map<String, Object> msg = new LinkedHashMap<>();
        msg.put("type", "lobby");
        msg.put("rooms", roomList());
        send(s, msg);
    }

    private void broadcastLobby() {
        // 방에 안 들어간 세션에게만 로비 목록 갱신
        List<Map<String, Object>> list = roomList();
        for (Map.Entry<String, WebSocketSession> e : sessions.entrySet()) {
            if (roomBySession.containsKey(e.getKey())) continue;
            Map<String, Object> msg = new LinkedHashMap<>();
            msg.put("type", "lobby");
            msg.put("rooms", list);
            send(e.getValue(), msg);
        }
    }

    // ---------- 유틸 ----------
    private Map<String, Object> obj(Object... kv) {
        Map<String, Object> m = new LinkedHashMap<>();
        for (int i = 0; i + 1 < kv.length; i += 2) m.put(String.valueOf(kv[i]), kv[i + 1]);
        return m;
    }

    private void send(WebSocketSession s, Map<String, Object> msg) {
        if (s == null || !s.isOpen()) return;
        try { s.sendMessage(new TextMessage(mapper.writeValueAsString(msg))); }
        catch (IOException ignored) { }
    }
}
