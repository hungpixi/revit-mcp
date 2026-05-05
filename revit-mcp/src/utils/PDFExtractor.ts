import fs from "fs";
import path from "path";
import { createRequire } from "module";

// pdf-parse v2+ exports a PDFParse class (CommonJS) — use createRequire for ESM
const _require = createRequire(import.meta.url);

async function parsePDF(buffer: Buffer): Promise<{ text: string; numpages: number }> {
  // pdf-parse v1.1.1 — exports a function directly
  const fn = _require("pdf-parse");
  if (typeof fn === "function") return fn(buffer, { max: 0 });
  const fn2 = fn.default;
  if (typeof fn2 === "function") return fn2(buffer, { max: 0 });
  throw new Error("pdf-parse: cannot locate parse function");
}

export interface MaterialSpec {
  code: string;           // e.g. "FT-01"
  name: string;           // e.g. "Gạch Ceramic 600x600"
  widthMm: number;        // tile width
  heightMm: number;       // tile height
  thicknessMm: number;    // tile thickness
  jointWidthMm: number;   // grout joint
  colorHex: string;       // approximate tile color
  category: "floor" | "wall" | "stair" | "other";
  locations: string[];    // rooms/areas where used
  patternType: "stack" | "running_bond" | "herringbone" | "basket_weave" | "diagonal";
  finish: string;         // e.g. "Bóng", "Mờ", "Chống trượt"
  notes: string;
}

export interface DoorSpec {
  code: string;           // e.g. "D01"
  widthMm: number;
  heightMm: number;
  type: "single" | "double" | "sliding" | "folding";
  material: string;
  glassType: string;
  notes: string;
}

export interface WindowSpec {
  code: string;           // e.g. "W1"
  widthMm: number;
  heightMm: number;
  sillHeightMm: number;
  type: "casement" | "fixed" | "sliding" | "awning";
  material: string;
  glassType: string;
  notes: string;
}

export interface DocumentExtractResult {
  materials: MaterialSpec[];
  doors: DoorSpec[];
  windows: WindowSpec[];
  rawText: string;
  sourceFile: string;
  pageCount: number;
  warnings: string[];
}

// ─── Known material table (from plan.md + standard AEC drawings) ──────────────
const KNOWN_MATERIALS: Record<string, Partial<MaterialSpec>> = {
  "FT-01": {
    name: "Gạch Ceramic 600x600", widthMm: 600, heightMm: 600, thicknessMm: 10,
    jointWidthMm: 3, colorHex: "#E8E0D0", category: "floor", finish: "Bóng",
    patternType: "stack", locations: ["Phòng khách", "Phòng ăn", "Sảnh"],
  },
  "FT-02": {
    name: "Gạch Ceramic 300x300 Chống Trượt", widthMm: 300, heightMm: 300, thicknessMm: 8,
    jointWidthMm: 3, colorHex: "#D4C9B0", category: "floor", finish: "Chống trượt",
    patternType: "stack", locations: ["WC1", "WC2", "WC3"],
  },
  "FT-03": {
    name: "Sàn Ốp Gỗ", widthMm: 1200, heightMm: 120, thicknessMm: 12,
    jointWidthMm: 2, colorHex: "#8B6914", category: "floor", finish: "Bóng",
    patternType: "running_bond", locations: ["Phòng ngủ 01", "Phòng ngủ 02"],
  },
  "FT-04": {
    name: "Sàn Bê Tông Thô", widthMm: 0, heightMm: 0, thicknessMm: 0,
    jointWidthMm: 0, colorHex: "#B0ACAA", category: "floor", finish: "Phụ gia làm cứng",
    patternType: "stack", locations: ["Hầm", "Ramp"],
  },
  "GT1": {
    name: "Đá Granite Bóng Mờ", widthMm: 600, heightMm: 300, thicknessMm: 20,
    jointWidthMm: 2, colorHex: "#78716C", category: "stair", finish: "Mờ",
    patternType: "running_bond", locations: ["Cầu thang ST1", "Cầu thang ST2"],
  },
  "P1": {
    name: "Sơn Nước Trắng Ngoại Thất", widthMm: 0, heightMm: 0, thicknessMm: 2,
    jointWidthMm: 0, colorHex: "#FFFFFF", category: "wall", finish: "Bóng nhẹ",
    patternType: "stack", locations: ["Mặt ngoài"],
  },
  "P3": {
    name: "Sơn Nước Xám", widthMm: 0, heightMm: 0, thicknessMm: 2,
    jointWidthMm: 0, colorHex: "#9CA3AF", category: "wall", finish: "Nhám",
    patternType: "stack", locations: ["Mặt đứng phụ"],
  },
  "P4": {
    name: "Ốp Đá Chẻ", widthMm: 300, heightMm: 150, thicknessMm: 20,
    jointWidthMm: 10, colorHex: "#6B7280", category: "wall", finish: "Tự nhiên",
    patternType: "running_bond", locations: ["Tường đặc trưng"],
  },
  "P5": {
    name: "Tường Kẻ Ron 15@300", widthMm: 300, heightMm: 300, thicknessMm: 15,
    jointWidthMm: 15, colorHex: "#E5E7EB", category: "wall", finish: "Nhẵn",
    patternType: "stack", locations: ["Tường nội thất"],
  },
};

const MATERIAL_CODE_REGEX = /\b(FT-\d{2}|GT\d+|P\d+|WD\d+|MC\d+)\b/gi;
const DOOR_CODE_REGEX = /\bD(\d{2})\b/g;
const WINDOW_CODE_REGEX = /\bW(\d{1,2})\b/g;
const DIM_REGEX = /(\d{3,4})\s*[xX×]\s*(\d{3,4})(?:\s*[xX×]\s*(\d{1,3}))?/g;

// ─── PDF Text Extraction ──────────────────────────────────────────────────────

export async function extractFromPDF(pdfPath: string): Promise<DocumentExtractResult> {
  const warnings: string[] = [];

  if (!fs.existsSync(pdfPath)) {
    throw new Error(`PDF not found: ${pdfPath}`);
  }

  const buffer = fs.readFileSync(pdfPath);
  const data = await parsePDF(buffer);

  const result = parseDocumentText(data.text, pdfPath, data.numpages, warnings);
  return result;
}

// ─── DWG Binary Reader ────────────────────────────────────────────────────────
// DWG is binary. We read the ASCII text strings embedded in the binary to
// extract dimension annotations and block reference names (D01, W1, FT-01, etc.)

export async function extractFromDWG(dwgPath: string): Promise<DocumentExtractResult> {
  const warnings: string[] = [];

  if (!fs.existsSync(dwgPath)) {
    throw new Error(`DWG not found: ${dwgPath}`);
  }

  const buffer = fs.readFileSync(dwgPath);

  // Extract printable ASCII strings from binary DWG (min length 4)
  const strings = extractStringsFromBinary(buffer, 4);
  const combinedText = strings.join(" ");

  warnings.push("DWG parsed via binary string extraction — some text may be missing or garbled. For full accuracy, open in Revit and use get_cad_entities tool.");

  return parseDocumentText(combinedText, dwgPath, 1, warnings);
}

function extractStringsFromBinary(buffer: Buffer, minLen: number): string[] {
  const results: string[] = [];
  let current = "";

  for (let i = 0; i < buffer.length; i++) {
    const byte = buffer[i];
    // Printable ASCII + common Vietnamese chars in UTF-8 low bytes
    if (byte >= 0x20 && byte <= 0x7E) {
      current += String.fromCharCode(byte);
    } else {
      if (current.length >= minLen) {
        results.push(current.trim());
      }
      current = "";
    }
  }

  if (current.length >= minLen) results.push(current.trim());
  return results;
}

// ─── Core Text Parser ─────────────────────────────────────────────────────────

function parseDocumentText(
  text: string,
  sourceFile: string,
  pageCount: number,
  warnings: string[]
): DocumentExtractResult {
  const materials = extractMaterials(text, warnings);
  const doors = extractDoors(text, warnings);
  const windows = extractWindows(text, warnings);

  return {
    materials,
    doors,
    windows,
    rawText: text.substring(0, 5000), // first 5000 chars for reference
    sourceFile,
    pageCount,
    warnings,
  };
}

function extractMaterials(text: string, warnings: string[]): MaterialSpec[] {
  const found = new Map<string, MaterialSpec>();

  // Find all material codes in text
  const codeMatches = [...text.matchAll(MATERIAL_CODE_REGEX)];
  for (const match of codeMatches) {
    const code = match[1].toUpperCase();
    if (found.has(code)) continue;

    const known = KNOWN_MATERIALS[code];
    if (known) {
      found.set(code, {
        code,
        name: known.name ?? code,
        widthMm: known.widthMm ?? 0,
        heightMm: known.heightMm ?? 0,
        thicknessMm: known.thicknessMm ?? 10,
        jointWidthMm: known.jointWidthMm ?? 3,
        colorHex: known.colorHex ?? "#CCCCCC",
        category: known.category ?? "floor",
        locations: known.locations ?? [],
        patternType: known.patternType ?? "stack",
        finish: known.finish ?? "",
        notes: "",
      });
    } else {
      // Unknown code — try to infer dimensions from nearby text
      const idx = match.index ?? 0;
      const context = text.substring(Math.max(0, idx - 80), idx + 120);
      const dims = [...context.matchAll(DIM_REGEX)];
      const dim = dims[0];

      found.set(code, {
        code,
        name: code,
        widthMm: dim ? parseInt(dim[1]) : 300,
        heightMm: dim ? parseInt(dim[2]) : 300,
        thicknessMm: dim && dim[3] ? parseInt(dim[3]) : 10,
        jointWidthMm: 3,
        colorHex: "#CCCCCC",
        category: "floor",
        locations: [],
        patternType: "stack",
        finish: "",
        notes: `Unknown code — auto-detected from context: "${context.substring(0, 60)}"`,
      });

      warnings.push(`Unknown material code ${code} — used inferred dimensions`);
    }
  }

  // Always include known materials even if not found in text (project standard)
  for (const [code, known] of Object.entries(KNOWN_MATERIALS)) {
    if (!found.has(code) && known.widthMm !== undefined) {
      found.set(code, {
        code,
        name: known.name ?? code,
        widthMm: known.widthMm,
        heightMm: known.heightMm ?? known.widthMm,
        thicknessMm: known.thicknessMm ?? 10,
        jointWidthMm: known.jointWidthMm ?? 3,
        colorHex: known.colorHex ?? "#CCCCCC",
        category: known.category ?? "floor",
        locations: known.locations ?? [],
        patternType: known.patternType ?? "stack",
        finish: known.finish ?? "",
        notes: "Added from project standard (not detected in text)",
      });
    }
  }

  return [...found.values()].sort((a, b) => a.code.localeCompare(b.code));
}

function extractDoors(text: string, warnings: string[]): DoorSpec[] {
  const found = new Map<string, DoorSpec>();
  const matches = [...text.matchAll(DOOR_CODE_REGEX)];

  for (const match of matches) {
    const code = `D${match[1]}`;
    if (found.has(code)) continue;

    const idx = match.index ?? 0;
    const context = text.substring(Math.max(0, idx - 60), idx + 120);
    const dims = [...context.matchAll(DIM_REGEX)];
    const dim = dims[0];

    found.set(code, {
      code,
      widthMm: dim ? parseInt(dim[1]) : 900,
      heightMm: dim ? parseInt(dim[2]) : 2100,
      type: "single",
      material: "Nhôm kính",
      glassType: "Kính tempered 8mm",
      notes: context.substring(0, 80).replace(/\s+/g, " "),
    });
  }

  return [...found.values()].sort((a, b) => a.code.localeCompare(b.code));
}

function extractWindows(text: string, warnings: string[]): WindowSpec[] {
  const found = new Map<string, WindowSpec>();
  const matches = [...text.matchAll(WINDOW_CODE_REGEX)];

  for (const match of matches) {
    const num = parseInt(match[1]);
    if (num > 20) continue; // avoid false positives
    const code = `W${match[1]}`;
    if (found.has(code)) continue;

    const idx = match.index ?? 0;
    const context = text.substring(Math.max(0, idx - 60), idx + 120);
    const dims = [...context.matchAll(DIM_REGEX)];
    const dim = dims[0];

    found.set(code, {
      code,
      widthMm: dim ? parseInt(dim[1]) : 1200,
      heightMm: dim ? parseInt(dim[2]) : 1500,
      sillHeightMm: 900,
      type: "casement",
      material: "Nhôm kính",
      glassType: "Kính Low-E 6mm",
      notes: context.substring(0, 80).replace(/\s+/g, " "),
    });
  }

  return [...found.values()].sort((a, b) => a.code.localeCompare(b.code));
}

// ─── Dispatcher ───────────────────────────────────────────────────────────────

export async function extractFromDocument(filePath: string): Promise<DocumentExtractResult> {
  const ext = path.extname(filePath).toLowerCase();
  if (ext === ".pdf") return extractFromPDF(filePath);
  if (ext === ".dwg" || ext === ".dxf") return extractFromDWG(filePath);
  throw new Error(`Unsupported file format: ${ext}. Supported: .pdf, .dwg, .dxf`);
}
