# OpenAI API Dev CLI

OpenAI Responses API 크레딧으로 Unity 프로젝트의 코드 작업을 수행하는 로컬 개발 CLI다.
Codex 사용량 대신 API Platform 크레딧을 사용하며, 실행 중인 Unity Editor에는 Unity CLI로 연결한다.

## 실행

PowerShell에서:

```powershell
& '.\Project RTD\90. Tools\OpenAIDevCLI\start.ps1'
```

또는 `api-dev.cmd`를 더블클릭한다.

프롬프트 예시:

```text
현재 플레이어 이동 코드를 분석하고 점프 버그를 수정한 뒤 컴파일해줘.
적 AI 구조를 설명하고 필요한 EditMode 테스트를 추가해줘.
이번 변경의 Git diff를 검토하고 컴파일 오류를 고쳐줘.
```

## 모델과 세션 명령

- `/model astra`: 품질 우선 `gpt-6.2-astra`로 전환한다.
- `/model sol`: 일반 구현용 `gpt-6.1-sol`로 전환한다.
- `/model luna`: 단순 작업용 `gpt-6-luna`로 전환한다.
- `/effort low|medium|high`: 추론 강도를 변경한다.
- `/scope project`: Unity 프로젝트 전체의 허용된 텍스트 파일에 쓴다. 기본값이다.
- `/scope assets`: 자동 쓰기를 다시 `Assets` 아래로 제한한다.
- `/new`: API 대화 문맥을 초기화한다.
- `/status`: 누적 토큰과 이번 세션 변경 파일을 표시한다.
- `/undo`: 이번 실행에서 CLI가 변경한 모든 파일을 시작 전 상태로 복원한다.
- `/exit`: 종료한다.

## 안전 범위

- `.env.local`의 `OPENAI_API_KEY`는 메모리에서만 읽고 출력하지 않는다.
- `Library`, `Temp`, `Logs`, 비밀정보 파일은 모델에 전달하지 않는다.
- 자동 쓰기는 `Project RTD` 전체의 허용된 텍스트 파일을 대상으로 한다.
- 기존 파일은 먼저 읽은 뒤 정확한 텍스트 교체로만 수정한다.
- `Packages`, `ProjectSettings`, `.vscode`, `90. Tools`도 수정할 수 있다.
- `.git`, `Library`, `Temp`, `Logs`, 빌드 산출물, 비밀정보와 바이너리 파일은 계속 차단한다.
- 에이전트가 `OpenAIDevCLI` 자신의 실행 코드와 권한 규칙을 수정하는 것은 차단한다.
- 삭제, 커밋, 푸시, 패키지 설치, 빌드, Unity Editor 종료 기능은 제공하지 않는다.
- Unity 작업은 상태 확인, 재컴파일, EditMode 테스트로 제한한다.

API 사용량은 OpenAI Platform의 Usage 화면에서 최종 확인한다. CLI에는 입력·출력 토큰 누계가 표시된다.
