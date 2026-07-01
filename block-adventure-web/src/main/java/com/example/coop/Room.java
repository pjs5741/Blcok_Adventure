package com.example.coop;

import com.fasterxml.jackson.databind.JsonNode;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketSession;

import java.io.IOException;
import java.util.Random;

//--- 2026-07-01 협동 2인 룸. 공유 몬스터 + 턴 동기화.
// 각자 자기 그리드를 놓고 "ready"(데미지+그리드)를 보내면, 둘 다 준비됐을 때(또는 타임아웃) 서버가 턴을 해결한다:
//  공유 몬스터 HP -= (양쪽 데미지 합), 서버가 몬스터 인텐트를 정해 양쪽에 브로드캐스트, 상대 그리드도 전달.
// "세션마다 스레드": 룸마다 데몬 스레드가 돌며 턴 타임아웃/정리를 담당.
public class Room {

    private static final int COOP_MONSTER_HP = 600;        // 2인용 공유 몬스터 체력(튜닝값)
    private static final long TURN_TIMEOUT_MS = 15000;     // 한쪽이 늦어도 이 시간 지나면 진행
    private static final String[] INTENTS = { "RowAttack", "RowAttack", "ConvertBlocks", "Blind" };

    private final WebSocketSession[] players;
    private final boolean[] ready = new boolean[2];
    private final int[] dmg = new int[2];
    private final String[] grid = new String[2];           // 상대에게 넘겨줄 그리드(JSON 배열 문자열)

    private int monsterMaxHp = COOP_MONSTER_HP;
    private int monsterHp = COOP_MONSTER_HP;
    private long firstReadyAt = 0;
    private volatile boolean running = true;

    private final Random rng = new Random();
    private Thread thread;

    public Room(WebSocketSession p0, WebSocketSession p1) {
        players = new WebSocketSession[] { p0, p1 };
    }

    public WebSocketSession[] getPlayers() { return players; }

    public void start() {
        send(0, "{\"type\":\"joined\",\"playerId\":0}");
        send(1, "{\"type\":\"joined\",\"playerId\":1}");
        broadcast("{\"type\":\"start\",\"monsterHp\":" + monsterHp + ",\"monsterMaxHp\":" + monsterMaxHp + "}");
        thread = new Thread(this::loop, "coop-room");
        thread.setDaemon(true);
        thread.start();
    }

    private int indexOf(WebSocketSession s) {
        return s == players[0] ? 0 : (s == players[1] ? 1 : -1);
    }

    public synchronized void onReady(WebSocketSession s, JsonNode node) {
        int i = indexOf(s);
        if (i < 0 || !running) return;
        ready[i] = true;
        dmg[i] = node.path("damage").asInt(0);
        JsonNode g = node.get("grid");
        grid[i] = (g != null) ? g.toString() : "[]";
        if (firstReadyAt == 0) firstReadyAt = System.currentTimeMillis();

        if (ready[0] && ready[1]) resolve();
        else send(i, "{\"type\":\"waitPartner\"}");
    }

    // 룸 전용 스레드: 한쪽만 준비된 채로 타임아웃되면 강제 해결
    private void loop() {
        while (running) {
            try { Thread.sleep(200); } catch (InterruptedException e) { break; }
            synchronized (this) {
                if (firstReadyAt > 0 && !(ready[0] && ready[1])
                        && System.currentTimeMillis() - firstReadyAt > TURN_TIMEOUT_MS) {
                    resolve();
                }
            }
        }
    }

    // 반드시 synchronized 컨텍스트에서 호출
    private void resolve() {
        int total = (ready[0] ? dmg[0] : 0) + (ready[1] ? dmg[1] : 0);
        monsterHp = Math.max(0, monsterHp - total);
        boolean dead = monsterHp <= 0;
        String intent = INTENTS[rng.nextInt(INTENTS.length)];

        for (int i = 0; i < 2; i++) {
            String partner = (grid[1 - i] != null) ? grid[1 - i] : "[]";
            send(i, "{\"type\":\"resolve\",\"monsterHp\":" + monsterHp
                    + ",\"monsterMaxHp\":" + monsterMaxHp
                    + ",\"monsterDead\":" + dead
                    + ",\"intent\":\"" + intent + "\""
                    + ",\"partnerGrid\":" + partner + "}");
        }

        ready[0] = ready[1] = false;
        dmg[0] = dmg[1] = 0;
        firstReadyAt = 0;
        if (dead) running = false;
    }

    public void onLeave(WebSocketSession s) {
        running = false;
        broadcast("{\"type\":\"partnerLeft\"}");
    }

    static void sendRaw(WebSocketSession s, String json) {
        try { if (s != null && s.isOpen()) s.sendMessage(new TextMessage(json)); }
        catch (IOException ignored) { }
    }

    private void send(int i, String json) { sendRaw(players[i], json); }
    private void broadcast(String json) { send(0, json); send(1, json); }
}
