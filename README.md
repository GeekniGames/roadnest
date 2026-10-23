# ROADNEST

**한국어** | [English](#english)

움직이는 집(카라반)을 몰고 도로를 달리며 선택과 자원 관리로 생존하는 2D 모바일 로그라이크.
Unity 6 / 2D URP / 세로 화면(1080x1920) / 픽셀 아트.

> 개발 중 (솔로 프로젝트). 코어 로직, 이벤트 UI, 분배/HUD/게임오버 흐름까지 구현되어 있습니다.

![gameplay](docs/gameplay.gif)

## 한눈에 보기

| 항목 | 내용 |
|---|---|
| 엔진 | Unity 6000.4.7f1, 2D URP |
| 언어 | C# |
| 라이브러리 | DOTween, TextMeshPro, Unity Localization, NUnit (EditMode 테스트) |
| 목표 플랫폼 | Android / iOS (세로 고정) |

## 구현된 것

**게임 코어 (Unity 비의존 순수 C#, `Roadnest.Core` 어셈블리)**
- 하루 진행 상태 머신 `DayFlow`: 모드 선택(출발/정착) → 이벤트 슬롯 → 분배 → 다음 날
- 선택지 처리 `ChoiceResolver`: 조건/비용 검사 → 확률 굴림 → 효과 적용
- 시드 기반 결정적 난수: 같은 시드면 같은 결과 (재현, 디버깅, 테스트에 사용)
- 가중치 기반 이벤트 선택: 슬롯 종류, 연속 정착 횟수, 일차에 따른 보정

**데이터 주도 이벤트**
- 이벤트는 `events.json`에 정의하고, 로드 시점에 전부 검증합니다(선택지 수, 확률 합, 비용 중복, 폴백 이벤트 등). 잘못된 데이터는 한/영 오류 메시지와 함께 즉시 실패합니다.
- 코드 수정 없이 이벤트 추가 가능
- 현재 이벤트 18종 (이동/정착 슬롯별 가중치, 일차 조건 포함)

**UI / 연출**
- 이벤트 패널: 일반 선택지 최대 4개 + 특수 선택지 1개, 소모/성공/실패 영역별 최대 3줄 효과 표시, 선택 불가 카드 표시
- 주행 연출: 다층 패럴랙스 스크롤, 타일 땅 배경 스크롤, 시동 떨림, 바퀴 프레임, 배기가스
- 이벤트 진입 연출: 랜덤 대기 후 정지, 예고형 이벤트는 차 앞 느낌표 표시, 돌발형은 바로 진입
- 결과 화면: 성공/실패 표시와 보상 줄(최대 3줄), 확인 버튼을 누르면 자원 변화 연출
- 식량/물 분배 패널: 분배 결과에 따라 캐릭터 HP와 재고가 변함
- HUD: 내구도 바(값을 부드럽게 따라가고 낮아지면 색 변경/깜빡임), 캐릭터별 HP(변화 연출, 사망 시 숨김)
- 게임오버: 차량 파괴 / 전원 사망 두 가지 사유별 이미지와 문구, 생존 일수 표시, 씬 재시작
- 연출은 DOTween을 사용하고, 씬의 Update는 한 곳에서만 돌립니다.

**로컬라이징**
- Unity Localization으로 이벤트 제목/본문/선택지/결과 문구를 한국어/영어로 제공합니다. (String Table `Events`)

## 설계 원칙

- **코어와 화면 분리**: 로직은 엔진 없이 테스트 가능하고, 화면은 코어 상태를 읽어 그리기만 합니다.
- **GC 최소화**: 자주 호출되는 경로에서 LINQ, 문자열 보간, 임시 배열/리스트 할당을 쓰지 않고, TMP 포맷 문자열은 미리 만들어 둡니다.
- **데이터 검증 우선**: 실행 중 오류보다 로드 시점 실패를 선호합니다.

## 테스트

Unity Test Runner (EditMode)에서 실행합니다. 현재 80개.

- 코어 규칙, 이벤트 로딩/검증, 선택지 처리, 이벤트 선택, 하루 진행 흐름 단위 테스트
- **봇 시뮬레이션**: 무작위 봇이 여러 시드로 수천 판을 자동 플레이해 (1) 진행 불가(데드락) 없음 (2) 같은 시드 = 같은 결과를 확인합니다.
  - 실행 규모: [ 시드 수 ] 시드 x [ 판 수 ] 판
  - 결과: [ 평균 생존 일수 / 종료 사유 비율 등 ]
  - 주의: 이 수치는 **밸런스 근거가 아니라 로직 안정성 검증용**입니다. 이벤트 수와 자원 획득 수단이 아직 적습니다.

## 로드맵

- [x] 코어 로직, 이벤트 데이터/검증, 테스트, 봇 시뮬레이션
- [x] 이벤트 패널 UI, 주행/이벤트 진입 연출
- [x] HUD (내구도, 캐릭터별 HP)
- [x] 결과 화면(성공/실패 표시, 보상 줄)
- [x] 식량/물 분배 패널
- [x] 게임 오버 / 재시작
- [x] 이벤트 18종, 이벤트 텍스트 로컬라이징(Unity Localization)
- [ ] 정비 선택 UI (수리 / 제작 / 넘어가기)
- [ ] 아이템 시스템 (재료, 도구, 제작)
- [ ] 연계 이벤트, 이벤트 대폭 확장
- [ ] 캐릭터 선택, 합류 이벤트
- [ ] 실기기 빌드, 프로파일링 결과, 서버 연동

## 폴더 구조

```
Assets/_Project/
  01_Scenes/    씬
  02_Scripts/
    Core/       순수 C# 게임 로직
    Data/       데이터 로딩
    Map/        주행 연출, 씬 진입점
    Caravan/    차량 연출
    Sound/      오디오
    UI/         UI 패널
  03_Arts/      아트 리소스
  04_Data/      events.json 등 데이터
```

## 실행 방법

1. Unity 6000.4.7f1 이상으로 프로젝트를 엽니다.
2. DOTween을 설치합니다. (라이선스상 저장소에 포함하지 않았다면 별도 설치)
3. `01_Scenes`의 메인 씬을 열고 Play 합니다.

---

<a name="english"></a>
# ROADNEST (English)

[한국어](#roadnest) | **English**

A 2D mobile roguelike about surviving on the road in a moving home (a caravan) through choices and resource management.
Unity 6 / 2D URP / portrait (1080x1920) / pixel art.

> Work in progress (solo project). The core logic, event UI, and the distribution / HUD / game over flow are implemented so far.

![gameplay](docs/gameplay.gif)

## At a Glance

| Item | Detail |
|---|---|
| Engine | Unity 6000.4.7f1, 2D URP |
| Language | C# |
| Libraries | DOTween, TextMeshPro, Unity Localization, NUnit (EditMode tests) |
| Target | Android / iOS (portrait only) |

## What's Implemented

**Game core (engine-independent pure C#, `Roadnest.Core` assembly)**
- `DayFlow` state machine: pick mode (depart / settle) → event slots → distribution → next day
- `ChoiceResolver`: requirement and cost checks → probability roll → apply effects
- Deterministic seeded RNG: the same seed gives the same result (used for replay, debugging and tests)
- Weighted event selection with modifiers by slot type, consecutive settles and day number

**Data-driven events**
- Events are defined in `events.json` and fully validated at load time (choice counts, probability totals, duplicate costs, fallback event, etc.). Bad data fails immediately with bilingual (KR/EN) error messages.
- New events need no code changes.
- 18 events at the moment (weights per depart / settle slot, day-based conditions)

**UI / presentation**
- Event panel: up to 4 normal choices plus 1 special choice, up to 3 effect rows each for cost / success / failure, and a locked state for unaffordable choices
- Driving presentation: multi-layer parallax scrolling, a tiled ground background that scrolls with the road, ignition shake, wheel frames, exhaust puffs
- Event entry: random wait, then the car stops; telegraphed events show an exclamation mark above the car, sudden events open right away
- Result view: success / fail banner and reward rows (up to 3); resource changes animate when you press Done
- Food / water distribution panel: the result changes each character's HP and the stock
- HUD: durability bar (follows value changes smoothly, changes color and blinks when low) and per-character HP (animated changes, hidden on death)
- Game over: separate image and text for vehicle destroyed / all characters dead, days survived, scene restart
- Animations use DOTween, and the scene runs a single Update loop.

**Localization**
- Event titles, body text, choices and results are provided in Korean and English through Unity Localization (String Table `Events`).

## Design Principles

- **Core / view separation**: logic is testable without the engine; the view only reads core state and draws it.
- **Minimal GC**: no LINQ, string interpolation or temporary arrays/lists on hot paths; TMP format strings are prebuilt.
- **Validate data first**: fail at load time rather than at runtime.

## Testing

Run from the Unity Test Runner (EditMode). 80 tests at the moment.

- Unit tests for core rules, event loading/validation, choice resolution, event selection and the day flow
- **Bot simulation**: a random bot auto-plays thousands of runs across many seeds to confirm (1) no deadlocks (2) same seed = same result.
  - Scale: [ number of seeds ] seeds x [ runs ] runs
  - Results: [ average days survived / end-reason ratios, etc. ]
  - Note: these numbers verify **logic stability, not game balance**. The event count and resource sources are still small.

## Roadmap

- [x] Core logic, event data and validation, tests, bot simulation
- [x] Event panel UI, driving and event entry presentation
- [x] HUD (durability, per-character HP)
- [x] Result view (success / fail banner, reward rows)
- [x] Food / water distribution panel
- [x] Game over / restart
- [x] 18 events, event text localization (Unity Localization)
- [ ] Maintenance choice UI (repair / craft / skip)
- [ ] Item system (materials, tools, crafting)
- [ ] Chained events, large event expansion
- [ ] Character selection, join events
- [ ] Device build, profiling results, backend integration

## Project Layout

```
Assets/_Project/
  01_Scenes/    scenes
  02_Scripts/
    Core/       pure C# game logic
    Data/       data loading
    Map/        road presentation, scene entry point
    Caravan/    caravan visuals
    Sound/      audio
    UI/         UI panels
  03_Arts/      art resources
  04_Data/      data such as events.json
```

## Getting Started

1. Open the project with Unity 6000.4.7f1 or later.
2. Install DOTween (separately, if it is not included in the repo for license reasons).
3. Open the main scene in `01_Scenes` and press Play.