/*
 * egg_hunter.cs — Auto chasseur d'oeufs Evolucraft
 * Minecraft 1.21.7+ / CheatUtils 3.14.0+
 *
 * - Détecte les oeufs via minecraft:interaction entities
 * - Priorise par rareté (mythic > légendaire > epic > rare > commun)
 * - A* pathfinding via pathfinding.findPath() (contourne le labyrinthe)
 * - Humanisation avancée : rotation easing, overshoot, jitter, strafe, timing varié
 * - Dépôt automatique au PNJ
 */

// ═══ CONFIG ═══════════════════════════════════
static int DEPOSIT_X    = 20;
static int DEPOSIT_Y    = -29;
static int DEPOSIT_Z    = 3;
static int ARRIVE_DIST  = 1;   // blocks pour "arrivé" à un waypoint
static int EGG_DIST     = 3;   // blocks pour "arrivé" à l'oeuf
static boolean active   = false;

// ─── Humanisation caméra ─────────────────────
// Vitesse max rotation (degrés/tick) — un humain fait ~15-25°/tick max
static int   CAM_SPEED_MAX   = 18;   // degrés/tick max
static int   CAM_SPEED_MIN   = 3;    // degrés/tick min (near target)
static int   CAM_DEAD        = 2;    // dead zone en degrés
static int   CAM_NOISE_AMP   = 2;    // amplitude bruit gaussien max (degrés)
static int   CAM_NOISE_INT   = 5;    // intervalle bruit (ticks)
// Overshoot : dépasse légèrement la cible puis corrige
static int   OVERSHOOT_MAX   = 6;    // degrés max d'overshoot
static int   OVERSHOOT_DIST  = 15;   // déclenche overshoot si delta > N degrés

// ─── Humanisation déplacement ─────────────────
// Sprint : va alterner sprint/walk selon distance et RNG
static int   SPRINT_DIST     = 6;    // distance min pour sprinter
// Strafe : petits mouvements latéraux occasionnels
static int   STRAFE_CHANCE   = 8;    // 1 chance sur N ticks de straf
static int   STRAFE_TICKS    = 3;    // durée d'un strafe (ticks)
// Pauses courtes
static int   PAUSE_CHANCE    = 60;   // 1 chance sur N ticks de faire une micro-pause
static int   PAUSE_MIN       = 1;    // ticks de pause min
static int   PAUSE_MAX       = 4;    // ticks de pause max
// Délais variés : base ± variation aléatoire
static int   TICK_JITTER     = 2;    // ±N ticks de jitter sur les delays
// ══════════════════════════════════════════════

// ─── État interne caméra ──────────────────────
static int   camNoiseTick   = 0;
// Overshoot state : quand on dépasse la cible, on mémorise combien
static int   overshootYaw   = 0;   // degrés d'overshoot restant à corriger
static int   overshootPitch = 0;
static boolean inOvershoot  = false;

// ─── État interne déplacement ─────────────────
static int   strafeTick     = 0;
static int   strafeDir      = 1;
static int   pauseTick      = 0;
// Stuck detection
static int   stuckTimer     = 0;
static int   stuckLastX     = 0;
static int   stuckLastZ     = 0;
// ══════════════════════════════════════════════

// ─── Pince ────────────────────────────────────

int getEggCount() {
    string[] tooltip = inventory.getMainHand().tooltip;
    for (int i = 0; i < tooltip.length; i++) {
        if (tooltip[i].contains("Oeufs captures")) {
            string[] nums = tooltip[i].getMatches("[0-9]+");
            if (nums.length >= 1) {
                return math.parseInt(nums[0]);
            }
        }
    }
    return 0;
}

int getMaxEggs() {
    string[] tooltip = inventory.getMainHand().tooltip;
    for (int i = 0; i < tooltip.length; i++) {
        if (tooltip[i].contains("Oeufs captures")) {
            string[] nums = tooltip[i].getMatches("[0-9]+");
            if (nums.length >= 2) {
                return math.parseInt(nums[1]);
            }
        }
    }
    return 30;
}

// ─── Rareté ───────────────────────────────────
// 0=inconnu 1=commun 2=rare 3=epic 4=légendaire 5=mythique

int getEggRarity(int interactionId) {
    int ix = math.floor(game.entities.getX(interactionId));
    int iz = math.floor(game.entities.getZ(interactionId));

    int[] displays = game.entities.enumerateById("minecraft:item_display");
    for (int d = 0; d < displays.length; d++) {
        int did = displays[d];
        if (math.floor(game.entities.getX(did)) == ix && math.floor(game.entities.getZ(did)) == iz) {
            string nbt = game.entities.getNbt(did).toString();
            if (nbt.contains("easteregg_mythic"))    return 5;
            if (nbt.contains("easteregg_legendary")) return 4;
            if (nbt.contains("easteregg_epic"))      return 3;
            if (nbt.contains("easteregg_rare"))      return 2;
            if (nbt.contains("easteregg_common"))    return 1;
        }
    }
    return 0;
}

int findBestEgg() {
    int[] eggs = game.entities.enumerateById("minecraft:interaction");
    int bestId     = -1;
    int bestRarity = -1;
    for (int i = 0; i < eggs.length; i++) {
        int r = getEggRarity(eggs[i]);
        if (r > bestRarity) {
            bestRarity = r;
            bestId     = eggs[i];
        }
    }
    return bestId;
}

string rarityName(int r) {
    if (r == 5) return "§5Mythique";
    if (r == 4) return "§6Légendaire";
    if (r == 3) return "§dÉpique";
    if (r == 2) return "§9Rare";
    if (r == 1) return "§fCommun";
    return "§7Inconnu";
}

// ─── Utilitaires ──────────────────────────────

int normalizeAngle(int a) {
    while (a > 180)  a = a - 360;
    while (a < -180) a = a + 360;
    return a;
}

// Clamp entier
int clampInt(int v, int lo, int hi) {
    if (v < lo) return lo;
    if (v > hi) return hi;
    return v;
}

// Abs entier
int absInt(int v) {
    if (v < 0) return -v;
    return v;
}

// Signe d'un entier
int signInt(int v) {
    if (v > 0) return 1;
    if (v < 0) return -1;
    return 0;
}

// Délai humanisé : N ± TICK_JITTER ticks
async void humanDelay(int baseTicks) {
    int jitter = math.floor((math.random() - 0.5) * TICK_JITTER * 2);
    int total = baseTicks + jitter;
    if (total < 1) total = 1;
    await delay.ticks(total);
}

// ─── Camera humanisée ─────────────────────────
//
// Technique : easing proportionnel + dead zone + bruit gaussien simulé +
// overshoot-puis-correction (comme un humain qui dépasse légèrement).
//
// Principe easing :
//   speed = clamp(|delta| * factor, MIN, MAX)
//   → rotation rapide quand loin, douce quand proche
//
// Overshoot :
//   Si |delta| > OVERSHOOT_DIST et non en overshoot :
//     on ajoute un dépassement aléatoire (0..OVERSHOOT_MAX)
//     L'overshoot se résorbe progressivement les ticks suivants.
//
// Bruit gaussien simulé (somme de 3 random → distribution en cloche) :
//   Appliqué 1 tick sur CAM_NOISE_INT pour éviter oscillation permanente.

void smoothLookAt(int tx, int ty, int tz) {
    int px    = math.floor(player.getX());
    int py    = math.floor(player.getY());
    int pz    = math.floor(player.getZ());
    int dx    = tx - px;
    int dz    = tz - pz;
    int dy    = ty - py - 1;
    int horiz = math.floor(math.sqrt(dx * dx + dz * dz));
    if (horiz < 1) horiz = 1;

    int wantYaw   = -math.floor(math.degrees.atan2(dx, dz));
    int wantPitch = -math.floor(math.degrees.atan2(dy, horiz));
    int curYaw    = math.floor(player.getYRot());
    int curPitch  = math.floor(player.getXRot());

    int dYaw   = normalizeAngle(wantYaw - curYaw);
    int dPitch = wantPitch - curPitch;

    // Dead zone : ignore micro-corrections
    if (dYaw > -CAM_DEAD && dYaw < CAM_DEAD)     dYaw   = 0;
    if (dPitch > -CAM_DEAD && dPitch < CAM_DEAD) dPitch = 0;

    // ── Overshoot ──────────────────────────────────────────────────────────
    // Si on vient de finir un overshooting, on applique la correction d'abord
    if (inOvershoot) {
        // Réduire progressivement l'overshoot (correction lente → humain)
        int corrYaw = signInt(overshootYaw) * clampInt(absInt(overshootYaw), 1, CAM_SPEED_MIN + 1);
        int corrPitch = signInt(overshootPitch) * clampInt(absInt(overshootPitch), 1, CAM_SPEED_MIN + 1);
        overshootYaw   = overshootYaw   - corrYaw;
        overshootPitch = overshootPitch - corrPitch;
        if (overshootYaw == 0 && overshootPitch == 0) inOvershoot = false;
        // Applique la correction seulement si ça n'empire pas la direction réelle
        player.setYRot(curYaw - corrYaw);
        player.setXRot(curPitch - corrPitch);
        return;
    }

    // Déclenche overshoot : quand le delta est grand et qu'on commence à viser
    if (!inOvershoot && absInt(dYaw) > OVERSHOOT_DIST) {
        int os = math.floor(math.random() * OVERSHOOT_MAX);
        if (math.random() > 0.5) os = -os;
        overshootYaw   = os;
        overshootPitch = math.floor(math.random() * 2) - 1; // -1, 0 ou +1
        if (os != 0) inOvershoot = true;
    }

    // ── Easing proportionnel ───────────────────────────────────────────────
    // speed = sqrt(|delta|) * facteur → courbe naturelle d'accélération
    // Un vrai humain accélère puis freine vers la cible.
    int absYaw   = absInt(dYaw);
    int absPitch = absInt(dPitch);

    // facteur = sqrt(delta)/sqrt(max) * (MAX-MIN) + MIN
    // Simplifié : speed proportionnel au delta, clampé
    int speedYaw = clampInt(absYaw / 3, CAM_SPEED_MIN, CAM_SPEED_MAX);
    // Pour le pitch, mouvement plus lent (physique réelle souris)
    int speedPitch = clampInt(absPitch / 4, CAM_SPEED_MIN - 1, CAM_SPEED_MAX - 3);
    if (speedPitch < 1) speedPitch = 1;

    int moveYaw   = signInt(dYaw)   * clampInt(absYaw,   0, speedYaw);
    int movePitch = signInt(dPitch) * clampInt(absPitch, 0, speedPitch);

    // ── Bruit gaussien simulé ──────────────────────────────────────────────
    // Somme de 3 random U(0,1) → distribution approximant une gaussienne
    // Appliqué que 1 tick sur CAM_NOISE_INT pour éviter oscillation visible.
    camNoiseTick = camNoiseTick + 1;
    int noiseYaw   = 0;
    int noisePitch = 0;
    if (camNoiseTick >= CAM_NOISE_INT) {
        camNoiseTick = 0;
        // Approx gaussienne centrée : somme 3 rand - 1.5, scalée
        int rawNoise = math.floor((math.random() + math.random() + math.random() - 1) * CAM_NOISE_AMP);
        noiseYaw   = rawNoise;
        // Petit bruit pitch indépendant, amplitude plus faible
        noisePitch = math.floor((math.random() - 0.5) * CAM_NOISE_AMP);
    }

    if (dYaw != 0 || noiseYaw != 0)   player.setYRot(curYaw   + moveYaw   + noiseYaw);
    if (dPitch != 0 || noisePitch != 0) player.setXRot(curPitch + movePitch + noisePitch);
}

// ─── Humanisation déplacement ─────────────────
//
// Gère sprint/walk selon distance, micro-pauses aléatoires,
// strafes latéraux occasionnels.
//
// Retourne true si une pause est en cours (le caller doit skipper followPath).

boolean humanizeMovement(int distToWaypoint) {
    // ── Micro-pause ────────────────────────────────────────────────────────
    if (pauseTick > 0) {
        keys.up.setDown(false);
        keys.sprint.setDown(false);
        keys.left.setDown(false);
        keys.right.setDown(false);
        pauseTick = pauseTick - 1;
        return true;  // en pause
    }
    // Chance de démarrer une pause
    if (math.random() < (1.0 / PAUSE_CHANCE)) {
        int dur = PAUSE_MIN + math.floor(math.random() * (PAUSE_MAX - PAUSE_MIN + 1));
        pauseTick = dur;
        return true;
    }

    // ── Sprint / Walk ──────────────────────────────────────────────────────
    // Ne sprinte pas systématiquement : parfois walk même si loin (humain distrait)
    boolean doSprint = false;
    if (distToWaypoint > SPRINT_DIST) {
        // 85% de chance de sprinter quand loin
        doSprint = math.random() > 0.15;
    }
    // Proche de la cible : jamais de sprint (précision)
    if (distToWaypoint <= 2) doSprint = false;
    keys.sprint.setDown(doSprint);

    // ── Strafe ─────────────────────────────────────────────────────────────
    // Désactivé en espace étroit (mur adjacent) pour éviter de se coincer
    int spx = math.floor(player.getX());
    int spy = math.floor(player.getY());
    int spz = math.floor(player.getZ());
    boolean tightSpace =
        game.blocks.getId(spx + 1, spy, spz) != "minecraft:air" ||
        game.blocks.getId(spx - 1, spy, spz) != "minecraft:air" ||
        game.blocks.getId(spx, spy, spz + 1) != "minecraft:air" ||
        game.blocks.getId(spx, spy, spz - 1) != "minecraft:air";

    if (tightSpace) {
        keys.left.setDown(false);
        keys.right.setDown(false);
        strafeTick = 0;
    } else if (strafeTick > 0) {
        if (strafeDir > 0) {
            keys.left.setDown(true);
            keys.right.setDown(false);
        } else {
            keys.left.setDown(false);
            keys.right.setDown(true);
        }
        strafeTick = strafeTick - 1;
    } else {
        keys.left.setDown(false);
        keys.right.setDown(false);
        if (distToWaypoint > 3 && math.random() < (1.0 / STRAFE_CHANCE)) {
            strafeTick = STRAFE_TICKS;
            strafeDir = strafeDir * -1;
        }
    }

    return false;
}

// ─── Navigation sur chemin A* ─────────────────

// Suit le chemin A* actuel tick par tick. Retourne true si waypoint atteint.
boolean followPath(int arriveBlocks) {
    if (pathfinding.isDone()) {
        keys.up.setDown(false);
        keys.sprint.setDown(false);
        return true;
    }

    int wx   = pathfinding.getWaypointX();
    int wy   = pathfinding.getWaypointY();
    int wz   = pathfinding.getWaypointZ();

    smoothLookAt(wx, wy, wz);

    int dx   = wx - math.floor(player.getX());
    int dz   = wz - math.floor(player.getZ());
    int dist = math.floor(math.sqrt(dx * dx + dz * dz));

    if (dist <= arriveBlocks) {
        boolean hasMore = pathfinding.advance();
        if (!hasMore) {
            keys.up.setDown(false);
            keys.sprint.setDown(false);
            return true;
        }
    }

    // Humanisation du déplacement
    boolean inPause = humanizeMovement(dist);
    if (!inPause) {
        keys.up.setDown(true);
    }

    // Jump si prochain waypoint plus haut ou obstacle au niveau pied
    if (!inPause) {
        int wy2 = pathfinding.getWaypointY();
        int py2 = math.floor(player.getY());
        if (wy2 > py2) {
            keys.jump.click();
        } else {
            int dirX = signInt(dx);
            int dirZ = signInt(dz);
            int bx = math.floor(player.getX()) + dirX;
            int bz = math.floor(player.getZ()) + dirZ;
            // Marche de 1 bloc : solide au pied, dégagé à la tête
            if (game.blocks.getId(bx, py2, bz) != "minecraft:air" &&
                game.blocks.getId(bx, py2 + 1, bz) == "minecraft:air") {
                keys.jump.click();
            }
        }
    }

    return false;
}

// ─── Dépôt ────────────────────────────────────

async void deposit() {
    ui.systemMessage("§e[EggHunter] Calcul chemin → PNJ...");

    pathfinding.reset();
    pathfinding.startFindPath(DEPOSIT_X, DEPOSIT_Y, DEPOSIT_Z);

    while (pathfinding.isComputing()) {
        await delay.ticks(1);
    }
    if (!pathfinding.hasPath()) {
        ui.systemMessage("§c[EggHunter] Chemin PNJ introuvable! Vérifie coords.");
        return;
    }

    ui.systemMessage("§e[EggHunter] → PNJ (" + pathfinding.getLength() + " waypoints)");
    boolean arrived = false;
    stuckTimer  = 0;
    stuckLastX  = math.floor(player.getX());
    stuckLastZ  = math.floor(player.getZ());
    while (active && !arrived) {
        arrived = followPath(ARRIVE_DIST);

        stuckTimer = stuckTimer + 1;
        if (stuckTimer >= 20) {
            stuckTimer = 0;
            int cx = math.floor(player.getX());
            int cz = math.floor(player.getZ());
            int mvX = cx - stuckLastX;
            int mvZ = cz - stuckLastZ;
            if (mvX < 0) mvX = -mvX;
            if (mvZ < 0) mvZ = -mvZ;
            if (!arrived && mvX + mvZ < 2) {
                ui.systemMessage("§e[EggHunter] Bloqué (dépôt), déblocage...");
                keys.up.setDown(false);
                keys.left.setDown(false);
                keys.right.setDown(false);
                keys.jump.click();
                keys.down.setDown(true);
                await delay.ticks(6);
                keys.jump.click();
                await delay.ticks(3);
                keys.down.setDown(false);
                pathfinding.reset();
                pathfinding.startFindPath(DEPOSIT_X, DEPOSIT_Y, DEPOSIT_Z);
                while (pathfinding.isComputing()) {
                    await delay.ticks(1);
                }
            }
            stuckLastX = math.floor(player.getX());
            stuckLastZ = math.floor(player.getZ());
        }

        await delay.ticks(1);
    }

    keys.up.setDown(false);
    keys.sprint.setDown(false);
    keys.left.setDown(false);
    keys.right.setDown(false);
    // Délai humanisé avant interaction
    await humanDelay(4);

    player.lookAt(DEPOSIT_X + 0.5, DEPOSIT_Y + 0.5, DEPOSIT_Z + 0.5);
    // Petite hésitation humaine avant de cliquer
    await humanDelay(3);
    keys.use.click();
    // Temps d'ouverture GUI variable
    await humanDelay(9);

    if (containers.getSlotsSize() > 0) {
        containers.click(0, 0, "PICKUP");
        await humanDelay(5);
        containers.close();
        ui.systemMessage("§a[EggHunter] Oeufs déposés!");
    } else {
        ui.systemMessage("§c[EggHunter] GUI pas ouvert.");
    }
}

// ─── Main ─────────────────────────────────────

async void main() {
    ui.systemMessage("§a[EggHunter] Démarré — A* pathfinding + humanisation avancée");

    while (active) {
        int count    = getEggCount();
        int maxCount = getMaxEggs();

        if (count >= maxCount) {
            await deposit();
            // Délai humanisé après dépôt (variable, pas exactement 20 ticks)
            await humanDelay(20);
            continue;
        }

        int eggId = findBestEgg();
        if (eggId == -1) {
            ui.systemMessage("§7[EggHunter] Aucun oeuf visible...");
            // Délai d'attente variable : 35-45 ticks au lieu d'exactement 40
            await humanDelay(40);
            continue;
        }

        int r = getEggRarity(eggId);
        ui.systemMessage("§a[EggHunter] → " + rarityName(r) + " §7(" + count + "/" + maxCount + ")");

        int ex = math.floor(game.entities.getX(eggId));
        int ey = math.floor(game.entities.getY(eggId));
        int ez = math.floor(game.entities.getZ(eggId));
        pathfinding.startFindPath(ex, ey, ez);

        boolean arrived = false;
        stuckTimer  = 0;
        stuckLastX  = math.floor(player.getX());
        stuckLastZ  = math.floor(player.getZ());

        while (active && !arrived) {
            if (!game.entities.isAlive(eggId)) {
                break;
            }

            ex = math.floor(game.entities.getX(eggId));
            ey = math.floor(game.entities.getY(eggId));
            ez = math.floor(game.entities.getZ(eggId));

            int pdx  = ex - math.floor(player.getX());
            int pdz  = ez - math.floor(player.getZ());
            int dist = math.floor(math.sqrt(pdx * pdx + pdz * pdz));

            if (dist <= EGG_DIST) {
                keys.up.setDown(false);
                keys.sprint.setDown(false);
                keys.left.setDown(false);
                keys.right.setDown(false);
                arrived = true;
            } else {
                if (!pathfinding.isComputing()) {
                    pathfinding.startFindPath(ex, ey, ez);
                }
                followPath(ARRIVE_DIST);

                // ── Stuck detection ──────────────────────────────────────────
                stuckTimer = stuckTimer + 1;
                if (stuckTimer >= 20) {
                    stuckTimer = 0;
                    int cx = math.floor(player.getX());
                    int cz = math.floor(player.getZ());
                    int mvX = cx - stuckLastX;
                    int mvZ = cz - stuckLastZ;
                    if (mvX < 0) mvX = -mvX;
                    if (mvZ < 0) mvZ = -mvZ;

                    if (mvX + mvZ < 2) {
                        // Bloqué — reculer + sauter + recompute
                        ui.systemMessage("§e[EggHunter] Bloqué, déblocage...");
                        keys.up.setDown(false);
                        keys.sprint.setDown(false);
                        keys.left.setDown(false);
                        keys.right.setDown(false);
                        keys.jump.click();
                        keys.down.setDown(true);
                        await delay.ticks(6);
                        keys.jump.click();
                        await delay.ticks(3);
                        keys.down.setDown(false);
                        pathfinding.reset();
                        pathfinding.startFindPath(ex, ey, ez);
                    }
                    stuckLastX = math.floor(player.getX());
                    stuckLastZ = math.floor(player.getZ());
                }
            }

            await delay.ticks(1);
        }

        if (arrived) {
            // Délai avant interaction : variable, comme un humain qui "cherche le bon moment"
            await humanDelay(2);
            player.interactions.interactWithEntity(eggId);
            // Délai post-interaction variable
            await humanDelay(8);
        }
    }

    keys.up.setDown(false);
    keys.sprint.setDown(false);
    keys.left.setDown(false);
    keys.right.setDown(false);
    ui.systemMessage("§7[EggHunter] Arrêté.");
}

// Toggle
active = !active;
if (active) {
    main();
} else {
    keys.up.setDown(false);
    keys.sprint.setDown(false);
    keys.left.setDown(false);
    keys.right.setDown(false);
    ui.systemMessage("§7[EggHunter] Arrêté.");
}
