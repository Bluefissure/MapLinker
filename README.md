# MapLinker

MapLinker is a dalamud plugin that can record the maplink information existing in the in-game chat, and then query or teleport to the nearest crystal.

![quicker_229761ec-bac1-4c1b-b25b-385af231d764.png](https://i.loli.net/2021/01/18/Ud1azLnGtIqWxf8.png)

## Features

The `/maplink` command calls out the config panel. After that, most of the instructions are in the popup instructions of that panel. It should be emphasized that the retrieval and teleport depend on other plugins:
- View (retrieve): Requires `ChatCoordinates` plugin, the function is to mark this maplink in the map
- Teleport: Requires `Teleporter` plugin, the function is to teleport to the nearest crystal

## Installation

You can download and install it in Dalamud Plugin Manager (`\xlplugins`)

Or download and install manually via https://github.com/Bluefissure/MapLinker/releases/
