const http = require('http');

const revitPort = 5050;

async function sendCommand(action, payload) {
    const data = JSON.stringify({ action, payload });
    const options = {
        hostname: 'localhost',
        port: revitPort,
        path: '/api/ai-command/',
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'Content-Length': Buffer.byteLength(data)
        }
    };

    return new Promise((resolve, reject) => {
        const req = http.request(options, (res) => {
            let resData = '';
            res.on('data', (chunk) => resData += chunk);
            res.on('end', () => {
                const parsed = JSON.parse(resData);
                if (parsed.status === 'OK') {
                    console.log(`  ✅ ${action}: ${parsed.msg || 'OK'}`);
                } else {
                    console.log(`  ❌ ${action}: ${parsed.msg}`);
                }
                resolve(parsed);
            });
        });
        req.on('error', (e) => {
            console.error(`  🔌 Lỗi kết nối cảng 5050. Revit chạy chưa?`);
            reject(e);
        });
        req.write(data);
        req.end();
    });
}

async function sleep(ms) {
    return new Promise(r => setTimeout(r, ms));
}

async function main() {
    console.log("========================================");
    console.log(" PHASE 3 AUTO TEST: NHÀ THÉP + CÁP NGÝ");
    console.log("========================================\n");

    // Bước 0: Ping để check Add-in sống
    try {
        const ping = await sendCommand("get_levels", {});
        console.log("  ℹ️  Add-in ping OK.\n");
    } catch(e) {
        console.error("  ❌ Revit Add-in KHÔNG phản hồi. Dừng.\n");
        return;
    }

    // Bước 1: 4 Cột Thép - Nhà xưởng 20x15m
    console.log("📌 Step 1: 4 Cột Kết Cấu Thép...");
    await sendCommand("create_structural_column", {
        points: [
            [0, 0, 0],
            [20, 0, 0],
            [0, 15, 0],
            [20, 15, 0]
        ]
    });
    await sleep(500);

    // Bước 2: Dầm thép kết nối 4 đỉnh cột ở Z=12
    console.log("\n📌 Step 2: Dầm Thép Kết Cấu (Z=12ft)...");
    await sendCommand("create_structural_framing", {
        points: [
            [0, 0, 12],
            [20, 0, 12],
            [20, 15, 12],
            [0, 15, 12],
            [0, 0, 12]
        ]
    });
    await sleep(500);

    // Bước 3: Cable Tray W300xH100mm từ tủ điện vào xưởng
    console.log("\n📌 Step 3: Máng Cáp Điện (W300 H100mm, Z=10ft)...");
    await sendCommand("create_cable_tray", {
        width: 300,
        height: 100,
        points: [
            [-5, 5, 10],
            [5, 5, 10],
            [5, 12, 10],
            [15, 12, 10]
        ]
    });
    await sleep(500);

    // Bước 4 (bonus): Conduit Ø25mm - nhánh phụ chiếu sáng
    console.log("\n📌 Step 4 (Bonus): Ống Luồn Dây Conduit Ø25mm...");
    await sendCommand("create_conduit", {
        diameter: 25,
        points: [
            [5, 5, 10],
            [5, 5, 3],  // Kéo thẳng xuống bảng điện
            [10, 5, 3]
        ]
    });

    console.log("\n========================================");
    console.log(" ✅ PHASE 3 COMPLETE - CHECK REVIT VIEW");
    console.log("========================================\n");
}

main();
