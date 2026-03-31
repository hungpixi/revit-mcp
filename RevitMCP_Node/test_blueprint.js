const axios = require('axios');
const path = require('path');

const APIUrl = "http://localhost:5050/api/ai-command/";
const blueprintPath = path.resolve(__dirname, 'test_blueprint.json');

async function test() {
    console.log("🚀 Bắt đầu gửi Blueprint siêu tốc (0-latency)...");
    const startTime = Date.now();
    try {
        const response = await axios.post(APIUrl, {
            action: "build_from_blueprint",
            payload: {
                path: blueprintPath
            }
        }, { timeout: 300000 }); // 5 minutes timeout to handle Revit building thousands of elements

        console.log("✅ Thành công trong:", (Date.now() - startTime) / 1000, "giây!");
        console.log("📄 Response:", response.data);
    } catch (e) {
        console.error("❌ Lỗi:", e.message);
    }
}

test();
