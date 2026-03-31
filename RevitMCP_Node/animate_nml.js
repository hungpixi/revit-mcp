const fs = require('fs');
const path = require('path');
const axios = require('axios');

const API_URL = 'http://localhost:5050/api/ai-command/';
const JSON_PATH = path.resolve(__dirname, 'nml_factory_full.json');

async function sendCommand(action, payload) {
    try {
        const response = await axios.post(API_URL, {
            action: action,
            payload: payload
        }, { timeout: 30000 });
        if (response.data.status === "Error") {
            console.error(`❌ Lỗi [${action}]:`, response.data.msg);
        }
        return response.data;
    } catch (e) {
        console.error(`❌ HTTP Error [${action}]:`, e.message);
        return null;
    }
}

async function runAnimation() {
    console.log("🚀 Bắt đầu quá trình vẽ NML Factory tự động (Animation Mode)...");
    
    // 1. Đọc file blueprint
    const bp = JSON.parse(fs.readFileSync(JSON_PATH, 'utf8'));
    
    // Virtual ID map để lưu ElementId trả về từ Revit
    let idMap = {};

    // 2. Levels (Giả sử template đã có Level 1, Level 2)

    // 3. Grids
    if (bp.grids) {
        for (let g of bp.grids) {
             console.log(`📡 Vẽ lưới trục: ${g.name}`);
             await sendCommand('create_grid', { start: g.start, end: g.end, name: g.name });
             await new Promise(r => setTimeout(r, 200)); // Sleep để tạo hiệu ứng animation
        }
    }

    // 4. Columns
    if (bp.columns) {
        for (let c of bp.columns) {
             console.log(`🏢 Vẽ cột thép tại: ${c.point}`);
             await sendCommand('place_family_instance', { 
                 category: "Structural Columns", 
                 family: "W-Wide Flange-Column", 
                 type: "W10X49",
                 point: c.point,
                 levelId: c.level || "Level 1"
             });
             await new Promise(r => setTimeout(r, 200));
        }
    }

    // 5. Equipment (Panels)
    if (bp.equipment) {
        for (let eq of bp.equipment) {
             console.log(`🔌 Đặt tủ điện ${eq.id} tại ${eq.point}`);
             let res = await sendCommand('place_electrical_equipment', {
                 family: "Lighting and Appliance Panelboard - 208V MCB - Surface",
                 type: "100 A",
                 point: eq.point,
                 levelId: eq.level || "Level 1"
             });
             if (res && res.elementId) idMap[eq.id] = res.elementId;
             await new Promise(r => setTimeout(r, 100));
        }
    }

    // 6. Lighting Fixtures
    if (bp.lighting) {
        for (let idx=0; idx<bp.lighting.length; idx++) {
             let l = bp.lighting[idx];
             console.log(`💡 Lắp bóng đèn thứ ${idx+1}/${bp.lighting.length} tại ${l.point}`);
             let res = await sendCommand('place_lighting_fixture', {
                 family: "M_Troffer - Lấy sáng",
                 type: "600x600",
                 point: l.point,
                 levelId: l.level || "Level 1"
             });
             if (res && res.elementId) idMap[l.id] = res.elementId;
             await new Promise(r => setTimeout(r, 100));
        }
    }

    // 7. Circuits
    if (bp.circuits) {
        for (let circ of bp.circuits) {
            console.log(`⚡ Tạo mạch điện nối vào tủ ${circ.panel}`);
            // Gửi mảng các ID phần tử đã map 
            let mappedFixtures = circ.fixtures.map(uid => idMap[uid]).filter(id => id);
            if (mappedFixtures.length > 0) {
               await sendCommand('create_electrical_circuit', {
                   panelId: idMap[circ.panel] || null,
                   fixtureIds: mappedFixtures,
                   type: circ.type || "Power"
               });
            }
        }
    }

    // 8. Cable Trays
    if (bp.cable_trays) {
        for (let i=0; i<bp.cable_trays.length; i++) {
             let tr = bp.cable_trays[i];
             console.log(`⚡ Kéo máng cáp tuyến ${i+1}`);
             // Send route to custom command if needed, or straight segments
             if (tr.route && tr.route.length >= 2) {
                 for (let j=0; j<tr.route.length - 1; j++) {
                     await sendCommand('create_cable_tray', {
                         start: tr.route[j],
                         end: tr.route[j+1],
                         levelId: tr.level || "Level 1",
                         width: tr.width || 300,
                         height: tr.height || 100
                     });
                     await new Promise(r => setTimeout(r, 300));
                 }
             }
        }
    }

    console.log("✅ HOÀN THÀNH TOÀN BỘ BẢN VẼ BIỂU DIỄN NML FACTORY!");
}

runAnimation();
