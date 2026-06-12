# block-adventure-web

Block Adventure WebGL 빌드를 서빙하는 Spring Boot 앱.

## 실행

```bash
cd block-adventure-web
./mvnw spring-boot:run
```

또는 IntelliJ에서 `Application.java` 실행.

접속: http://localhost:8080/

## 구조

- `src/main/resources/static/` — Unity WebGL 빌드 결과물 (`index.html`, `Build/`, `TemplateData/`)
- `BrotliHeaderFilter` — `.br` 파일에 `Content-Encoding: br` 헤더 추가 (Unity가 기본 Brotli 압축)

## 새 빌드 반영

Unity에서 새로 빌드한 뒤:
```bash
rm -rf src/main/resources/static/*
cp -R ../Block_Adventure/Build/WebGL/* src/main/resources/static/
```

Spring 재시작.
