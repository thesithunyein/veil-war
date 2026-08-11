import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(__dirname, "..");
const src = path.join(root, "web-sandbox");
const dest = path.join(root, "public");

function copyRecursive(from, to) {
  if (!fs.existsSync(from)) throw new Error("Missing web-sandbox");
  fs.mkdirSync(to, { recursive: true });
  for (const entry of fs.readdirSync(from, { withFileTypes: true })) {
    if (entry.name === ".git") continue;
    const a = path.join(from, entry.name);
    const b = path.join(to, entry.name);
    if (entry.isDirectory()) copyRecursive(a, b);
    else fs.copyFileSync(a, b);
  }
}

copyRecursive(src, dest);
console.log("Synced web-sandbox → public");
