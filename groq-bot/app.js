const { App } = require('@slack/bolt');
const Groq = require('groq-sdk');
const express = require('express');
const multer = require('multer');
const fs = require('fs');
const axios = require('axios');
require('dotenv').config();

const groq = new Groq({
    apiKey: process.env.GROQ_API_KEY,
});

const app = new App({
    token: process.env.SLACK_BOT_TOKEN,
    signingSecret: process.env.SLACK_SIGNING_SECRET,
    socketMode: false,
    port: process.env.PORT || 5000
});

const upload = multer({ dest: 'uploads/' });

function formatMessage(text, isCode = false) {
    if (isCode) {
        return {
            "type": "section",
            "text": {
                "type": "mrkdwn",
                "text": `\`\`\`\n${text}\n\`\`\``
            }
        };
    }
    
    const blocks = [];
    const paragraphs = text.split('\n\n');
    
    paragraphs.forEach(paragraph => {
        if (paragraph.trim()) {
            if (paragraph.includes('```')) {
                const parts = paragraph.split('```');
                for (let i = 0; i < parts.length; i++) {
                    if (i % 2 === 0 && parts[i].trim()) {
                        blocks.push({
                            "type": "section",
                            "text": {
                                "type": "mrkdwn",
                                "text": parts[i].trim()
                            }
                        });
                    } else if (i % 2 === 1) {
                        blocks.push({
                            "type": "section",
                            "text": {
                                "type": "mrkdwn",
                                "text": `\`\`\`\n${parts[i]}\n\`\`\``
                            }
                        });
                    }
                }
            } else {
                blocks.push({
                    "type": "section",
                    "text": {
                        "type": "mrkdwn",
                        "text": paragraph
                    }
                });
            }
        }
    });
    
    return blocks;
}

app.message(async ({ message, say }) => {
    try {
        if (message.channel_type !== 'im') {
            return;
        }

        let userMessage = message.text;
        let fileContent = '';

        if (message.files && message.files.length > 0) {
            const file = message.files[0];
            try {
                const response = await axios.get(file.url_private, {
                    headers: {
                        'Authorization': `Bearer ${process.env.SLACK_BOT_TOKEN}`
                    }
                });
                fileContent = `\n\n파일 내용 (${file.name}):\n${response.data}`;
            } catch (error) {
                console.error('파일 다운로드 오류:', error);
                fileContent = '\n\n파일을 읽는 중 오류가 발생했습니다.';
            }
        }

        await say({
            text: '생각하는 중...',
            blocks: [
                {
                    "type": "section",
                    "text": {
                        "type": "mrkdwn",
                        "text": ":thinking_face: 생각하는 중..."
                    }
                }
            ]
        });

        const completion = await groq.chat.completions.create({
            messages: [
                {
                    role: "system",
                    content: "당신은 도움이 되는 AI 어시스턴트입니다. 사용자의 질문에 정확하고 친절하게 답변해주세요. 코드나 기술적인 내용이 있으면 적절히 포맷팅해서 제공해주세요."
                },
                {
                    role: "user",
                    content: userMessage + fileContent
                }
            ],
            model: "llama-3.1-405b-reasoning",
            temperature: 0.7,
            max_tokens: 4096,
        });

        const aiResponse = completion.choices[0]?.message?.content || '죄송합니다. 응답을 생성할 수 없습니다.';
        
        const blocks = formatMessage(aiResponse);
        
        await say({
            text: aiResponse,
            blocks: [
                {
                    "type": "section",
                    "text": {
                        "type": "mrkdwn",
                        "text": ":robot_face: *AI 응답*"
                    }
                },
                {
                    "type": "divider"
                },
                ...blocks
            ]
        });

    } catch (error) {
        console.error('오류 발생:', error);
        await say({
            text: '오류가 발생했습니다.',
            blocks: [
                {
                    "type": "section",
                    "text": {
                        "type": "mrkdwn",
                        "text": ":warning: 죄송합니다. 처리 중 오류가 발생했습니다. 잠시 후 다시 시도해주세요."
                    }
                }
            ]
        });
    }
});

app.event('app_mention', async ({ event, say }) => {
    if (event.channel_type !== 'im') {
        return;
    }
});

(async () => {
    try {
        await app.start();
        console.log(`⚡️ Slack Bot이 포트 ${process.env.PORT || 5000}에서 실행 중입니다!`);
    } catch (error) {
        console.error('앱 시작 오류:', error);
    }
})();