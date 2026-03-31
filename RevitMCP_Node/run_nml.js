const axios = require('axios');
const path = require('path');

const API_URL = 'http://localhost:5050/api/ai-command/';
const JSON_PATH = path.resolve(__dirname, 'nml_factory_full.json');

async function run() {
    console.log("🚀 Đang khởi động xây dựng Nhà máy NML (Full Automation)...");
    try {
        const response = await axios.post(API_URL, {
            action: 'build_from_blueprint',
            payload: { path: JSON_PATH }
        }, { timeout: 300000 }); // 5 phút

        console.log("✅ KẾT QUẢ:", JSON.stringify(response.data, null, 2));
    } catch (e) {
        console.error("❌ LỖI:", e.message);
    }
}

run();
