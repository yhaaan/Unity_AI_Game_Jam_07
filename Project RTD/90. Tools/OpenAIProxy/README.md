# OpenAI local proxy

Unity 클라이언트에 API 키를 넣지 않고 OpenAI Responses API를 호출하기 위한 로컬 개발 서버입니다.
`127.0.0.1`에만 바인딩되므로 현재 PC 밖에서는 접근할 수 없습니다.

## 1. API 키 설정

[OpenAI Platform](https://platform.openai.com/api-keys)에서 이 게임잼 전용 Project API key를 만든 뒤,
현재 PowerShell 세션에만 설정합니다.

```powershell
$env:OPENAI_API_KEY = "your-project-api-key"
```

사용자 환경 변수로 저장하려면 아래 명령을 실행하고 새 터미널을 여세요.

```powershell
setx OPENAI_API_KEY "your-project-api-key"
```

키를 이 저장소, Unity Inspector, ScriptableObject, 빌드 파일에 넣지 마세요.

OpenAI Platform의 해당 프로젝트 `Limits`에서 처음에는 월 $50 hard limit과
$10 / $25 / $40 알림을 권장합니다. 게임 기능이 확정된 뒤에만 상한을 올리면
$500 전체가 실수로 소진되는 것을 막을 수 있습니다.

## 2. 서버 시작

```powershell
node --env-file="../../../.env.local" server.mjs
```

기본 주소는 `http://127.0.0.1:8787`입니다.

```powershell
Invoke-RestMethod http://127.0.0.1:8787/health
Invoke-RestMethod http://127.0.0.1:8787/v1/game-response `
  -Method Post `
  -ContentType "application/json" `
  -Body '{"input":"플레이어에게 짧은 환영 대사를 해줘"}'
```

## 3. 모델과 출력량 조정

기본 모델은 반복 호출 비용을 낮추기 위해 `gpt-6-luna`, 최대 출력은 256 tokens입니다.
품질이 더 필요한 요청만 환경 변수로 상향하세요.

```powershell
$env:OPENAI_MODEL = "gpt-6.2-sol"
$env:OPENAI_MAX_OUTPUT_TOKENS = "512"
node server.mjs
```

## 배포 전 주의

이 서버는 게임잼 로컬 개발용입니다. 배포 빌드에서는 API 키를 포함하지 말고,
인증·사용자별 rate limit·비용 제한을 둔 서버에 동일한 엔드포인트를 배포해야 합니다.

