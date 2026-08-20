/*
 * Minecraft: 1.21+
 * CheatUtils: 3.3.0+
 * Advanced Scripting: OFF
 *
 * CONFIG : change a_craft (nombre) pour choisir l'item
 *   0 = bottes      (4 lingots)
 *   1 = pantalon    (7 lingots)
 *   2 = plastron    (8 lingots)
 *   3 = casque      (5 lingots)
 *   4 = epee        (2 lingots + 1 baton)
 *   5 = hache       (3 lingots + 2 batons)
 * Boucle : craft item fer → XP sur mobs → enchant → jette item → recommence
 */

// ══ CONFIG ══════════════════════════════════════════════════════════
static int a_craft = 0;
// ════════════════════════════════════════════════════════════════════

static boolean active = false;
static int craftX = 0;
static int craftY = 0;
static int craftZ = 0;
static int enchX  = 0;
static int enchY  = 0;
static int enchZ  = 0;

// ── Helpers config ──────────────────────────────────────────────────

int getIngotCount() {
    if (a_craft == 0) return 4;
    if (a_craft == 1) return 7;
    if (a_craft == 2) return 8;
    if (a_craft == 3) return 5;
    if (a_craft == 4) return 2;
    if (a_craft == 5) return 3;
    return 4;
}

int getStickCount() {
    if (a_craft == 4) return 1;
    if (a_craft == 5) return 2;
    return 0;
}

boolean isTargetUnenchanted(int slot) {
    if (!containers.hasItemAtSlot(slot)) return false;
    boolean match = false;
    if (a_craft == 0) match = (containers.getItemAtSlot(slot).item.id == "minecraft:iron_boots");
    if (a_craft == 1) match = (containers.getItemAtSlot(slot).item.id == "minecraft:iron_leggings");
    if (a_craft == 2) match = (containers.getItemAtSlot(slot).item.id == "minecraft:iron_chestplate");
    if (a_craft == 3) match = (containers.getItemAtSlot(slot).item.id == "minecraft:iron_helmet");
    if (a_craft == 4) match = (containers.getItemAtSlot(slot).item.id == "minecraft:iron_sword");
    if (a_craft == 5) match = (containers.getItemAtSlot(slot).item.id == "minecraft:iron_axe");
    if (!match) return false;
    return containers.getItemAtSlot(slot).enchantments.length == 0;
}

int countTargetItems() {
    if (a_craft == 0) return inventory.getCount("minecraft:iron_boots");
    if (a_craft == 1) return inventory.getCount("minecraft:iron_leggings");
    if (a_craft == 2) return inventory.getCount("minecraft:iron_chestplate");
    if (a_craft == 3) return inventory.getCount("minecraft:iron_helmet");
    if (a_craft == 4) return inventory.getCount("minecraft:iron_sword");
    if (a_craft == 5) return inventory.getCount("minecraft:iron_axe");
    return 0;
}

// ── Scan blocs ──────────────────────────────────────────────────────

boolean findCraftTable() {
    int px = math.floor(player.getX());
    int py = math.floor(player.getY());
    int pz = math.floor(player.getZ());
    for (int dx = -6; dx <= 6; dx++) {
        for (int dy = -3; dy <= 3; dy++) {
            for (int dz = -6; dz <= 6; dz++) {
                int bx = px + dx;
                int by = py + dy;
                int bz = pz + dz;
                if (game.blocks.getId(bx, by, bz) == "minecraft:crafting_table") {
                    craftX = bx; craftY = by; craftZ = bz;
                    return true;
                }
            }
        }
    }
    return false;
}

boolean findEnchTable() {
    int px = math.floor(player.getX());
    int py = math.floor(player.getY());
    int pz = math.floor(player.getZ());
    for (int dx = -6; dx <= 6; dx++) {
        for (int dy = -3; dy <= 3; dy++) {
            for (int dz = -6; dz <= 6; dz++) {
                int bx = px + dx;
                int by = py + dy;
                int bz = pz + dz;
                if (game.blocks.getId(bx, by, bz) == "minecraft:enchanting_table") {
                    enchX = bx; enchY = by; enchZ = bz;
                    return true;
                }
            }
        }
    }
    return false;
}

// ── Mobs ────────────────────────────────────────────────────────────

int findClosestMob() {
    int id = game.entities.findClosestEntityById("minecraft:blaze");
    if (id > 0) return id;
    id = game.entities.findClosestEntityById("minecraft:cave_spider");
    if (id > 0) return id;
    id = game.entities.findClosestEntityById("minecraft:zombie");
    if (id > 0) return id;
    id = game.entities.findClosestEntityById("minecraft:skeleton");
    if (id > 0) return id;
    id = game.entities.findClosestEntityById("minecraft:creeper");
    if (id > 0) return id;
    return -1;
}

// ── Craft helpers (sync, pas d'await) ───────────────────────────────

boolean placeIngot(int craftSlot) {
    int invSlot = -1;
    for (int i = 10; i < 46; i++) {
        if (containers.hasItemAtSlot(i) && containers.getItemAtSlot(i).item.id == "minecraft:iron_ingot") {
            invSlot = i; break;
        }
    }
    if (invSlot < 0) return false;
    containers.click(invSlot, 0, "PICKUP");
    containers.click(craftSlot, 1, "PICKUP");
    containers.click(invSlot, 0, "PICKUP");
    return true;
}

boolean placeStick(int craftSlot) {
    int invSlot = -1;
    for (int i = 10; i < 46; i++) {
        if (containers.hasItemAtSlot(i) && containers.getItemAtSlot(i).item.id == "minecraft:stick") {
            invSlot = i; break;
        }
    }
    if (invSlot < 0) return false;
    containers.click(invSlot, 0, "PICKUP");
    containers.click(craftSlot, 1, "PICKUP");
    containers.click(invSlot, 0, "PICKUP");
    return true;
}

// ── Main ─────────────────────────────────────────────────────────────

async void main() {
    if (!findEnchTable()) {
        ui.systemMessage("§c[Auto] Pas d'enchanting table a portee.");
        active = false;
        return;
    }

    int needIngots     = getIngotCount();
    int needSticks     = getStickCount();
    int totalEnchanted = 0;
    ui.systemMessage("§e[Auto] Demarre. Item=" + a_craft + ". Appuie encore pour stopper.");

    while (active) {

        // ══ PHASE 1 : CRAFT ════════════════════════════════════════

        if (inventory.getCount("minecraft:iron_ingot") >= needIngots) {
            if (needSticks > 0 && inventory.getCount("minecraft:stick") < needSticks) {
                ui.systemMessage("§c[Craft] Pas assez de batons (" + needSticks + " requis).");
                active = false;
                return;
            }
            if (!findCraftTable()) {
                ui.systemMessage("§c[Craft] Pas de crafting table a portee.");
                active = false;
                return;
            }
            player.lookAt(craftX + 0.5, craftY + 0.5, craftZ + 0.5);
            await delay.ticks(3);
            keys.use.click();
            await delay.ticks(5);
            if (!containers.getMenuClass().endsWith("CraftingMenu")) {
                ui.systemMessage("§c[Craft] Echec ouverture.");
                active = false;
                return;
            }

            boolean keepCrafting = true;
            while (keepCrafting && active) {
                int emptySlots = 0;
                for (int i = 10; i < 46; i++) {
                    if (!containers.hasItemAtSlot(i)) emptySlots = emptySlots + 1;
                }
                if (emptySlots <= 1) { keepCrafting = false; break; }
                if (inventory.getCount("minecraft:iron_ingot") < needIngots) { keepCrafting = false; break; }
                if (needSticks > 0 && inventory.getCount("minecraft:stick") < needSticks) { keepCrafting = false; break; }

                // ── Pattern craft avec await entre chaque click ──────────
                // Grille : 1 2 3 / 4 5 6 / 7 8 9  (slot 0 = output)
                if (a_craft == 0) {
                    // bottes : X . X / X . X / . . .
                    placeIngot(1); await delay.ticks(2);
                    placeIngot(3); await delay.ticks(2);
                    placeIngot(4); await delay.ticks(2);
                    placeIngot(6); await delay.ticks(2);
                } else if (a_craft == 1) {
                    // pantalon : X X X / X . X / X . X
                    placeIngot(1); await delay.ticks(2);
                    placeIngot(2); await delay.ticks(2);
                    placeIngot(3); await delay.ticks(2);
                    placeIngot(4); await delay.ticks(2);
                    placeIngot(6); await delay.ticks(2);
                    placeIngot(7); await delay.ticks(2);
                    placeIngot(9); await delay.ticks(2);
                } else if (a_craft == 2) {
                    // plastron : X . X / X X X / X X X
                    placeIngot(1); await delay.ticks(2);
                    placeIngot(3); await delay.ticks(2);
                    placeIngot(4); await delay.ticks(2);
                    placeIngot(5); await delay.ticks(2);
                    placeIngot(6); await delay.ticks(2);
                    placeIngot(7); await delay.ticks(2);
                    placeIngot(8); await delay.ticks(2);
                    placeIngot(9); await delay.ticks(2);
                } else if (a_craft == 3) {
                    // casque : X X X / X . X / . . .
                    placeIngot(1); await delay.ticks(2);
                    placeIngot(2); await delay.ticks(2);
                    placeIngot(3); await delay.ticks(2);
                    placeIngot(4); await delay.ticks(2);
                    placeIngot(6); await delay.ticks(2);
                } else if (a_craft == 4) {
                    // epee : . X . / . X . / . B .
                    placeIngot(2); await delay.ticks(2);
                    placeIngot(5); await delay.ticks(2);
                    placeStick(8); await delay.ticks(2);
                } else if (a_craft == 5) {
                    // hache : X X . / X B . / . B .
                    placeIngot(1); await delay.ticks(2);
                    placeIngot(2); await delay.ticks(2);
                    placeIngot(4); await delay.ticks(2);
                    placeStick(5); await delay.ticks(2);
                    placeStick(8); await delay.ticks(2);
                } else {
                    ui.systemMessage("§c[Config] a_craft invalide : " + a_craft);
                    keepCrafting = false;
                    break;
                }

                if (!containers.hasItemAtSlot(0)) { keepCrafting = false; break; }
                containers.click(0, 0, "QUICK_MOVE");
                await delay.ticks(2);
            }
            containers.close();
            await delay.ticks(10);
        }

        // ══ PHASE XP : 50 FRAPPES ══════════════════════════════════

        ui.systemMessage("§6[XP] 50 frappes...");
        for (int hit = 0; hit < 50 && active; hit++) {
            if (player.getHealth() < 6) {
                ui.systemMessage("§c[XP] Vie trop basse, pause.");
                await delay.ticks(40);
            }
            int mob = findClosestMob();
            if (mob > 0) {
                player.lookAt(game.entities.getX(mob), game.entities.getY(mob) + 0.3, game.entities.getZ(mob));
            }
            keys.attack.click();
            await delay.ticks(10);
        }
        ui.systemMessage("§6[XP] Frappe terminee.");

        // Jette ficelle + yeux d'araignee
        if (findCraftTable()) {
            player.lookAt(craftX + 0.5, craftY + 0.5, craftZ + 0.5);
            await delay.ticks(3);
            keys.use.click();
            await delay.ticks(5);
            if (containers.getMenuClass().endsWith("CraftingMenu")) {
                for (int i = 9; i < 45; i++) {
                    if (!containers.hasItemAtSlot(i)) continue;
                    if (containers.getItemAtSlot(i).item.id == "minecraft:string" ||
                        containers.getItemAtSlot(i).item.id == "minecraft:spider_eye") {
                        containers.click(i, 1, "THROW");
                        await delay.ticks(1);
                    }
                }
                containers.close();
                await delay.ticks(5);
            }
        }

        // ══ PHASE 2 : ENCHANT ══════════════════════════════════════

        if (inventory.getCount("minecraft:lapis_lazuli") < 3) {
            ui.systemMessage("§c[Enchant] Plus de lapis. Arret.");
            active = false;
            return;
        }

        player.lookAt(enchX + 0.5, enchY + 0.75, enchZ + 0.5);
        await delay.ticks(3);
        keys.use.click();
        await delay.ticks(6);
        if (!containers.getMenuClass().endsWith("EnchantmentMenu")) {
            ui.systemMessage("§c[Enchant] Echec ouverture.");
            active = false;
            return;
        }

        int guiScale = window.getGuiScale();
        int guiLeft  = (window.getGuiWidth()  - 176) / 2;
        int guiTop   = (window.getGuiHeight() - 166) / 2;
        int btnX     = (guiLeft + 114) * guiScale;
        int btnY     = (guiTop  +  23) * guiScale;
        window.emulateMouseMove(btnX, btnY);
        await delay.ticks(1);

        boolean hasMore = true;
        while (hasMore && active) {
            if (inventory.getCount("minecraft:lapis_lazuli") < 3) {
                ui.systemMessage("§c[Enchant] Plus de lapis. Arret.");
                containers.close();
                active = false;
                return;
            }

            int itemSlot  = -1;
            int lapisSlot = -1;
            for (int i = 2; i < 38; i++) {
                if (!containers.hasItemAtSlot(i)) continue;
                if (itemSlot < 0 && isTargetUnenchanted(i)) {
                    itemSlot = i;
                }
                if (containers.getItemAtSlot(i).item.id == "minecraft:lapis_lazuli" && lapisSlot < 0) {
                    lapisSlot = i;
                }
            }

            if (itemSlot < 0) { hasMore = false; break; }

            containers.click(itemSlot, 0, "PICKUP");
            containers.click(0, 0, "PICKUP");
            containers.click(lapisSlot, 0, "PICKUP");
            containers.click(1, 0, "PICKUP");
            await delay.ticks(6);

            window.emulateMouseButtonEvent(0, true);
            await delay.ticks(1);
            window.emulateMouseButtonEvent(0, false);
            await delay.ticks(4);

            containers.click(1, 0, "QUICK_MOVE");
            player.lookAt(player.getX() + 100.0, player.getY() + 1.62, player.getZ());
            await delay.ticks(1);
            containers.click(0, 1, "THROW");
            await delay.ticks(2);

            totalEnchanted = totalEnchanted + 1;
            ui.systemMessage("§a[Auto] " + totalEnchanted + " items enchantes.");
        }

        containers.close();
        await delay.ticks(4);

        if (inventory.getCount("minecraft:iron_ingot") < needIngots && countTargetItems() == 0) {
            ui.systemMessage("§a[Auto] Termine ! " + totalEnchanted + " items enchantes.");
            active = false;
            return;
        }
    }

    ui.systemMessage("§7[Auto] Stoppe. " + totalEnchanted + " items enchantes.");
}

// ── Toggle ───────────────────────────────────────────────────────────
active = !active;
if (active) {
    ui.systemMessage("§e[Auto] Active ! Item=" + a_craft);
    main();
} else {
    ui.systemMessage("§7[Auto] Desactive.");
}
