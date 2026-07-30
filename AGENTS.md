Include ..\AGENTS.md

# Visible Layers List — Mod-Specific Agent Instructions

## Identity
- **Assembly:** `visiblelayerslist`
- **Namespace:** `Calloatti.VisibleLayersList`
- **Framework:** Harmony, Bindito DI (Game + MapEditor contexts)
- **ModId:** `Calloatti.VisibleLayersList`
- **Min Game Version:** 1.0.12.5 — uses `timberborn-decompiled-1.0.*`

## What This Mod Does
Replaces the vanilla level visibility panel buttons with a scrollable dropdown list of all layers. Adds reset-to-max and quick-toggle-previous-layer functionality. Works in both Game and MapEditor contexts.

## Source Architecture (`Version-1.0/Source/`)

| File | Role |
|---|---|
| `VisibleLayersList.cs` | `IModStarter` entry point, `LayerDropdownConfigurator`, `LayerDropdownManager`, `LevelVisibilityPanel_Load_Patch` |
