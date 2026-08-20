/*
 * Minecraft: 1.21+
 * CheatUtils: 3.3.0+
 * Advanced Scripting: OFF
 *
 * Auto-attack infinie. Delai fixe 13 ticks (epee standard 1.6 atk/s).
 * Toggle : appuie sur la touche pour activer/desactiver.
 */

static boolean active = false;

async void main() {
    ui.systemMessage("§e[AutoAtk] Demarre.");
    while (active) {
        keys.attack.click();
        await delay.ticks(2); // laisser le cooldown se mettre en place
        // Attendre charge complete (cooldown redescend a 0.0)
        while (player.getAttackCooldown() > 0.0 && active) {
            await delay.ticks(1);
        }
    }
    ui.systemMessage("§7[AutoAtk] Stoppe.");
}

active = !active;
if (active) {
    ui.systemMessage("§e[AutoAtk] Active !");
    main();
} else {
    ui.systemMessage("§7[AutoAtk] Desactive.");
}
