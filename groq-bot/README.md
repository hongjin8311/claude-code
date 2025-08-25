# Slack Groq AI Bot

Groq AI를 사용하는 Slack 봇입니다. 1:1 DM에서 파일 업로드와 예쁜 응답 포맷을 지원합니다.

## 특징

- 🤖 **Groq AI 연동**: llama-3.1-405b-reasoning 모델 사용
- 💬 **1:1 DM 전용**: 개인 메시지에서만 동작
- 📁 **파일 업로드 지원**: 텍스트 파일 내용 분석 가능
- 🎨 **예쁜 응답**: 마크다운 포맷과 블록 레이아웃
- ⚡ **실시간 응답**: 빠른 AI 응답
- 🔒 **안전한 설정**: 환경변수 기반 보안

## 설치 및 설정

### 1. 클론 및 의존성 설치
```bash
git clone <repository-url>
cd groq-bot
npm install
```

### 2. 환경변수 설정
`.env` 파일을 생성하고 다음 내용을 입력:
```
SLACK_BOT_TOKEN=xoxb-your-bot-token-here
SLACK_SIGNING_SECRET=your-signing-secret-here
GROQ_API_KEY=your-groq-api-key-here
PORT=5000
```

### 3. Slack App 설정

#### 방법 1: Manifest 파일 사용 (권장)
1. https://api.slack.com/apps 방문
2. "Create New App" → "From an app manifest" 선택
3. `manifest.json` 파일 내용 복사하여 붙여넣기
4. Request URL을 실제 도메인으로 수정: `https://your-domain.com/slack/events`

#### 방법 2: 수동 설정
1. https://api.slack.com/apps 에서 새 Slack App 생성
2. **OAuth & Permissions**에서 Bot Token Scopes 추가:
   - `chat:write`
   - `im:history`
   - `im:read`
   - `files:read`
3. **Event Subscriptions** 활성화:
   - Request URL: `https://your-domain.com/slack/events`
   - Subscribe to bot events: `message.im`
4. **Install App** 탭에서 워크스페이스에 설치

### 4. 실행

#### 개발 모드
```bash
npm run dev
```

#### 프로덕션 모드
```bash
npm start
```

#### Ubuntu 서버에 시스템 서비스로 설정
```bash
# 서비스 파일 복사
sudo cp slack-bot.service /etc/systemd/system/

# 서비스 활성화 및 시작
sudo systemctl daemon-reload
sudo systemctl enable slack-bot
sudo systemctl start slack-bot

# 상태 확인
sudo systemctl status slack-bot
```

### 5. 설정 테스트
```bash
node test-setup.js
```

## 사용법

1. Slack에서 봇을 DM으로 초대
2. 메시지 전송하면 AI가 응답
3. 텍스트 파일을 업로드하면 내용 분석 후 응답
4. 코드, 마크다운 등 다양한 포맷 지원

## 로그 및 모니터링

```bash
# 서비스 로그 실시간 보기
sudo journalctl -u slack-bot -f

# 최근 1시간 로그
sudo journalctl -u slack-bot --since "1 hour ago"

# 에러 로그만 보기
sudo journalctl -u slack-bot -p err
```

## 파일 구조

```
groq-bot/
├── app.js              # 메인 애플리케이션
├── package.json        # 프로젝트 설정
├── manifest.json       # Slack App 매니페스트
├── slack-bot.service   # 시스템 서비스 설정
├── test-setup.js       # 설정 테스트
├── setup.md           # 상세 설정 가이드
├── .env.example       # 환경변수 템플릿
├── .gitignore         # Git 무시 파일
├── uploads/           # 파일 업로드 임시 저장소
└── README.md          # 이 파일
```

## 라이선스

MIT License