// Bir SQLite yedeginin icinde ne oldugunu ozetler.
// Kullanim: node scripts/inspect-db.js backups\fittrack_2026-07-25_141436.db
//
// Node 22+ ile gelen dahili node:sqlite modulunu kullanir — kurulum gerekmez.
const { DatabaseSync } = require("node:sqlite");
const path = process.argv[2];

if (!path) {
  console.error("Kullanim: node scripts/inspect-db.js <veritabani.db>");
  process.exit(1);
}

const db = new DatabaseSync(path.replace(/\\/g, "/"), { readOnly: true });

const tables = db
  .prepare("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name")
  .all();

console.log("tablo                 kayit");
console.log("-".repeat(28));
let total = 0;
for (const t of tables) {
  const n = db.prepare(`SELECT COUNT(*) c FROM "${t.name}"`).get().c;
  total += n;
  console.log(t.name.padEnd(20), String(n).padStart(6));
}
console.log("-".repeat(28));
console.log("TOPLAM".padEnd(20), String(total).padStart(6));

// Kilo takibi bu uygulamanin omurgasi — ozetini de goster.
const w = db.prepare("SELECT COUNT(*) c, MIN(LoggedAt) a, MAX(LoggedAt) b FROM WeightLogs").get();
if (w.c > 0) {
  console.log(`\nKilo kaydi: ${w.c} adet, ${String(w.a).slice(0, 10)} - ${String(w.b).slice(0, 10)}`);
}

db.close();
