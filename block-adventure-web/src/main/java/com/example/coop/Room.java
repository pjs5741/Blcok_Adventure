package com.example.coop;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketSession;

import java.io.IOException;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Random;

//--- 2026-07-03 협동 방(2인). 로비→방→방장 시작→[공유 맵]→방장이 노드 선택→[공유 전투]→맵 복귀 반복.
// 맵은 클라가 시드로 생성(서버는 시드만 관리). 방장이 mapSelect로 진행(몹HP 동봉), 참여자는 mapEmote로 의견.
// 전투는 턴마다 데미지 합산 + 인텐트 브로드캐스트. 몹 처치 시 battleEnd로 맵 복귀.
public class Room {

    private static final long TURN_TIMEOUT_MS = 15000;
    private static final String[] INTENTS = { "RowAttack", "RowAttack", "ConvertBlocks", "Blind" };

    private final String id;
    private final ObjectMapper mapper;

    private final WebSocketSession[] players = new WebSocketSession[2]; // 0=방장, 1=참여자
    private final String[] shortIds = new String[2];
    private final boolean[] roomReady = new boolean[2];
    private final boolean[] nodeReady = new boolean[2];   //--- 2026-07-07 현재 노드 완료(맵 복귀). 둘 다여야 다음 노드 진행
    private boolean started;      // 게임 시작(맵 단계 진입)

    // 맵/전투 상태
    private int mapSeed;
    private int currentNodeId = -1;
    private boolean inBattle;
    private int monsterHp, monsterMaxHp;
    private int monsterSeed;              // 몬스터 프로필 동기화 시드
    private int coopAttackInterval = 3;   // 공격 주기(방장이 전달)
    private int battleTurn;               // 현재 전투의 진행 턴
    private int lastIntentIdx = -1;       // 직전 인텐트(연속 방지)

    private final boolean[] turnReady = new boolean[2];
    private final int[] dmg = new int[2];
    private final JsonNode[] grid = new JsonNode[2];
    private long firstReadyAt;

    private volatile boolean running;   // 방 스레드 생존
    private final Random rng = new Random();
    private Thread thread;

    public Room(String id, WebSocketSession host, String hostId, ObjectMapper mapper) {
        this.id = id;
        this.mapper = mapper;
        players[0] = host;
        shortIds[0] = hostId;
        send(0, obj("type", "roomJoined", "roomId", id, "youAreHost", true, "playerIndex", 0));
        sendRoomUpdate();
    }

    public String getId() { return id; }
    public String getHostId() { return shortIds[0]; }
    public boolean isStarted() { return started; }
    public boolean isFull() { return players[0] != null && players[1] != null; }
    public boolean isEmpty() { return players[0] == null && players[1] == null; }
    public int playerCount() { return (players[0] != null ? 1 : 0) + (players[1] != null ? 1 : 0); }

    public void addGuest(WebSocketSession guest, String guestId) {
        players[1] = guest;
        shortIds[1] = guestId;
        send(1, obj("type", "roomJoined", "roomId", id, "youAreHost", false, "playerIndex", 1));
        sendRoomUpdate();
    }

    public synchronized void setReady(WebSocketSession s, boolean ready) {
        int i = indexOf(s);
        if (i < 0) return;
        roomReady[i] = ready;
        sendRoomUpdate();
    }

    public void chat(WebSocketSession s, String text) {
        if (text == null || text.isBlank()) return;
        int i = indexOf(s);
        if (i < 0) return;
        Map<String, Object> msg = obj("type", "roomChat", "from", shortIds[i], "text", text);
        send(0, msg); send(1, msg);
    }

    // 방장 시작 → 공유 시드 전달, 맵 단계로
    public synchronized void startByHost(WebSocketSession s) {
        if (indexOf(s) != 0 || started) return;
        if (!isFull() || !roomReady[0] || !roomReady[1]) return;
        started = true;
        mapSeed = rng.nextInt();
        for (int i = 0; i < 2; i++)
            send(i, obj("type", "gameStart", "seed", mapSeed, "playerIndex", i));
        running = true;
        thread = new Thread(this::loop, "coop-room-" + id);
        thread.setDaemon(true);
        thread.start();
    }

    // ---------- 맵 ----------
    // 방장이 다음 노드 선택(몹HP는 방장이 노드 타입 보고 계산해 동봉) → 전투 시작
    public synchronized void onMapSelect(WebSocketSession s, JsonNode node) {
        if (indexOf(s) != 0 || !started || inBattle) return;   // 방장만, 전투 중 아님
        //--- 2026-07-07 둘 다 현재 노드 완료(맵 복귀)해야 다음 진행. 아니면 방장에게 대기 알림.
        if (!(nodeReady[0] && nodeReady[1])) { send(0, obj("type", "waitPartnerNode")); return; }
        nodeReady[0] = nodeReady[1] = false;   // 새 노드 진입 → 둘 다 다시 완료해야 함
        currentNodeId = node.path("nodeId").asInt(-1);
        monsterSeed = node.path("monsterSeed").asInt(0);
        boolean isBattle = node.path("isBattle").asBoolean(true);
        if (isBattle) {
            monsterMaxHp = Math.max(1, node.path("monsterHp").asInt(600));
            monsterHp = monsterMaxHp;
            coopAttackInterval = Math.max(1, node.path("attackInterval").asInt(3));
            inBattle = true;
            battleTurn = 0; lastIntentIdx = -1;
            turnReady[0] = turnReady[1] = false; dmg[0] = dmg[1] = 0; firstReadyAt = 0;
        }
        // 전투/비전투 모두 goNode 전송 → 클라가 노드 타입 보고 씬(전투/상점/휴식/이벤트) 라우팅. 비전투면 턴 루프 없음.
        for (int i = 0; i < 2; i++)
            send(i, obj("type", "goNode", "nodeId", currentNodeId,
                    "monsterHp", monsterHp, "monsterMaxHp", monsterMaxHp, "monsterSeed", monsterSeed,
                    "attackCountdown", coopAttackInterval));
    }

    // 노드 완료(맵 복귀) 신호. 둘 다 완료되면 진행 가능 알림.
    public synchronized void onNodeDone(WebSocketSession s) {
        int i = indexOf(s);
        if (i < 0) return;
        nodeReady[i] = true;
        if (nodeReady[0] && nodeReady[1]) { send(0, obj("type", "advanceReady")); send(1, obj("type", "advanceReady")); }
    }

    // 참여자(또는 누구든) 노드 클릭 → 의견 이모트 브로드캐스트
    public synchronized void onMapEmote(WebSocketSession s, JsonNode node) {
        int i = indexOf(s);
        if (i < 0 || !started) return;
        Map<String, Object> msg = obj("type", "emote", "from", shortIds[i], "nodeId", node.path("nodeId").asInt(-1));
        send(0, msg); send(1, msg);
    }

    // ---------- 전투 턴 동기화 ----------
    public synchronized void onTurnReady(WebSocketSession s, JsonNode node) {
        int i = indexOf(s);
        if (i < 0 || !inBattle) return;
        turnReady[i] = true;
        dmg[i] = node.path("damage").asInt(0);
        grid[i] = node.get("grid");
        if (firstReadyAt == 0) firstReadyAt = System.currentTimeMillis();
        if (turnReady[0] && turnReady[1]) resolve();
        else send(i, obj("type", "waitPartner"));
    }

    public synchronized void onGrid(WebSocketSession s, JsonNode node) {
        int i = indexOf(s);
        if (i < 0 || !inBattle) return;
        Map<String, Object> msg = obj("type", "grid");
        msg.put("partnerGrid", node.get("grid"));
        send(1 - i, msg);
    }

    private void loop() {
        while (running) {
            try { Thread.sleep(200); } catch (InterruptedException e) { break; }
            synchronized (this) {
                if (inBattle && firstReadyAt > 0 && !(turnReady[0] && turnReady[1])
                        && System.currentTimeMillis() - firstReadyAt > TURN_TIMEOUT_MS) resolve();
            }
        }
    }

    private void resolve() {
        int total = (turnReady[0] ? dmg[0] : 0) + (turnReady[1] ? dmg[1] : 0);
        monsterHp = Math.max(0, monsterHp - total);
        boolean dead = monsterHp <= 0;

        //--- 2026-07-03 공격 주기: 매 턴이 아니라 attackInterval마다만 인텐트 발동(그 외엔 None). 연속 인텐트 방지.
        battleTurn++;
        String intent = "None";
        if (battleTurn % coopAttackInterval == 0)
        {
            int idx;
            do { idx = rng.nextInt(INTENTS.length); } while (INTENTS.length > 1 && idx == lastIntentIdx);
            lastIntentIdx = idx;
            intent = INTENTS[idx];
        }
        int attackCountdown = coopAttackInterval - (battleTurn % coopAttackInterval);   // 다음 공격까지 남은 턴
        for (int i = 0; i < 2; i++) {
            int partnerDmg = turnReady[1 - i] ? dmg[1 - i] : 0;   // 상대가 이번 턴 준 데미지(버디 공격 연출용)
            Map<String, Object> msg = obj("type", "resolve", "monsterHp", monsterHp,
                    "monsterMaxHp", monsterMaxHp, "monsterDead", dead, "intent", intent,
                    "partnerDamage", partnerDmg, "attackCountdown", attackCountdown);
            msg.put("partnerGrid", grid[1 - i]);
            send(i, msg);
        }
        turnReady[0] = turnReady[1] = false;
        dmg[0] = dmg[1] = 0;
        firstReadyAt = 0;
        if (dead) {
            inBattle = false;
            Map<String, Object> end = obj("type", "battleEnd", "nodeId", currentNodeId);
            send(0, end); send(1, end);   // 클라가 맵 복귀(보스면 종료는 클라가 판단)
        }
    }

    public void onLeave(WebSocketSession s) {
        int i = indexOf(s);
        if (i < 0) return;
        players[i] = null;
        shortIds[i] = null;
        roomReady[i] = false;
        running = false;
        Map<String, Object> msg = obj("type", "partnerLeft");
        send(0, msg); send(1, msg);
    }

    // ---------- 유틸 ----------
    private int indexOf(WebSocketSession s) {
        return s == players[0] ? 0 : (s == players[1] ? 1 : -1);
    }

    private void sendRoomUpdate() {
        Map<String, Object> msg = obj("type", "roomUpdate",
                "host", shortIds[0], "guest", shortIds[1],
                "hostReady", roomReady[0], "guestReady", roomReady[1]);
        send(0, msg); send(1, msg);
    }

    private Map<String, Object> obj(Object... kv) {
        Map<String, Object> m = new LinkedHashMap<>();
        for (int k = 0; k + 1 < kv.length; k += 2) m.put(String.valueOf(kv[k]), kv[k + 1]);
        return m;
    }

    private void send(int i, Map<String, Object> msg) {
        WebSocketSession s = players[i];
        if (s == null || !s.isOpen()) return;
        try { s.sendMessage(new TextMessage(mapper.writeValueAsString(msg))); }
        catch (IOException ignored) { }
    }
}
