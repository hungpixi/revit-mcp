import fs from "fs";
import path from "path";

/**
 * Expected CAD structure for Mẫu Nhà Số 5 project
 * Based on plan.md and CAD file listing
 */
export interface CADFileStructure {
  fileName: string;
  level?: string;
  elevation?: number;
  expectedLayers: string[];
  expectedBlocks?: string[];
  expectedEntities: string[];
  description: string;
}

export interface ParsedCADFile {
  fileName: string;
  filePath: string;
  fileSize: number;
  lastModified: Date;
  expectedStructure?: CADFileStructure;
}

/**
 * Expected CAD structure mapping from plan.md
 * Định nghĩa cấu trúc CAD mong đợi cho dự án
 */
export const EXPECTED_CAD_STRUCTURE: Record<string, CADFileStructure> = {
  "A-101-MẶTBẰNGHẦM.dwg": {
    fileName: "A-101-MẶTBẰNGHẦM.dwg",
    level: "Basement (-1500mm)",
    elevation: -1500,
    expectedLayers: ["Tường_Hầm", "Cột", "Cửa", "Ramp", "Hệ thống"],
    expectedBlocks: ["D01", "D02"],
    expectedEntities: ["LINE", "POLYLINE", "BLOCK", "TEXT"],
    description: "Mặt bằng hầm - Garage, Kho, Bể tự hoại",
  },
  "A-102-MẶTBẰNGTẦNG1.dwg": {
    fileName: "A-102-MẶTBẰNGTẦNG1.dwg",
    level: "Level 1 (+1200mm FFL)",
    elevation: 1200,
    expectedLayers: ["Tường", "Cột", "Cửa", "Vách ngăn", "Hoàn thiện"],
    expectedBlocks: ["D01", "D02", "D03", "D04", "D05", "W1", "W2"],
    expectedEntities: ["LINE", "POLYLINE", "BLOCK", "TEXT", "HATCH"],
    description: "Mặt bằng tầng 1 - Phòng khách, Bếp, Phòng ăn",
  },
  "A-103-MẶTBẰNGTẦNG2.dwg": {
    fileName: "A-103-MẶTBẰNGTẦNG2.dwg",
    level: "Level 2 (+4600mm)",
    elevation: 4600,
    expectedLayers: ["Tường", "Cột", "Cửa", "Cửa sổ", "Vách ngăn"],
    expectedBlocks: ["D06", "D07", "D08", "W3", "W4", "W5"],
    expectedEntities: ["LINE", "POLYLINE", "BLOCK", "TEXT"],
    description: "Mặt bằng tầng 2 - Phòng ngủ 01, 02, Sảnh chung",
  },
  "A-104-MẶTBẰNGTẦNG3.dwg": {
    fileName: "A-104-MẶTBẰNGTẦNG3.dwg",
    level: "Level 3 (+8000mm)",
    elevation: 8000,
    expectedLayers: ["Tường", "Cột", "Cửa", "Cửa sổ", "Vách ngăn"],
    expectedBlocks: ["D09", "D10", "D11", "W6", "W7", "W8"],
    expectedEntities: ["LINE", "POLYLINE", "BLOCK", "TEXT"],
    description: "Mặt bằng tầng 3 - Phòng ngủ 01, 02, Hành lang",
  },
  "A-105-MẶTBẰNGMÁI.dwg": {
    fileName: "A-105-MẶTBẰNGMÁI.dwg",
    level: "Roof Level (+11400mm)",
    elevation: 11400,
    expectedLayers: ["Sàn Mái", "Tường chắn", "Bồn nước", "Louver"],
    expectedBlocks: ["LV1", "LV2"],
    expectedEntities: ["LINE", "POLYLINE", "BLOCK", "TEXT"],
    description: "Mặt bằng mái - Sân thượng, Vị trí bồn nước",
  },
  "A-608-KHAITRIỂNRAMDỐC.dwg": {
    fileName: "A-608-KHAITRIỂNRAMDỐC.dwg",
    level: "Basement Detail",
    elevation: -1500,
    expectedLayers: ["Ramp", "Kích thước", "Ghi chú", "Hệ thống"],
    expectedBlocks: [],
    expectedEntities: ["LINE", "POLYLINE", "TEXT", "DIMENSION", "ARC"],
    description: "Chi tiết ramp dốc hầm (18-19% độ dốc) - A-608",
  },
  "A-611-CHITIẾTLOUVER1,2.dwg": {
    fileName: "A-611-CHITIẾTLOUVER1,2.dwg",
    level: "Facade Detail",
    elevation: undefined,
    expectedLayers: ["Louver", "Kết cấu", "Kích thước"],
    expectedBlocks: ["LV1", "LV2"],
    expectedEntities: ["LINE", "POLYLINE", "BLOCK", "DIMENSION"],
    description: "Chi tiết Louver 1,2 - Thép vuông chống rỉ 50x100@100mm",
  },
  "A-612-CHITIẾTLOUVER5,6.dwg": {
    fileName: "A-612-CHITIẾTLOUVER5,6.dwg",
    level: "Facade Detail",
    elevation: undefined,
    expectedLayers: ["Louver", "Kết cấu", "Kích thước"],
    expectedBlocks: ["LV5", "LV6"],
    expectedEntities: ["LINE", "POLYLINE", "BLOCK", "DIMENSION"],
    description: "Chi tiết Louver 5,6 - Thép vuông chống rỉ 50x100@100mm",
  },
  "A-701-DANHSÁCHCỬAĐI.dwg": {
    fileName: "A-701-DANHSÁCHCỬAĐI.dwg",
    level: "Door Schedule",
    elevation: undefined,
    expectedLayers: ["Cửa", "Kích thước", "Ghi chú", "Bảng biểu"],
    expectedBlocks: ["D01", "D02", "D03", "D04", "D05", "D06", "D07", "D08", "D09", "D10", "D11"],
    expectedEntities: ["BLOCK", "TEXT", "TABLE", "LINE"],
    description: "Danh sách cửa đi - D01 đến D11",
  },
  "A-702-DANHSÁCHCỦASỔ1.dwg": {
    fileName: "A-702-DANHSÁCHCỦASỔ1.dwg",
    level: "Window Schedule",
    elevation: undefined,
    expectedLayers: ["Cửa sổ", "Kích thước", "Ghi chú", "Bảng biểu"],
    expectedBlocks: ["W1", "W2", "W3", "W4", "W5"],
    expectedEntities: ["BLOCK", "TEXT", "TABLE", "LINE"],
    description: "Danh sách cửa sổ 1 - W1 đến W5",
  },
  "A-703-DANHSÁCHCỬASỔ2.dwg": {
    fileName: "A-703-DANHSÁCHCỬASỔ2.dwg",
    level: "Window Schedule",
    elevation: undefined,
    expectedLayers: ["Cửa sổ", "Kích thước", "Ghi chú", "Bảng biểu"],
    expectedBlocks: ["W6", "W7", "W8", "W9", "W10"],
    expectedEntities: ["BLOCK", "TEXT", "TABLE", "LINE"],
    description: "Danh sách cửa sổ 2 - W6 đến W10",
  },
  "A-801-CHITIẾTRẢNHTHUNƯỚC.dwg": {
    fileName: "A-801-CHITIẾTRẢNHTHUNƯỚC.dwg",
    level: "Building System Detail",
    elevation: -1500,
    expectedLayers: ["Rãnh", "Cống", "Kích thước", "Ghi chú"],
    expectedBlocks: [],
    expectedEntities: ["LINE", "POLYLINE", "TEXT", "DIMENSION", "HATCH"],
    description: "Chi tiết rãnh thu nước và hệ thống hạ tầng ngầm - A-801",
  },
};

/**
 * Analyze CAD files in directory
 * Phân tích các file CAD trong thư mục
 */
export async function analyzeCADFiles(
  cadDirectory: string
): Promise<ParsedCADFile[]> {
  const files = fs.readdirSync(cadDirectory);
  const cadFiles = files.filter((f) => f.endsWith(".dwg"));

  const analyzed: ParsedCADFile[] = [];

  for (const file of cadFiles) {
    const filePath = path.join(cadDirectory, file);
    const stats = fs.statSync(filePath);

    analyzed.push({
      fileName: file,
      filePath,
      fileSize: stats.size,
      lastModified: stats.mtime,
      expectedStructure: EXPECTED_CAD_STRUCTURE[file],
    });
  }

  return analyzed.sort((a, b) => a.fileName.localeCompare(b.fileName));
}

/**
 * Get CAD file analysis report
 * Tạo báo cáo phân tích file CAD
 */
export function generateCADAnalysisReport(cadFiles: ParsedCADFile[]): string {
  let report = "=== CAD STRUCTURE ANALYSIS REPORT ===\n\n";

  report += `Total CAD Files: ${cadFiles.length}\n`;
  report += `Total Size: ${(cadFiles.reduce((sum, f) => sum + f.fileSize, 0) / 1024 / 1024).toFixed(2)} MB\n\n`;

  report += "=== FILE DETAILS ===\n";
  for (const file of cadFiles) {
    report += `\n📄 ${file.fileName}\n`;
    report += `   Size: ${(file.fileSize / 1024).toFixed(1)} KB\n`;
    report += `   Modified: ${file.lastModified.toISOString()}\n`;

    if (file.expectedStructure) {
      const struct = file.expectedStructure;
      report += `   Level: ${struct.level || "N/A"}\n`;
      report += `   Elevation: ${struct.elevation ?? "N/A"} mm\n`;
      report += `   Description: ${struct.description}\n`;
      report += `   Expected Layers: ${struct.expectedLayers.join(", ")}\n`;
      if (struct.expectedBlocks?.length) {
        report += `   Expected Blocks: ${struct.expectedBlocks.join(", ")}\n`;
      }
    } else {
      report += `   ⚠️  No expected structure found\n`;
    }
  }

  return report;
}

/**
 * Verify CAD structure completeness
 * Kiểm tra độ hoàn thiện của cấu trúc CAD
 */
export function verifyCADStructure(cadFiles: ParsedCADFile[]): {
  total: number;
  found: number;
  missing: string[];
  percentageComplete: number;
} {
  const expectedFiles = Object.keys(EXPECTED_CAD_STRUCTURE);
  const foundFiles = cadFiles.map((f) => f.fileName);

  const missing = expectedFiles.filter((f) => !foundFiles.includes(f));
  const found = expectedFiles.length - missing.length;

  return {
    total: expectedFiles.length,
    found,
    missing,
    percentageComplete: (found / expectedFiles.length) * 100,
  };
}

export default {
  EXPECTED_CAD_STRUCTURE,
  analyzeCADFiles,
  generateCADAnalysisReport,
  verifyCADStructure,
};
