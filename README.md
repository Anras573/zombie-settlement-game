# zombie-settlement-game
A web game where you build a settlement during the zombie apocalypse. Build farms, construct houses, and keep you fences repaired to ward off the approaching horde.

## Engine

The game is built on [Yaeger](https://github.com/Anras573/Yaeger), a modular, experimental 2D/3D
game engine written in C#, vendored as a git submodule under `engine/Yaeger`. The game targets
Yaeger's browser/WebAssembly runtime (`Yaeger.Browser`) so it can run entirely client-side.

## Getting started

### Prerequisites

- .NET 10.0 SDK or later

### Clone

Yaeger is pulled in as a git submodule, so clone with `--recurse-submodules`:

```bash
git clone --recurse-submodules https://github.com/Anras573/zombie-settlement-game.git
```

If you already cloned without that flag:

```bash
git submodule update --init --recursive
```

### Build

```bash
dotnet build ZombieSettlementGame.slnx
```

### Run the browser example

`src/ZombieSettlementGame.Browser` is a minimal Blazor WebAssembly host that boots the Yaeger
engine and renders the settlement's ground plot: a 10x10 tilemap (grass interior, brick boundary
wall) with three placed buildings — a farm, a house, and a fence, each a distinct entity with its
own `BuildingKind` and sprite-sheet frame — viewed through a `Camera2D` whose zoom is recomputed
every frame to fit the whole grid inside the viewport regardless of aspect ratio (letterboxed on
wide windows, pillarboxed on tall/portrait phone screens). Tap or click a building button in the
on-screen picker (or press **1**/**2**/**3** on a keyboard) to choose a building type, then tap
or click an empty grass cell to place it there; both point and click are mapped to a grid cell by
inverting the camera's view-projection, so placement stays correct at any window size. Touch input
is handled by Yaeger's browser input layer, which maps pointer events (mouse, touch, and pen)
into the same mouse-style state the placement logic reads.

Each house shelters up to two residents, who move in over time while there's room and food and
eat one food each every 10 seconds; farms (one food per 5 seconds) keep them fed. If the stockpile
runs dry a resident leaves per missed meal, and a destroyed house takes its residents with it. The
HUD shows food with its net rate, plus residents out of house capacity.

The game is lost once the zombies destroy every house, or once the settlement has had residents
and they've all left because the food ran out; a game-over screen shows how long you survived, and
**Play again** (or **Space**) starts a fresh settlement.

Every placed building carries hit points. On a timer, a zombie spawns on the grid's boundary
ring and walks in a straight line toward whichever building is nearest, then attacks it until it's
destroyed — freeing its cell for the player to rebuild. A building under attack tints redder the
more damaged it is, giving an at-a-glance warning before it's lost. Tap or click the **Repair**
button (or press **R**) to switch the next tap/click into a repair action instead of a placement:
tapping a damaged building spends a little wood to patch it back up, buying time before the horde
finishes it off. To actually thin the horde, build a **Watchtower**: zombies have hit points, and
each watchtower (key **5**) automatically shoots the nearest zombie within 3 cells once a second,
flashing a shot line and tinting the zombie red as it weakens and destroying it at zero. Repairing isn't free, though, so a ring of fences around the settlement's
perimeter (the sturdiest building) is still the front line: since zombies always go for the nearest
target, fences take the brunt of the horde before farms, houses, or sawmills ever do.

```bash
dotnet run --project src/ZombieSettlementGame.Browser/ZombieSettlementGame.Browser.csproj
```

Then open the printed `http://localhost:...` URL in a browser with WebGL 2.0 support.

## Assets

Art comes from [Kenney](https://kenney.nl) (CC0 — free for any use, credit appreciated but not
required). Packs are vendored under `src/ZombieSettlementGame.Browser/wwwroot/assets/kenney/<pack>/`,
each with its own `LICENSE.txt`. Currently in use: the
[Roguelike/RPG pack](https://kenney.nl/assets/roguelike-rpg-pack), a single 16x16-tile sheet
(57 columns × 31 rows, 1px margin) referenced via `Yaeger.Graphics.SpriteSheet` and `GetFrameUv`,
plus the [Roguelike Characters pack](https://kenney.nl/assets/roguelike-characters) (54 columns ×
12 rows, 1px margin) for the zombies.

## Deployment

Pushes to `main` publish `ZombieSettlementGame.Browser` and deploy it to GitHub Pages via
[`.github/workflows/deploy-pages.yml`](.github/workflows/deploy-pages.yml) (also runnable manually
from the Actions tab). This is a one-time repository setting, not something the workflow can turn
on itself: under **Settings → Pages**, set **Source** to **GitHub Actions**.
