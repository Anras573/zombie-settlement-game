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
engine and renders a single box bouncing around the canvas — a smoke test proving the engine,
the browser render pipeline, and the game loop are wired up end-to-end.

```bash
dotnet run --project src/ZombieSettlementGame.Browser/ZombieSettlementGame.Browser.csproj
```

Then open the printed `http://localhost:...` URL in a browser with WebGL 2.0 support. It now
renders a textured tile (not just a flat-color box) bouncing around the canvas, proving the
texture-loading path works end-to-end in the browser.

## Assets

Art comes from [Kenney](https://kenney.nl) (CC0 — free for any use, credit appreciated but not
required). Packs are vendored under `src/ZombieSettlementGame.Browser/wwwroot/assets/kenney/<pack>/`,
each with its own `LICENSE.txt`. Currently in use: the
[Roguelike/RPG pack](https://kenney.nl/assets/roguelike-rpg-pack), a single 16x16-tile sheet
(57 columns × 31 rows, 1px margin) referenced via `Yaeger.Graphics.SpriteSheet` and `GetFrameUv`.

## Deployment

Pushes to `main` publish `ZombieSettlementGame.Browser` and deploy it to GitHub Pages via
[`.github/workflows/deploy-pages.yml`](.github/workflows/deploy-pages.yml) (also runnable manually
from the Actions tab). This is a one-time repository setting, not something the workflow can turn
on itself: under **Settings → Pages**, set **Source** to **GitHub Actions**.
