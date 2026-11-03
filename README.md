# The Last Hope Alt

A separate stat-and-flight variant of The Last Hope for tModLoader 2026.08 and
Calamity 2.2.2. The original project and installed original mod are preserved.

## Use

Disable **The Last Hope**, enable **The Last Hope Alt**, and reload mods. Both
have their own mod and item identity. An original relic does not turn into an
alternate relic; forge the alternate one using the same collection.

The crafting rules are unchanged: one of each of the same 644 eligible vanilla
and Calamity accessories, in unlocked closed chests within 10 tiles of a vanilla
workbench. Right-click the workbench or press P, then click Forge. The separate
Accessory Spawner helper still provides the full collection with
`/spawnaccessories`.

Equip the alternate relic in one normal accessory slot. It grants numerical
equipment modifiers and ordinary wing flight. No accessory transformations,
screen filters, monolith effects, music overrides, dashes, extra jumps, triggered
attacks, summoned companions, active abilities, immunities, or automatic buffs
are enabled. It never runs source accessories' equipment hooks on a live player.
The source Calamity compatibility hooks are disabled in this variant.

## Stats and tooltip

Numerical bonuses are measured per accessory in an isolated headless process,
then stored in `src/LastHopeAlt/Data/accessory-stats.json`. During gameplay only
allowlisted numeric fields are applied. Bonuses from component accessories and
upgrades add together. Wings contribute flight time; a vanilla Solar Wings item
supplies ordinary flight controls, without Calamity wing abilities.

These are fixed passive profiles measured at full health, stationary, without
buffs or boss progression. Separate day/night profiles switch automatically.
Conditional combat procs, buffs, biome/progression scaling, and situational
abilities are excluded; the alternate relic does not simulate their live state.
Percentage life bonuses scale with the character's base maximum life. Profiles
are specific to the installed versions, and must be regenerated after accessory
balance changes. A changed catalog with missing profiles fails with a clear error.

The tooltip reads exactly the same numerical profile that the accessory applies.
It shows damage per class, crit, attack speed, armor penetration, life, defense,
regeneration, summon slots, damage reduction and flight duration. Hold Shift for
all remaining scalar, ammunition and knockback modifiers. Generic bonuses also
contribute to damage classes according to Terraria's normal inheritance rules.
Class lines describe each class's own added modifier, rather than double-counting
the generic line. Crit values are percentage-point bonuses; multipliers and flat
bonuses are labelled separately. Mana cost cannot become negative, mining use
time has a 5% floor, and ordinary damage reduction has a 100% ceiling; tooltips
state those limits. Calamity's normal downstream combat calculations still apply.

The config contains only an ordinary-flight toggle and a list of individual
accessory stat contributions to disable. Neither changes the ingredient list.
Settings are synchronized for multiplayer using the existing packet format.

## Layout and development

- `src/LastHopeAlt/`: C# source, localization, numeric data, metadata and project.
- `src/tModLoader.targets`: reference to this machine's tModLoader installation.
- `artifacts/`: packaged `.tmod` and extracted DLL/debug symbols.
- `verification/`: integration report and original/alternate source comparison.
- `tools/`: isolated profile-generation and verification runner.

Build in Develop Mods with Build + Reload, or from this folder:

```bash
dotnet build src/LastHopeAlt/LastHopeAlt.csproj
```

Close tModLoader or disable this mod before terminal packaging. Develop Mods
handles unloading itself. For just C# compilation, append `-p:BuildMod=false`.
Artifact files are release snapshots. Rebuilding updates the installed Mods file.

The project was copied from `~/Projects/TheLastHope`. The original numerical
accumulator and compatibility source remain for reference but are unused during
alternate gameplay. Shared crafting/catalog/UI logic was retained; namespace
changes are required so tModLoader recognizes this as an independent mod.
