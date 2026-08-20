/*
 * Minecraft: 1.21+
 * CheatUtils: 3.3.0+
 * Advanced Scripting: OFF
 *
 * Iron Chestplate Auto-Farm
 * Loop : trouve fer (inv/coffres) → blocs→lingots → craft plastron → jette → recommence
 * Toggle avec la touche assignee.
 */

// ════════════════════ CONFIG ════════════════════════════
static int  CHEST_RADIUS  = 12;  // rayon recherche coffres (blocs)
static int  TABLE_RADIUS  = 6;   // rayon recherche crafting table (blocs)
static int  LOOP_DELAY    = 5;   // ticks de pause entre chaque cycle
static int  MIN_FREE_SLOTS = 4;  // slots libres min avant de jeter les plastrons
// ════════════════════════════════════════════════════════

static boolean active       = false;
static int     totalCrafted = 0;
static int     craftX = 0;
static int     craftY = 0;
static int     craftZ = 0;

// ── Trouve la crafting table la plus proche ──────────────
boolean findCraftTable() {
    int px = math.floor(player.getX());
    int py = math.floor(player.getY());
    int pz = math.floor(player.getZ());
    for (int dx = -TABLE_RADIUS; dx <= TABLE_RADIUS; dx++) {
        for (int dy = -3; dy <= 3; dy++) {
            for (int dz = -TABLE_RADIUS; dz <= TABLE_RADIUS; dz++) {
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

// ── Regarde et ouvre un bloc, retourne true si container ouvert ──
async boolean openBlock(int bx, int by, int bz) {
    player.lookAt(bx + 0.5, by + 0.5, bz + 0.5);
    await delay.ticks(3);
    keys.use.click();
    await delay.ticks(5);
    return containers.getSlotsSize() > 0;
}

// ── Scan les coffres proches et prend lingots + blocs de fer ──
// enumerateNearbyBlocks retourne les blocs tries du plus proche au plus loin.
// Blocs de fer : 1 seul stack pris en tout (64 blocs = 576 lingots, largement suffisant).
async boolean searchChestsForIron() {
    BlockPos[] nearby = player.enumerateNearbyBlocks(CHEST_RADIUS);
    boolean found    = false;
    boolean tookBlock = false; // 1 stack de blocs de fer max au total

    for (int i = 0; i < nearby.length; i++) {
        if (!active) break;
        BlockPos bp = nearby[i];
        string bid = game.blocks.getId(bp);
        if (bid != "minecraft:chest" && bid != "minecraft:barrel") continue;

        if (!await openBlock(bp.x, bp.y, bp.z)) continue;

        int chestSlots  = containers.getSlotsSize() - 36; // slots coffre = total - 36 slots joueur
        int playerStart = chestSlots;                     // debut des slots joueur dans ce container

        for (int slot = 0; slot < chestSlots; slot++) {
            // Compte slots libres dans l'inventaire joueur — laisse 10 libres
            int freeInv = 0;
            for (int s = playerStart; s < containers.getSlotsSize(); s++) {
                if (!containers.hasItemAtSlot(s)) freeInv = freeInv + 1;
            }
            if (freeInv <= 10) break; // inv presque plein, arrete de prendre

            if (!containers.hasItemAtSlot(slot)) continue;
            string itemId = containers.getItemAtSlot(slot).item.id;
            if (itemId == "minecraft:iron_ingot") {
                containers.click(slot, 0, "QUICK_MOVE");
                await delay.ticks(1);
                found = true;
            } else if (itemId == "minecraft:iron_block" && !tookBlock) {
                containers.click(slot, 0, "QUICK_MOVE"); // 1 seul stack
                await delay.ticks(1);
                found     = true;
                tookBlock = true;
            }
        }

        containers.close();
        await delay.ticks(3);

        if (found) break; // du fer recupere → inutile de chercher d'autres coffres
    }
    return found;
}

// ── Convertit blocs de fer → lingots (crafting table doit etre ouverte) ──
// 1 bloc = 9 lingots via recette vanilla 1.21
async void convertBlocksToIngots() {
    while (active) {
        int blockSlot = -1;
        for (int i = 10; i < 46; i++) {
            if (containers.hasItemAtSlot(i) && containers.getItemAtSlot(i).item.id == "minecraft:iron_block") {
                blockSlot = i;
                break;
            }
        }
        if (blockSlot < 0) break;

        containers.click(blockSlot, 0, "PICKUP"); // prend le stack de blocs
        containers.click(1, 1, "PICKUP");          // pose 1 bloc en slot 1 de la grille
        containers.click(blockSlot, 0, "PICKUP"); // repose le reste
        await delay.ticks(2);

        if (!containers.hasItemAtSlot(0)) break;
        containers.click(0, 0, "QUICK_MOVE"); // prend les 9 lingots
        await delay.ticks(2);
    }
}

// ── Craft plastrons en fer (crafting table doit etre ouverte) ──
// Recette :
//   [I][ ][I]   slots grille: 1  2  3
//   [I][I][I]                 4  5  6
//   [I][I][I]                 7  8  9
// → slot 2 vide, tous les autres = iron_ingot (8 lingots par plastron)
async int craftChestplates() {
    int crafted = 0;
    while (active) {
        boolean failed = false;
        for (int gridSlot = 1; gridSlot <= 9; gridSlot++) {
            if (gridSlot == 2) continue; // slot 2 vide dans la recette plastron
            int invSlot = -1;
            for (int i = 10; i < 46; i++) {
                if (containers.hasItemAtSlot(i) && containers.getItemAtSlot(i).item.id == "minecraft:iron_ingot") {
                    invSlot = i;
                    break;
                }
            }
            if (invSlot < 0) { failed = true; break; }
            containers.click(invSlot, 0, "PICKUP"); // prend le stack
            containers.click(gridSlot, 1, "PICKUP"); // pose 1 lingot dans la grille
            containers.click(invSlot, 0, "PICKUP"); // repose le reste
            await delay.ticks(1);
        }

        if (failed || !containers.hasItemAtSlot(0)) break;
        containers.click(0, 0, "QUICK_MOVE"); // prend le plastron
        await delay.ticks(2);
        crafted = crafted + 1;

        // Compte slots libres — si trop peu, jette les plastrons pour faire de la place
        int freeSlots = 0;
        for (int i = 10; i < 46; i++) {
            if (!containers.hasItemAtSlot(i)) freeSlots = freeSlots + 1;
        }
        if (freeSlots <= MIN_FREE_SLOTS) {
            for (int i = 10; i < 46; i++) {
                if (!containers.isValidSlotIndex(i)) continue;
                if (containers.hasItemAtSlot(i) && containers.getItemAtSlot(i).item.id == "minecraft:iron_chestplate") {
                    containers.click(i, 1, "THROW");
                    await delay.ticks(1);
                }
            }
            ui.overlayMessage("§7[IronFarm] Inv plein → plastrons jetes | Total: " + (totalCrafted + crafted));
        }
    }
    return crafted;
}

// ── Jette tous les plastrons de fer (crafting table doit etre ouverte) ──
// THROW button=1 : jette le stack entier sans passer par le curseur
async void dropChestplates() {
    // Slot 0 (resultat) au cas ou l'inventaire etait plein pendant le craft
    if (containers.hasItemAtSlot(0) && containers.getItemAtSlot(0).item.id == "minecraft:iron_chestplate") {
        containers.click(0, 1, "THROW");
        await delay.ticks(1);
    }
    for (int i = 10; i < 46; i++) {
        if (!containers.isValidSlotIndex(i)) continue;
        if (containers.hasItemAtSlot(i) && containers.getItemAtSlot(i).item.id == "minecraft:iron_chestplate") {
            containers.click(i, 1, "THROW");
            await delay.ticks(1);
        }
    }
}

// ── Boucle principale ─────────────────────────────────────
async void main() {
    if (!findCraftTable()) {
        ui.systemMessage("§c[IronFarm] Pas de crafting table dans " + TABLE_RADIUS + " blocs.");
        active = false;
        return;
    }
    ui.systemMessage("§e[IronFarm] Table trouvee en " + craftX + "/" + craftY + "/" + craftZ + ". Demarre !");

    while (active) {
        // ── Verifie stock de fer ─────────────────────────
        int ingots = inventory.getCount("minecraft:iron_ingot");
        int blocks = inventory.getCount("minecraft:iron_block");
        int totalIron = ingots + blocks * 9;

        if (totalIron < 8) {
            ui.systemMessage("§7[IronFarm] Fer: " + totalIron + " ling. equiv. → cherche coffres...");
            await searchChestsForIron();

            ingots    = inventory.getCount("minecraft:iron_ingot");
            blocks    = inventory.getCount("minecraft:iron_block");
            totalIron = ingots + blocks * 9;

            if (totalIron < 8) {
                ui.systemMessage("§c[IronFarm] Plus de fer. Arret. Total: " + totalCrafted + " plastrons.");
                break;
            }
        }

        // ── Ouvre la crafting table ──────────────────────
        if (!await openBlock(craftX, craftY, craftZ) || !containers.getMenuClass().endsWith("CraftingMenu")) {
            ui.systemMessage("§c[IronFarm] Impossible d'ouvrir la crafting table. Arret.");
            break;
        }

        // ── Convertit blocs → lingots ────────────────────
        if (inventory.getCount("minecraft:iron_block") > 0) {
            await convertBlocksToIngots();
        }

        // ── Craft les plastrons ──────────────────────────
        int crafted = await craftChestplates();
        totalCrafted = totalCrafted + crafted;
        ui.overlayMessage("§e[IronFarm] +" + crafted + " plastrons | Total: " + totalCrafted);

        // ── Jette les plastrons ──────────────────────────
        await dropChestplates();

        containers.close();
        await delay.ticks(LOOP_DELAY);
    }

    if (containers.getSlotsSize() > 0) containers.close();
    active = false;
    ui.systemMessage("§7[IronFarm] Arrete. Total: " + totalCrafted + " plastrons craftes.");
}

// ── Toggle ────────────────────────────────────────────────
active = !active;
if (active) {
    totalCrafted = 0;
    ui.systemMessage("§e[IronFarm] Active !");
    main();
} else {
    ui.systemMessage("§7[IronFarm] Desactive.");
}
