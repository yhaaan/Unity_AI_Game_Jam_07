# Project RTD AI 게임잼 제출 초안

## 1. Unity CLI 사용 내역

| 어떤 것을 구현하셨나요? | 잘 됐던 점 | 잘 안됐던 점 (개선 필요) |
| --- | --- | --- |
| Windows 프로토타입 빌드 | 타이틀 → Stage01 → 엔딩으로 이어지는 초기 플레이 버전을 `StandaloneWindows64`로 빌드했다. 빌드 성공 여부와 에디터 버전, 소스 리비전이 `ProjectRTD.provenance.json`에 남았다. | 당시 작업 중 변경분을 포함해 빌드했다. 같은 결과를 재현하려면 커밋 시점의 파일 상태와 빌드 시 포함된 변경을 함께 관리할 필요가 있다. |
| Unity 에디터 조회 및 EditMode 검증 | 전용 대화 앱에서 Unity CLI로 씬, Hierarchy, Inspector를 조회했다. 최종 시트와 파츠 에셋 18개의 값·스킬 일치, 드론·과학자의 방향 전환 시 콜라이더 크기 유지 등을 테스트했다. | 일부 세션에서는 에디터 연결이 끊기거나 재컴파일·테스트가 시간 초과됐다. 에디터 연결 상태를 점검한 뒤 검증을 다시 실행해야 했다. |
| Play Mode 기믹 확인 및 화면 캡처 | 초기 Stage01의 무게 발판과 문 개방, 낙하 리스폰, 출구 이동을 확인했다. 이후 7개 레벨 진행과 엔딩, 타이틀 이후의 실제 파츠 추첨·재추첨·장착까지 확인했다. | 초기에 에디터가 백그라운드에 있을 때 프레임이 진행되지 않아 상태 조회와 캡처에 앞서 프레임 진행을 확인했다. |
| Unity AI CLI로 게임 효과음 생성 및 연결 | 효과음 11종을 먼저 생성한 뒤 레트로 실험실의 분위기에 맞춰 다시 제작했다. 발소리·물 젓기·활강음을 포함한 15종을 `GameSoundBank`에 연결했다. | 첫 생성 중 대기열 시간 초과가 있었다. 점프음은 긴 뒷부분이 플레이에서 거슬려 원본 WAV를 보존하고 재생할 때 첫 타격음만 최대 0.12초 사용하도록 수정했다. |

## 2. OpenAI 도구 사용 내역

| 도구 및 사용 위치 | 사용 방식 (어떻게) | 특별히 활용한 점 (선택) |
| --- | --- | --- |
| Codex / 전용 개발 앱 **Unity Game Jam** 제작 | 프로젝트 폴더를 읽고 Unity 에디터와 연동할 수 있는 OpenAI Responses API 기반 로컬 브라우저 개발 에이전트를 제작했다. | 작업별로 대화와 모델 설정을 나누고 실행 중에도 다른 세션을 사용할 수 있게 했다. 프로젝트 파일을 인덱싱하고 대화 기록과 토큰 사용량을 로컬에 저장해 재실행 후 이어서 작업했다. |
| OpenAI Responses API / Unity Game Jam · 키메라 파츠 시스템 | Google Sheets의 최종 9열 구조를 Unity ScriptableObject 18개에 반영하고, 시트의 스킬 식별자를 활강·부력·과학자 통과 동작에 연결했다. | Inspector와 EditMode 테스트로 원본과 에셋을 대조했다. 이후 무게 −6~6에 따른 점프·파워 표와 공통 이동속도를 코드에 반영했다. |
| OpenAI Responses API / Unity Game Jam · 효과음과 캐릭터 연출 | 레트로 TV 속 실험실에 어울리는 효과음별 프롬프트를 만들고 Unity AI CLI로 생성을 요청했다. 이동 거리에 따른 좌우 발소리, 타이틀 로고 등장, 플레이어와 적의 카드식 방향 전환을 구현했다. | 생성된 소리의 길이와 재생 시점을 다시 조정했다. 화면에서 뒤집히는 연출 중에도 조작 입력과 충돌 판정을 유지하도록 구현했다. |
| Codex / 게임 기획·플레이어 조작·스테이지 | 동물 파츠 조합에 따라 공용·특수 경로를 고르는 퍼즐 구조를 구체화했다. 기본 이동·점프, 씬 전환, 7개 레벨과 타이틀 이후의 `Chimera Roll` 화면 구현에 활용했다. | 연출 화면의 파츠 슬롯, `Reroll` 버튼, 실제 플레이어 장착이 같은 추첨 결과를 쓰도록 연결하고 Play Mode에서 확인했다. |

## 3. 주요 프롬프트 (선택)

| 사용 도구 | 프롬프트 | 결과 및 활용 방식 |
| --- | --- | --- |
| OpenAI Responses API | 앞선 요청에서 최종 9열 Google Sheets 표와 파츠 18개의 데이터를 전달하고, 새 규격으로 가져올 수 있게 임포터를 수정해 달라고 했다. 후속 요청은 “시트랑 실제 SO 데이터 동기화 시키고 실제 skill의 능력도 구현해”였다. | 시트의 `skill`과 `heightDelta` 원본 표기를 Unity ScriptableObject 18개에 보존했다. `canFly`는 활강, `canSwim`은 부력, `canPass`는 과학자 통과 동작에 연결했다. 관련 EditMode 테스트 10건이 통과했다. |
| OpenAI Responses API + Unity AI CLI | 너무 효과음이 구리다. 너가 이 게임의 아트 스타일과 , 특색을 잘 고려해서 unity cli 에게 프롬프트 잘 넣어서 적절한 sound를 만들어봐. 그리고 걸을때는 걷는 사운드가 계속 재생돼야하는데 그런 기본적인 게임적 요소들도 다 고려해서 다시 제작하고 재생시켜 | 레트로 실험실의 소품 질감을 기준으로 15종을 다시 구성했다. 실제 이동 거리에 맞춰 왼발과 오른발 소리를 번갈아 재생하고, 수면 이동 중에는 물 젓는 소리를 재생한다. |
| OpenAI Responses API | 좌우 키를 눌러서 flip 될때 바로 flip 되지 말고 scale이 서서히 0이 되면서 마치 카드 뒤집히듯 이 flip 되게 해줄 수 있어 ? 대신 아주 빠르게 돼서 이 연출로 인한 딜레이가 존재하지는 않게 . | 약 0.08초 동안 스프라이트가 접혔다 펴지는 효과를 구현했다. 드론과 과학자에도 공통 코드를 적용하고 콜라이더 크기 유지 테스트를 추가했다. |
| Codex | 프롤로그에서는 보이지 말고 프롤로그 -> 타이틀 -> 추첨 -> 스테이지 진입 . 이렇게 해 | 프롤로그의 파츠 회전 연출을 타이틀 이후 `Chimera Roll` 화면으로 옮겼다. `Reroll`로 바뀐 조합이 실제 플레이어에게 장착되는 것을 확인했다. |

## 4. 생성물 목록

`Assets/` 및 `90. Tools/` 경로는 Unity 프로젝트 폴더인 `Project RTD/` 기준이다.

| 구분 | 파일명 또는 위치 | 생성 도구 | 수정 여부 및 비고 |
| --- | --- | --- | --- |
| 개발용 웹 앱 | `90. Tools/OpenAIDevCLI/` 및 저장소 루트 `Start-Unity-Game-Jam.cmd` | Codex | 세션 분리, 프로젝트 파일 탐색, 기록 복원, 토큰 표시, Unity CLI 연동을 추가했다. 앱 이름은 **Unity Game Jam**이다. |
| C# / 파츠 데이터 | `Assets/01. Scripts/01. Gameplay/`의 `AnimalPartCsv.cs`, `AnimalPartImporter.cs`, `AnimalPartDefinition.cs`, `PlayerPartBuild.cs`, `PlayerPartLoadout.cs`, `PlayerMotor2D.cs` 및 `Assets/06. Data/Part_*.asset` | Codex, Unity Game Jam / OpenAI Responses API | 18개 파츠 데이터를 갱신하고 테스트를 추가했다. 활강은 후속 피드백에 따라 서서히 하강하는 방식으로 바꿨다. |
| C#·씬·프리팹 / 게임 진행과 연출 | `Assets/00. Scenes/`, `Assets/01. Scripts/00. Core/ScreenFlowController.cs`, `Assets/01. Scripts/01. Gameplay/`의 `StageRuntime.cs`, `ChimeraRollPresentation.cs`, `StageAssemblyIntro.cs`, `CardTurn.cs`, `PlatformEnemy.cs` 및 `Assets/02. Prefabs/`의 적·배경 프리팹 | Codex, Unity Game Jam / OpenAI Responses API | 타이틀 배치, 스테이지·키메라 추첨 흐름, 카드식 방향 전환과 새 배경 에셋의 패럴랙스 배치를 구현·수정했다. |
| C# / 사운드와 검증 | `Assets/01. Scripts/00. Core/`의 `SoundManager.cs`, `GameSoundBank.cs`, `JumpTakeoff.cs`, `Assets/01. Scripts/01. Gameplay/TravelCadence.cs`, `Assets/99. Tests/` | Codex, Unity Game Jam / OpenAI Responses API | 보행 거리 기반 재생과 점프음 트리밍을 추가했다. 파츠, 사운드, 배경 참조, 적 방향 전환을 EditMode 테스트로 확인했다. |
| 효과음 WAV 26개 | `Assets/05. Arts/RTD_Foley_*.wav` 13개 및 `RTD_SFX_*.wav` 13개, 각 `.unityai.json` | Unity AI CLI / ElevenLabs Sound Effects v2 | 첫 생성분 11개와 이후 사용본 15개를 보관했다. `RTD_Foley_*.wav` 13개와 좌우 발소리 2개가 `GameSoundBank.asset`에 연결돼 있다. |
| 이미지 / 초기 배경 소품 6개 | 이전 버전 `Assets/03. Sprites/Background/Clean/`의 `Chamber.png`, `Door.png`, `LabPropsSheet.png`, `Machine.png`, `RobotArm.png`, `TV.png` | Unity AI CLI / Photoroom Background Remover | 소품 이미지의 배경 제거에 사용했다. 새 배경을 적용한 커밋 `6c016dbd`에서 구버전 작업본을 제거했다. |

## 5. 심사 참고 자료

| 구분 | 자료 설명 | 링크 또는 파일명 |
| --- | --- | --- |
| 개발 세션 발췌 | 앱의 실제 프롬프트와 수정·재검증 내역. 파츠 동기화, 효과음 생성·보정, 카드식 방향 전환, 무게·점프·파워 조정 사례. | `AI 게임잼 심사 참고 기록.md` |
| Unity CLI 빌드 기록 | Windows 64비트 프로토타입 빌드 성공과 당시 에디터·소스 버전 기록. | `Project RTD/Builds/Prototype/ProjectRTD.provenance.json` |
| Play Mode 화면 | 초기 프로토타입 타이틀, Stage01 시작 지점, 무게 발판 반응. | `Project RTD/Captures/`의 `title.png`, `stage01-start-final.png`, `stage01-weight-feedback.png` |
| 효과음 생성 기록 | 점프·발소리 등 효과음의 생성 모델, 프롬프트, 파라미터. | `Project RTD/Assets/05. Arts/`의 `RTD_Foley_Jump.wav.unityai.json`, `RTD_SFX_StepLeft.wav.unityai.json` |
| 저장소 | Unity 프로젝트와 전용 개발 앱의 구현 및 변경 이력. | [Unity_AI_Game_Jam_07](https://github.com/yhaaan/Unity_AI_Game_Jam_07) |
