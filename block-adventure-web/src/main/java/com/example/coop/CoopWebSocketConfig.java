package com.example.coop;

import org.springframework.context.annotation.Configuration;
import org.springframework.web.socket.config.annotation.EnableWebSocket;
import org.springframework.web.socket.config.annotation.WebSocketConfigurer;
import org.springframework.web.socket.config.annotation.WebSocketHandlerRegistry;

//--- 2026-07-01 협동 멀티플레이 WebSocket 엔드포인트 등록. WebGL과 같은 오리진(8888)에서 /ws/coop 제공.
@Configuration
@EnableWebSocket
public class CoopWebSocketConfig implements WebSocketConfigurer {

    private final CoopHandler handler;

    public CoopWebSocketConfig(CoopHandler handler) {
        this.handler = handler;
    }

    @Override
    public void registerWebSocketHandlers(WebSocketHandlerRegistry registry) {
        registry.addHandler(handler, "/ws/coop").setAllowedOrigins("*");
    }
}
