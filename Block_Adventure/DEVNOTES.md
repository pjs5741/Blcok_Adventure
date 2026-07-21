# Block Adventure — 개발 기법 / 아키텍처 노트

이 프로젝트에서 반복적으로 쓰는 **기법·패턴·파이프라인·함정**을 한곳에 정리한 문서.
(클라 = Unity 2D `Block_Adventure/`, 서버 = Spring `block-adventure-web/`)

---

## 1. 튜닝/데이터 단일 소스

- **`FxTuning.asset`(ScriptableObject)** = 연출값 + 애니메이션 커브의 실시간 튜닝 단일 소스. 코드에서 `FxTuning.I`로 접근. 인스펙터에서 플레이 중 조정 가능.
- **`Tuning.cs`** = 밸런스 상수(데미지 배수, 쿨다운, 넉백 임계 등). `const`라 컴파일 타임 고정.
- **직렬화 필드 함정 (중요)**: 씬/에셋에 **이미 저장된 값이 코드 기본값을 덮어씀**. 그래서 `pivotDuration`·`monsterTelegraph` 등은 직렬화 필드를 버리고 **프로퍼티(`=> Tuning.X`)로 고정**해 씬 잔재값을 무시하게 함.
- **에셋에 없는 신규 필드 함정**: `FxTuning.asset`은 특정 시점까지의 필드만 직렬화돼 있음. 이후 추가한 필드(예: `corruptMarkColor`, `devour*`)는 에셋 YAML에 없어 **C# 초기값에 의존** → 인스펙터 튜닝이 안 됨. 값 바꾸려면 `FxTuning.cs` 초기값을 고치거나, 에디터에서 에셋을 한 번 터치해 재저장. **바꾸기 전 `grep`으로 그 필드가 `.asset`에 실제 있는지 확인할 것.**

## 2. 턴 루프 & 페이즈 구조 (`GameManager.cs`)

- `GameState`: PlayerTurn → Calculate → (Dot) → MonsterTurn → **Dead** → Reward / GameOverCheck.
- 각 페이즈는 코루틴. 루프는 `while (!종료 && !_coopEnded)`.
- **데드 페이즈 기법**: 몬스터가 죽으면 `Monster.IsDead`가 즉시 true(오브젝트 비활성 전). `DeadPhase()`가 `IsDeathPlaying`(데스 애니 재생 중)이 끝날 때까지 대기 후 보상 → 씬 전환. "죽어가는 중 다음 블록 스폰/몬스터 재공격" 버그 방지.
- `HasLivingMonster()`는 `activeSelf`가 아니라 **`!IsDead` 기준** (데스 모션 재생 중을 "살아있음"으로 오판하지 않게).

## 3. 몬스터 인텐트(패턴) 시스템 (`Monster.cs`, `MonsterRegistry.cs`)

- **풀 + 가중치**: 몹별 `intentPool`(배열). **중복 = 가중치**(RowAttack 2개 = 높은 빈도).
- **쿨다운**: 특수 인텐트는 뽑히면 `Tuning.IntentCooldown`(=3)회 재등장 금지. RowAttack은 필러라 연속 허용.
- **예고(텔레그래프)**: Devour/Convert는 인텐트 선택 시점에 대상 칸을 확정해 판에 마커 표시(`TelegraphMark`) → 카운트다운 동안 보여주고, 그 칸을 미리 지우면 회피(카운터플레이).
- **모션 분류**: `IsCastIntent()`로 몸으로 때리는 패턴(기본공격) vs 판 조작 패턴(Cast). 클립 없으면 `AnimHelper.TriggerOrFallback`로 기본공격 폴백.
- **새 패턴 추가 시 손대는 곳** (자동 아님): `MonsterIntent` enum → `IntentTitle/IntentDesc` → `IsCastIntent` → `RefreshTelegraph`(필요시) → `AttackCoroutine` switch → `BlockGrid`에 `ApplyXxx` 구현 → `Tuning`/`FxTuning` 값 → `MonsterRegistry` 풀 → (튜토리얼 필요시 `ContextTutorial`) → **서버 협동은 클라가 풀을 보내므로 서버 수정 불필요**(9번 참고).

## 4. 연출(Feel) 기법

- **`PivotActor.extraOffset` 포물선 이동**: transform을 직접 움직이면 `PivotActor.LateUpdate`가 덮어씀 → 대신 `extraOffset`을 a→b로 SmoothStep 보간 + 정점 아치(`arc`). 피벗 점프가격·삼키기·(예정)넉백에 재사용.
- **스케일 성장 연출**: 광폭화(`EnrageRoutine`)·삼키기 성장(`DevourGrowRoutine`)은 `localScale`을 Lerp. 광폭화는 기본색(`_baseColor`)까지 바꿔 이후 피격/독펄스 복귀색을 유지.
- **피격 틴트**: `HitEffect()`가 빨강 깜빡 후 `_baseColor` 복귀. 독은 보라 사인펄스(`Update`), 피격 중(`_hitting`)엔 양보.
- **HP바 보간**: 매 프레임 목표치로 Lerp(피격/사망과 무관).
- **카메라 셰이크**: `CameraShake.Instance.TriggerShake(dur, strength)` — 광폭화/폭발/성장에 사용.

## 5. 캐릭터 아트 파이프라인

**흐름**: ChatGPT 컨셉(1024px) → PIL로 64×64 NEAREST 다운스케일 → PixelLab "Animate with Text"로 애니 → `.pxo`(Pixelorama zip) → **프레임 추출로 가로 스트립 PNG** → `Assets/Art/<이름>/<이름>_<상태>.png` → **자동 빌더**.

- **`.pxo` → 스트립 변환 기법**: `.pxo`는 zip. `data.json`(프레임수/크기) + `image_data/frames/N/layer_1`(raw RGBA 64×64×4). PIL로 `Image.frombytes("RGBA",(64,64),bytes)` 후 가로 타일. (스크립트 예: scratchpad `pxo2strip.py`)
- **자동 빌더** (`MonsterAnimBuilder.cs` / `PlayerAnimBuilder.cs`): `Build/Monster Anim — <몹> 생성` 메뉴 → 스트립을 **코드로 슬라이스**(`ISpriteEditorDataProvider`, 64px 셀, PPU 16, Point 필터) → `.anim` 클립 + AnimatorController 자동 생성(→ `Resources/MonsterAnim/<몹>/`).
- **명명 규칙 필수 일치**: 상태명 `Idle/Basic_Attack/Cast_Attack/Hit/Death`, 트리거 `basicAttack/castAttack/hit/knockBack/death`. 코드(`Monster.cs`, `BattleManager` 필드)와 반드시 일치. `knockBack`은 전용 클립 없어 Hit 재사용(→ 넉백 TODO).
- **프로필 교체**: `MonsterProfile.animPath`(Resources 경로) + `artScale`로 몹별 컨트롤러 스왑(`Monster.SetProfile`). 일반 0.65 / 보스 0.9.
- **주의**: AI 애니는 "gentle/softly"가 졸음 유발 → "alert, eyes OPEN, head stays still" 명시. 무기 스윙은 주어를 캐릭터(전신)로("steps forward, torso twists") — 안 그러면 무기만 들썩. 몸 가로지르는 스윙은 손 바뀜 아티팩트 유발 → 내려치기/같은쪽 유지 + "held in the RIGHT hand in every frame".

## 6. UI 동적 생성

- **`UIBuilder`**: 코드로 UI 조립(`Text/Button/NewUI/SetAnchors/Stretch`). 씬에 없는 버튼도 런타임 생성(예: 보상 골드 버튼).
- **`UIScale.FixAll()`**: 창 크기 달라도 캔버스 스케일 통일(글씨 안 깨지게). 씬 진입 시 호출.
- **슬롯 배치 기법**(`RewardManager.RealignButtons`): 활성 버튼만 모아 위에서부터 앵커로 슬롯 채움. 고정 버튼(스킵)은 코너에 두고 리스트 x범위를 겹치지 않게 제한.
- **툴팁**: `TooltipTarget`(title/body) + 월드스페이스 캔버스면 `GraphicRaycaster`/`worldCamera` 보장.
- **UI 겹침 주의**: 텍스트 앵커의 **세로 범위가 겹치면 글씨가 포개짐** — 이름/가격 등은 y밴드를 분리.

## 7. 컨텍스트 튜토리얼 (`ContextTutorial.cs`)

- 상황이 "처음 일어나는 순간" `Time.timeScale=0`으로 멈추고 화면 어둡게 + 해당 위치만 **원형 스포트라이트**(절차 생성 텍스처, 구멍+소프트 가장자리) + 설명. 클릭하면 계속.
- **행동별 평생 1회**(PlayerPrefs `CtxTuto_<key>`). 인트로는 `tutorialSeen`.
- 리셋: `Build/Reset Tutorials (Intro + Context)` 메뉴 → `ContextTutorial.ResetAll()`(인트로 키까지 삭제).
- **협동에선 비활성**(일시정지가 상대 턴 타임아웃 유발).

## 8. 런/세이브 상태 (`Run.cs`)

- `Run` = 정적 런 상태(stats, ownedRelics, deck, mapState, **gridSnapshot**, lastResult). 씬 넘어가도 유지.
- 그리드 저장/복원으로 전투 간 판이 이어짐(솔로·협동 공통).

## 9. 협동 멀티 아키텍처 (`Multiplayer/` + 서버 `coop/`)

- **전송 계층 자동 분기**: 에디터/스탠드얼론=네이티브 `ClientWebSocket`, WebGL=브라우저 WebSocket(.jslib). 수신은 큐→메인스레드(`Update`) 디스패치.
- **서버 권위**: 몬스터 HP/공격 카운트다운/인텐트는 서버(`Room.java`)가 결정. 클라는 각자 자기 그리드 조작, 데미지만 서버로.
- **턴 동기화(resolve)**: 두 클라가 `turnReady`(데미지+그리드) 보내면 서버가 합산→몹 HP 갱신→인텐트 결정→양쪽에 `resolve` 브로드캐스트(15초 타임아웃 폴백). 몹 사망 시 `battleEnd`.
- **시드 동기**: 몹 프로필은 시드로 양쪽 동일 스폰(`MonsterRegistry.GetForNode(type, seed)`).
- **⚠️ C#/Java `Random` 시퀀스 다름**: 서버가 시드로 같은 몹을 재현 못 함 → **클라(방장)가 고른 몹의 인텐트 풀을 서버로 전송**(`mapSelect.intentPool`), 서버는 그 풀로 솔로와 동일 규칙(쿨다운/가중치) 선택. → **MonsterRegistry가 단일 소스**(서버에 풀 데이터 중복 없음).
- **실시간 그리드 전송**: 최대 5회/초로 체크하되 **직전과 달라졌을 때만** 전송(가만있으면 0회) → 파트너 미니뷰.
- **협동 제외**: `Pivot`(판 회전) — 그리드 가로/세로가 바뀌면 파트너 미니뷰(고정 크기)가 깨짐. 그래서 클라가 풀 전송 시 Pivot 제외.
- **데드 파리티**: 서버 사망 확정 시 `Monster.EnsureDead()`로 로컬 데스모션 보장, 로컬 사망 시 다음 스폰 막고 `battleEnd` 대기(솔로 DeadPhase와 동일 취지).

## 10. 코딩 컨벤션

- **삭제 대신 주석 처리** + 위에 날짜/이유. 예: `//--- 2026-xx-xx <이유>`.
- 상태명/트리거명은 코드-애니메이터 **문자열 일치 필수**(오타 시 조용히 폴백/무동작).
- Awake=자기 참조/싱글톤, Start=타 객체 참조(순서 의존 회피).
- 데모 아닌 **실운영 수준**으로 설계.

## 11. 검증 기법 (에디터 없이도)

- **서버 단독 컴파일**: `~/.m2`·`~/.gradle`에서 jackson/spring jar 찾아 `javac -cp <jars> Room.java`로 타입 검증(Maven 미설치라도 가능).
- **Unity 컴파일**: MCP `recompile_scripts`(연결 불안정하면 재시도). 메뉴 실행은 큐 타임아웃 나도 실제로 실행될 수 있어 **파일시스템으로 결과 확인**.
- **.pxo/스프라이트 육안 확인**: 스트립 PNG를 직접 열어 색/방향/손 파지 확인.
- **씬 YAML 편집**: 앵커/좌표 등 소수 값만. `m_LocalPosition` 등은 유일 문자열로 매칭.

---

## 관련 미완/TODO
- **넉백**: 강공격=넉백 의도지만 몹 `knockBack`이 Hit 재사용 + 위치 밀림 코드 없음. → 실제 위치 넉백(FxTuning 값, PivotActor 방식) 미구현.
- **협동 텔레그래프 타이밍**: 인텐트가 공격 턴에만 정해져 Devour/Convert 예고가 카운트다운 중 안 뜸(솔로는 미리 뜸). 서버가 다음 인텐트를 한 사이클 먼저 보내면 해결(프로토콜 확장).
- **WebGL 한글 IME**: jslib 브릿지로 해결 가능 확인, 미구현.
- 사운드/SFX/BGM, 밸런스 패스, 플레이 URL, 보스 크기·강공격 리롤 육안 튜닝.
