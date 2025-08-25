// 봇 설정 테스트 스크립트
require('dotenv').config();

console.log('환경변수 확인:');
console.log('SLACK_BOT_TOKEN:', process.env.SLACK_BOT_TOKEN ? '설정됨' : '설정되지 않음');
console.log('SLACK_SIGNING_SECRET:', process.env.SLACK_SIGNING_SECRET ? '설정됨' : '설정되지 않음');
console.log('GROQ_API_KEY:', process.env.GROQ_API_KEY ? '설정됨' : '설정되지 않음');
console.log('PORT:', process.env.PORT || '기본값(5000) 사용');

// Groq API 연결 테스트
const Groq = require('groq-sdk');

if (process.env.GROQ_API_KEY) {
    const groq = new Groq({
        apiKey: process.env.GROQ_API_KEY,
    });
    
    console.log('\nGroq API 연결 테스트 중...');
    
    groq.chat.completions.create({
        messages: [{ role: "user", content: "안녕하세요" }],
        model: "llama-3.1-405b-reasoning",
        max_tokens: 100,
    }).then(response => {
        console.log('✅ Groq API 연결 성공');
        console.log('응답:', response.choices[0]?.message?.content);
    }).catch(error => {
        console.log('❌ Groq API 연결 실패:', error.message);
    });
} else {
    console.log('❌ GROQ_API_KEY가 설정되지 않았습니다.');
}