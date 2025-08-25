# Slack Groq AI Bot 설정 가이드

## 1. 의존성 설치
```bash
npm install
```

## 2. 환경변수 설정
`.env` 파일을 생성하고 다음 내용을 입력:
```
SLACK_BOT_TOKEN=xoxb-your-bot-token-here
SLACK_SIGNING_SECRET=your-signing-secret-here  
GROQ_API_KEY=your-groq-api-key-here
PORT=5000
```

## 3. Slack App 설정
1. https://api.slack.com/apps 에서 새 Slack App 생성
2. OAuth & Permissions에서 스코프 추가:
   - `chat:write`
   - `im:history` 
   - `im:read`
   - `files:read`
3. Event Subscriptions 활성화하고 이벤트 구독: `message.im`
4. Bot Token을 `.env`에 추가

## 4. 시스템 서비스 설정
```bash
sudo cp slack-bot.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable slack-bot
sudo systemctl start slack-bot
```

## 5. 실행 및 확인
```bash
# 상태 확인
sudo systemctl status slack-bot

# 로그 확인  
sudo journalctl -u slack-bot -f
```