# Nightfall Arena

A small 2.5D survival game prototype inspired by auto-attacking wave survival games.

## Play

Open `index.html` through GitHub Pages.

## Builder

Run `NightfallArenaBuilder.exe` to edit the game.

- `Save` updates the local config files.
- `Open Game` opens the editable browser version.
- `Build Game EXE` creates/updates `dist/NightfallArenaGame/NightfallArenaGame.exe`.
- `Open Build` opens the folder that contains the playable game EXE.
- `Push Pages` uploads the current web version to GitHub Pages.

The built game EXE keeps its web files in `dist/NightfallArenaGame/www`. When you rebuild, the builder refreshes those files from the current project.

## Controls

- Move: WASD or arrow keys
- Attacks: automatic
- Upgrade: click one card when leveling up

## Files

- `index.html` - page and UI
- `style.css` - layout and HUD styling
- `game.js` - gameplay, rendering, upgrades, waves
- `game-config.json` - editable game values used by the builder
- `game-config.js` - browser-friendly copy of the editable config
- `NightfallArenaBuilder.exe` - Windows editor for changing the config and pushing Pages updates
