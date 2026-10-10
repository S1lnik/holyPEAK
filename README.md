# HOLY PEAK

*A pilgrimage up the silent mountain.*

They say a bell hangs at the top of the Holy Peak, silent for a thousand years, and that while it sleeps, so does the dawn.
You have walked a long way to wake it.

**Holy Peak** is a hand-drawn-in-code action platformer set on one interconnected mountain.
Explore five regions, fight what lives in them with your pilgrim's staff, trade embers with a wandering peddler, find the relics that open the way up, and face four guardians that will not let you pass easily.

![Title screen](docs/screenshots/title.png)

| | |
|---|---|
| ![Pilgrim's Rest](docs/screenshots/village.png) | ![Thornwood](docs/screenshots/thornwood.png) |
| ![The Sunken Abbey](docs/screenshots/abbey.png) | ![Frostwind Cliffs](docs/screenshots/frostwind.png) |
| ![The Holy Peak](docs/screenshots/summit.png) | ![The Frost Wyrm](docs/screenshots/guardian.png) |

## The mountain

The world is one map of 33 chambers. Every region opens with something you find in the one before it, and old places hide new secrets once you can reach them.

| Region | | What you find there |
|---|---|---|
| **Pilgrim's Rest** | the village at the foot | a shrine, Old Tam the peddler, Sister Ione, and a cleft too sheer to climb |
| **Thornwood** | where the forest closed its hand | creatures, bramble beds, the old smithy and **Thornback**, the bramble boar |
| **The Sunken Abbey** | its prayers long since fallen silent | sentinels, bell-glass, resonance stones and **the Hollow Abbot** |
| **Frostwind Cliffs** | the wind never rests | icefalls, a crevasse, rising winds and **the Frost Wyrm** |
| **The Holy Peak** | where the old bell waits | the stairs of light and **the Bellkeeper** |

### The pilgrim's ways

- **The staff.** Strike sideways, upward, or downward in the air. A downward strike bounces you off creatures, thorns and projectiles, so a bramble bed can become a staircase.
- **The Iron Grips.** Cling to a wall where you catch it, then leap away. Leap between two walls to climb.
- **The Bell of Resonance.** Ring it to shatter bell-glass, wake resonance stones into platforms for a few seconds, and stagger creatures. Rung in the air, it lifts you one more time.
- **The Pilgrim's Kite.** Open it in the air to glide, and ride updrafts high into the cliffs.

### Surviving

You have a row of vigil flames for health and balm flasks that heal you if you kneel and hold still.
Thorns cost a flame and put you back on safe ground. If your last flame goes out you wake at the last shrine you rested at, and the embers you carried stay behind as a lingering flame. Go back and touch it to take them back. Die before you get there, and they are gone.

Embers drop from creatures and cracked ember caches. Old Tam sells a map of the mountain, whetstones, balm flasks, a vessel of vigil and an ember charm.

## Controls

| Action | Keyboard (arrows) | Keyboard (WASD) | Gamepad |
|---|---|---|---|
| Move, look | Arrow keys | WASD | Left stick or D-pad |
| Jump, open the kite | Z | Space | A |
| Strike | X | J | X |
| Ring the bell | C | K | Y or RB |
| Heal (hold) | V | L | B or LB |
| Rest, talk, use | Up | W | Up |
| Map | Tab or M | Tab or M | Back |
| Pause | Esc or Enter | Esc or Enter | Start |
| Fullscreen | F11 | F11 | |

Hold Down and strike in the air to bounce. Hold Down and jump on a thin ledge to drop through it.
Hold Up or Down while standing still to look around. Button prompts follow the keys you actually use.

## Features

- One connected world with fast transitions between chambers, a map that fills in as you explore, five shrines, and shortcuts that loop back to them.
- Six kinds of creatures, each with a tell before it attacks, and four guardians with telegraphed patterns, a second phase when wounded, and arena doors that close behind you.
- A lighting pass: caves, crypts and the abbey are dark, and the lantern at the head of your staff, candles, lit windows and glowing creatures push the dark back.
- Everything is generated in code. There is no asset folder. The pixel font, every sprite, the terrain, the parallax mountains, the lighting, every sound effect and all eight pieces of music are made at startup.
- An original soundtrack that shares one theme across the village, each region, the guardians and the ending.
- Sister Ione, Elder Bram and Old Tam, lore tablets, an ending with statistics and how much of the mountain you found.
- Progress saves at shrines and when you collect something. Options cover volume, fullscreen, window size, screen shake, a speedrun timer, and assists for game speed and invincibility.
- The mountain is proven passable. A solver searches the real game physics for every route the design depends on, and a whole-game walk plays from the village to the great bell.

## Download

Ready-to-play builds for Windows, macOS (Apple Silicon and Intel) and Linux are on the [Releases page](../../releases).
Nothing needs installing: unzip and play. Each download includes a `README.txt` with first-launch notes for unsigned apps.

## Running from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```sh
dotnet run --project src/HolyPeak -c Release
```

On the first launch the soundtrack renders in the background, which takes a few seconds, and is then cached.
Saves and settings live in a `HolyPeak` folder inside your user application-data folder (for example `%APPDATA%\HolyPeak` on Windows or `~/.config/HolyPeak` on Linux).
Set `HOLYPEAK_DATA` to use a different folder. Saves from version 1 are not carried over.

### Building release packages

```sh
build/package.sh 2.0.0                      # all platforms, written to dist/
build/package.sh 2.0.0 win-x64 linux-x64    # or just some
```

This produces a Windows zip, a Linux tarball and macOS `.app` bundles. The `.app` bundles are ad-hoc signed when built on a Mac, which Apple Silicon requires.
The [Release workflow](.github/workflows/release.yml) runs the same script on GitHub Actions and publishes the result. Start it from the Actions tab, or push a `v*` tag.

MonoGame's DesktopGL runtime bundles SDL2 and OpenAL, so nothing else needs installing.
If the machine has no audio device, the game runs silently instead of failing.

## For developers

```
src/HolyPeak/
  World/            the simulation: the pilgrim, rooms, creatures, guardians, travel between rooms (no graphics)
  World/world.txt   the whole mountain as ASCII maps on a grid of cells
  Gfx/              procedural art: tiles, backdrops, sprites, particles, the pilgrim, the world renderer
  Audio/            synthesizer, sound effects, the score and its renderer, playback
  Scenes/           title, the game (with its map, shop, dialogue and pause), ending, menus
tools/worldgen/     the Python script that builds world.txt, and a previewer
tools/HolyPeak.Verify/   the route solver and whole-game walker
```

### The world format

`world.txt` is generated by `tools/worldgen/world.py`:

```sh
cd tools/worldgen
python3 world.py ../../src/HolyPeak/World/world.txt
python3 preview.py ../../src/HolyPeak/World/world.txt world.png    # a picture of the whole map, and an edge check
```

Each room sits on the world grid (`@room id at=x,y size=w,h region=...`), one cell is 40 by 23 tiles of 8 pixels, and leaving through an open edge enters whichever room owns the next cell.
Rooms can set `npc:`, `tablet:`, `hint1:` to `hint3:`, `ability:`, `boss:`, `gatekey:`, `dark:` and `sky:`.

```
#  terrain          =  secondary terrain      :  background wall      -  thin ledge
^ v < >  thorns     x  cracked stone          b  bell-glass           r  resonance stone
u  updraft          |  gate (lever)           !  arena door           @  where the journey starts
G  shrine           M  peddler                N  pilgrim to talk to   T  tablet
H  vessel of vigil  F  balm flask             $  ember cache          A  relic altar
L  lever            B  guardian               K  the great bell       1 2 3  hint text
c crawler  h hopper  w wisp  k sentinel  s spitter  d dropper
```

### Proving the mountain

```sh
dotnet run --project tools/HolyPeak.Verify -c Release                    # every route, secret and gate
dotnet run --project tools/HolyPeak.Verify -c Release -- --only abbey    # a subset
dotnet run --project tools/HolyPeak.Verify -c Release -- --walk walk.txt # the whole game as one replay
```

The verifier runs a best-first search over the real simulation with creatures left out, so a route it finds is an exact input sequence.
Some checks are gates: they must fail, which proves that a relic is genuinely needed and the regions can't be skipped.

### Automation

The game binary has flags for screenshots and end-to-end checks:

```sh
HolyPeak --scene game --room v-square --pos 40,20 --frames 120 --shot shot.png
HolyPeak --scene game --room v-east --replay walk.txt --peaceful --frames 11000   # plays the whole game
HolyPeak --scene game --room t-den --pos 30,20 --invincible --autofight         # a crude bot fights Thornback
HolyPeak --scene title --keys "80:Down,95:Confirm" --frames 200 --shot menu.png
HolyPeak --render-audio wav/                                                     # every sound and song as WAV
```

`--give grips,bell,kite` grants relics (or any flag, such as `boss:thornback`) for testing.

## Credits

Design, code, pixel art, music and sound by Claude. Built with [MonoGame](https://monogame.net).
