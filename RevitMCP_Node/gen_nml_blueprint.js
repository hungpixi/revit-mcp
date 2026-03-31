const fs = require('fs');
const path = require('path');

// Constants for Grid Spacing (METRIC to FEET conversion)
const M_TO_FT = 3.28084;
const gridX = [0, 10, 20, 30].map(m => m * M_TO_FT);
const gridY = [0, 8, 16, 24].map(m => m * M_TO_FT);

const blueprint = {
  project: {
    name: "NML_Factory_Automation",
    template: "C:\\ProgramData\\Autodesk\\RVT 2020\\Templates\\US Imperial\\Electrical-Default.rte",
    save_path: "d:\\CODE\\revit-mcp\\NML_Factory_Electrical_Final.rvt"
  },
  levels: [
    { name: "Level 1", elevation: 0.0 },
    { name: "Level 2", elevation: 14.0 }
  ],
  grids: [],
  columns: [],
  walls: [],
  cable_trays: [],
  lighting: [],
  equipment: [],
  circuits: []
};

// 1. Grids
gridX.forEach((x, i) => {
  blueprint.grids.push({ name: (i+1).toString(), start: [x, -5, 0], end: [x, gridY[gridY.length-1] + 5, 0] });
});
gridY.forEach((y, i) => {
  const label = String.fromCharCode(65 + i); // A, B, C, D
  blueprint.grids.push({ name: label, start: [-5, y, 0], end: [gridX[gridX.length-1] + 5, y, 0] });
});

// 2. Columns (All intersections)
for (let x of gridX) {
  for (let y of gridY) {
    blueprint.columns.push({ level: "Level 1", point: [x, y, 0] });
  }
}

// 3. Walls (Boundary)
const xMax = gridX[gridX.length-1];
const yMax = gridY[gridY.length-1];
blueprint.walls.push({ level: "Level 1", start: [0,0,0], end: [xMax,0,0] });
blueprint.walls.push({ level: "Level 1", start: [xMax,0,0], end: [xMax,yMax,0] });
blueprint.walls.push({ level: "Level 1", start: [xMax,yMax,0], end: [0,yMax,0] });
blueprint.walls.push({ level: "Level 1", start: [0,yMax,0], end: [0,0,0] });

// 4. Panels (Equipment)
blueprint.equipment.push({ id: "Panel_A", category: "Panels", level: "Level 1", point: [2, 2, 0] });
blueprint.equipment.push({ id: "Panel_B", category: "Panels", level: "Level 1", point: [xMax - 2, yMax - 2, 0] });

// 5. Lighting & Circuits
// 3x3 Bays
for (let i = 0; i < 3; i++) { // X bays
  for (let j = 0; j < 3; j++) { // Y bays
    const bayId = `Bay_${i+1}${String.fromCharCode(65+j)}`;
    const centerX = (gridX[i] + gridX[i+1]) / 2;
    const centerY = (gridY[j] + gridY[j+1]) / 2;
    
    // 4 Lights per bay
    const offsets = [[-4, -4], [4, -4], [-4, 4], [4, 4]];
    const bayLights = [];
    offsets.forEach((off, idx) => {
      const lid = `${bayId}_L${idx+1}`;
      blueprint.lighting.push({ id: lid, level: "Level 1", point: [centerX + off[0], centerY + off[1], 12] });
      bayLights.push(lid);
    });

    // Circuiting: Panels splits (Roughly left side to A, right to B)
    const panelToUse = i < 2 ? "Panel_A" : "Panel_B";
    blueprint.circuits.push({
      panel: panelToUse,
      type: "Power",
      fixtures: bayLights
    });
  }
}

// 6. Cable Trays (Main Spine on Grid B and D branches)
blueprint.cable_trays.push({
  level: "Level 1",
  width: 450,
  height: 100,
  route: [ [0, gridY[1], 13], [xMax, gridY[1], 13] ]
});
blueprint.cable_trays.push({
  level: "Level 1",
  width: 300,
  height: 100,
  route: [ [gridX[2], gridY[1], 13], [gridX[2], gridY[3], 13] ]
});

fs.writeFileSync(path.join(__dirname, 'nml_factory_full.json'), JSON.stringify(blueprint, null, 2));
console.log("✅ Done: Generated nml_factory_full.json");
