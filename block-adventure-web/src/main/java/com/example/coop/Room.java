package com.example.coop;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketSession;

import java.io.IOException;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Random;

//--- 2026-07-03 협동 방(2인). 로비→방→방장 시작→[공유 맵]→방장이 노드 선택→[공유 전투]→맵 복귀 반복.
// 맵은 클라가 시드로 생성(서버는 시드만 관리). 방장이 mapSelect로 진행(몹HP 동봉), 참여자는 mapEmote로 의견.
// 전투는 턴마다 데미지 합산 + 인텐트 브로드캐스트. 몹 처치 시 battleEnd로 맵 복귀.
public class Room {

    private static final long TURN_TIMEOUT_MS = 15000;
    //--- 2026-07-20 폴백용 기본 풀(구 클라 호환). 정상 흐름에선 방장이 몹별 풀(intentPool)을 전송하고 그걸 씀.
    //--- 2026-07-09 얼림/시한폭탄 추가 (판 회전은 협동 미지원 — 파트너 미니뷰 그리드 크기 고정 때문)
    //--- 2026-07-13 삼키기(Devour) 추가
    private static final String[] INTENTS = { "RowAttack", "RowAttack", "ConvertBlocks", "Blind", "Freeze", "TimeBomb", "Devour" };
    //--- 2026-07-20 솔로 Tuning.IntentCooldown(=3)과 동일: 특수 인텐트 재등장 쿨다운(픽 횟수)
    private static final int INTENT_COOLDOWN = 3;

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
    //--- 2026-07-20 이 전투 몹의 인텐트 풀(방장 전송, 없으면 INTENTS 폴백) + 특수 인텐트 쿨다운 (솔로 PickIntent와 동일 규칙)
    private String[] battleIntents;
    private final Map<String, Integer> intentCooldown = new HashMap<>();
    //--- 2026-07-20 다음 공격에 쓸 인텐트를 한 사이클 미리 정해둠(예고/텔레그래프용). resolve마다 nextIntent로 전송.
    private String pendingIntent = "None";

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
            //--- 2026-07-20 몹별 인텐트 풀(방장이 MonsterRegistry에서 뽑아 전송, Pivot 제외). 없으면 기본 INTENTS 폴백.
            JsonNode pool = node.path("intentPool");
            if (pool.isArray() && pool.size() > 0) {
                battleIntents = new String[pool.size()];
                for (int k = 0; k < pool.size(); k++) battleIntents[k] = pool.get(k).asText();
            } else {
                battleIntents = null;
            }
            intentCooldown.clear();
            pendingIntent = pickIntent();   //--- 2026-07-20 첫 공격 인텐트 미리 정함(전투 시작부터 예고 가능)
            inBattle = true;
            battleTurn = 0;
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

    //--- 2026-07-20 솔로 Monster.PickIntent와 동일 규칙: 몹별 풀에서 쿨다운 안 걸린 후보 중 랜덤(중복=RowAttack 가중치).
    // 픽 시점마다 쿨다운 1 감소, 특수 인텐트는 뽑히면 INTENT_COOLDOWN회 쿨다운. 후보 없으면 RowAttack 폴백.
    private String pickIntent() {
        String[] pool = (battleIntents != null && battleIntents.length > 0) ? battleIntents : INTENTS;
        for (Map.Entry<String, Integer> e : intentCooldown.entrySet())
            if (e.getValue() > 0) e.setValue(e.getValue() - 1);
        List<String> cand = new ArrayList<>();
        for (String it : pool)
            if (intentCooldown.getOrDefault(it, 0) <= 0) cand.add(it);
        String picked = cand.isEmpty() ? "RowAttack" : cand.get(rng.nextInt(cand.size()));
        if (!"RowAttack".equals(picked)) intentCooldown.put(picked, INTENT_COOLDOWN);
        return picked;
    }

    private void resolve() {
        int total = (turnReady[0] ? dmg[0] : 0) + (turnReady[1] ? dmg[1] : 0);
        monsterHp = Math.max(0, monsterHp - total);
        boolean dead = monsterHp <= 0;

        //--- 2026-07-03 공격 주기: attackInterval마다만 인텐트 발동(그 외 턴은 발동 None).
        //--- 2026-07-20 발동 인텐트는 지난 사이클에 미리 정해둔 pendingIntent(예고했던 것). 발동 후 다음 것을 새로 뽑아 예고.
        //    nextIntent를 매 resolve에 실어 보내 클라가 카운트다운 동안 예고(텔레그래프)하게 함 → 솔로와 동일.
        battleTurn++;
        String fireIntent = "None";
        if (battleTurn % coopAttackInterval == 0)
        {
            fireIntent = pendingIntent;       // 이번 턴 발동 = 예고했던 인텐트
            pendingIntent = pickIntent();     // 다음 공격 인텐트 미리 정함(예고용)
        }
        int attackCountdown = coopAttackInterval - (battleTurn % coopAttackInterval);   // 다음 공격까지 남은 턴
        for (int i = 0; i < 2; i++) {
            int partnerDmg = turnReady[1 - i] ? dmg[1 - i] : 0;   // 상대가 이번 턴 준 데미지(버디 공격 연출용)
            Map<String, Object> msg = obj("type", "resolve", "monsterHp", monsterHp,
                    "monsterMaxHp", monsterMaxHp, "monsterDead", dead, "intent", fireIntent,
                    "nextIntent", pendingIntent,   //--- 2026-07-20 다음 공격 예고(클라 텔레그래프)
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
