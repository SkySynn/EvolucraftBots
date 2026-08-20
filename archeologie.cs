/*
 * Minecraft: 1.21.7+
 * CheatUtils: 3.14.0+
 *
 * Detecte l'item dans le suspicious sand/gravel des le 1er tick de brush.
 * Affiche dans le chat + overlay + log dans un fichier.
 * Toggle pour activer/desactiver.
 */

// ════════════ CONFIG ════════════════════════════════════
static string LOG_FILE = "C:\\Users\\thomas\\Downloads\\archeologie_log.txt";
// ════════════════════════════════════════════════════════

static boolean active     = false;
static int     lastX      = 0;
static int     lastY      = 0;
static int     lastZ      = 0;
static string  lastItem   = "";

void appendLog(string entry) {
    string existing = os.files.readAllText(LOG_FILE);
    os.files.writeAllText(LOG_FILE, existing + entry + "\n");
}

async void main() {
    ui.systemMessage("§e[Archéo] Actif. Log : " + LOG_FILE);

    while (active) {
        if (player.isDestroyingBlock()) {
            int bx = player.getDestroyingBlockX();
            int by = player.getDestroyingBlockY();
            int bz = player.getDestroyingBlockZ();

            // Nouveau bloc → on lit le NBT
            if (bx != lastX || by != lastY || bz != lastZ) {
                lastX = bx; lastY = by; lastZ = bz;

                string blockId = game.blocks.getId(bx, by, bz);
                if (blockId == "minecraft:suspicious_sand" || blockId == "minecraft:suspicious_gravel") {
                    await delay.ticks(2); // laisse le serveur envoyer l'item

                    string nbt = game.blocks.getNbt(bx, by, bz).toString();

                    if (nbt.contains("item:")) {
                        string ts    = time.getSystemTimeFormatted("HH:mm:ss");
                        string entry = "[" + ts + "] " + bx + "/" + by + "/" + bz + " → " + nbt;

                        appendLog(entry);
                        lastItem = nbt;

                        ui.systemMessage("§a[Archéo] §f" + nbt);
                    }
                }
            }
        }

        // Overlay persistant — dernier item detecte
        if (lastItem != "") {
            ui.overlayMessage("§e[Archéo] §f" + lastItem);
        }

        await delay.ticks(2);
    }

    ui.systemMessage("§7[Archéo] Desactive.");
}

// Toggle
active = !active;
if (active) {
    lastX = 0; lastY = 0; lastZ = 0;
    lastItem = "";
    main();
} else {
    ui.systemMessage("§7[Archéo] Desactive.");
}
