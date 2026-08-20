# CheatUtils — Modules & Scripts Evolucraft

Extensions pour [CheatUtils](https://github.com/zergatul/cheatutils) (Minecraft 1.21.7 / Forge) + scripts d'automatisation pour le serveur **Evolucraft**.

---

## Modules Java ajoutés

### `PathfindingApi`
A\* pathfinding non-bloquant exposé au scripting.

| Méthode | Description |
|---------|-------------|
| `startFindPath(x, y, z)` | Lance le calcul A\* en background (CompletableFuture) |
| `hasPath()` | Retourne true si un chemin est disponible |
| `isDone()` | Retourne true si le dernier waypoint est atteint |
| `getNextWaypoint()` | Position3d du prochain waypoint |
| `advanceWaypoint()` | Passe au waypoint suivant |
| `reset()` | Vide le chemin actuel |

**Paramètres internes :**
- MAX\_NODES = 30 000
- Poids heuristique = 1.2 (weighted A\*)
- Pénalité mur = +4 par face adjacente
- Bounding box = `heuristique × 4 + 80`

---

### `ScriptEntityEspApi`
ESP X-ray par entité — couleurs custom, passe à travers les blocs.

| Méthode | Description |
|---------|-------------|
| `set(entityId, "#RRGGBB")` | Affiche une box colorée sur l'entité |
| `remove(entityId)` | Supprime la box |
| `clear()` | Supprime toutes les boxes |
| `size()` | Nombre d'entités suivies |

Utilise `LineRenderer.begin(event, false)` (depth test désactivé = X-ray).
Taille minimum 0.4 bloc pour les petites entités (`minecraft:interaction`).

---

### `ScoreboardApi`
Lecture du scoreboard sidebar.

| Méthode | Description |
|---------|-------------|
| `getSidebarTitle()` | Titre de l'objectif sidebar |
| `getSidebarLines()` | Lignes triées par score décroissant (`owner : value`) |
| `findLine(keyword)` | Première ligne contenant le mot-clé (insensible à la casse) |

> ⚠️ Sur Evolucraft, les vraies données (or, vagues, kills) sont dans les prefixes/suffixes des teams. Un `getSidebarLinesDisplayed()` est nécessaire pour les lire — voir issue ouverte.

---

### `Root.java` — Ajouts

```java
public static PathfindingApi   pathfinding    = new PathfindingApi();
public static ScriptEntityEspApi scriptEntityEsp = new ScriptEntityEspApi();
public static ScoreboardApi    scoreboard     = new ScoreboardApi();
```

---

## Scripts


### `egg_hunter.cs` — Bot chasseur d'oeufs
Automatise la collecte d'oeufs dans le labyrinthe Evolucraft.

**Boucle :**
1. Scanne les `minecraft:interaction` entities dans le champ de vision
2. Priorise par rareté (mythic > légendaire > épique > rare > commun)
3. A\* pathfinding vers l'oeuf cible
4. Collecte (right-click)
5. Quand inventaire plein (30/30) → pathfinding vers le PNJ de dépôt [20, -29, 3]
6. Dépôt + reset

**Humanisation anti-détection :**
- Rotation easing proportionnelle (vitesse = delta/3)
- Overshoot aléatoire sur longues rotations
- Bruit gaussien sur la caméra (somme de 3 randoms)
- Sprint 85% du temps, micro-pauses aléatoires
- Strafes latéraux (désactivés en couloir étroit)
- Variation de timing par tick (±2 ticks)
- Récupération stuck : recul + saut + recalcul

**Config (variables statiques en haut du script) :**
```
DEPOSIT_X/Y/Z = 20, -29, 3       // PNJ dépôt
ARRIVE_DIST   = 1                 // distance d'arrivée waypoint
EGG_DIST      = 3                 // distance collecte oeuf
CAM_SPEED_MAX = 18                // vitesse rotation max
CAM_NOISE_AMP = 2                 // amplitude bruit caméra
SPRINT_DIST   = 6                 // distance avant sprint
STRAFE_CHANCE = 8                 // 1/N chance de strafe par tick
PAUSE_CHANCE  = 60                // 1/N chance de micro-pause
```

---

### `donjon_auto.cs` — Bot donjon infini *(WIP)*
Automatise le farm du donjon infini Evolucraft.

**Boucle principale :**

```
[Spawn] → Pathfind NPC [59,161,-5]
       → Ouvre GUI → clique "Donjon Infini" (check tickets)
       → Attend TP en donjon (scoreboard apparaît)
       → Combat : cible minecraft:interaction le plus proche
              → Gestion level-up GUI (choisit la rareté max)
              → Tentative d'ouverture coffre [111,87,79] quand or suffisant
       → Détection mort (scoreboard disparaît) → restart
```

**États :**
| ID | État | Description |
|----|------|-------------|
| 0 | GOTO_NPC | Pathfind vers le PNJ d'entrée |
| 1 | OPEN_NPC | Interagit avec le PNJ |
| 2 | NPC_MENU | Clique "Donjon Infini" si tickets > 0 |
| 3 | ENTERING | Attend le TP en donjon |
| 4 | COMBAT | Cible + attaque + gère GUIs |
| 5 | LEVEL_UP | Choisit le meilleur upgrade |

**Priorité upgrades level-up :**
Mythique > Légendaire > Épique > Rare > Peu commun



---

## Installation

1. Cloner [CheatUtils modifié](https://github.com/SkySynn/EvolucraftBots)
2. Copier le `.jar` dans `mods/`
3. Importer les scripts `.cs` via l'interface web CheatUtils (keybinding scripts)

## Compatibilité

| Composant | Version |
|-----------|---------|
| Minecraft | 1.21.7 |
| Forge | compatible 1.21.7 |
| CheatUtils | 3.14.0+ |
| Java | 21 (JBR IntelliJ recommandé) |
| Serveur | Evolucraft (Paper + ModelEngine) |
