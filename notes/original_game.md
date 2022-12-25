# Base building

https://dalek.zone/w/vB2A6k1wmUaEF3mAi3MFgf

Buildings are not constructed and deployed directly (like in C&C or RA), but instead MCU's (Mobile Construction Unit) are built from Production/Construction Center and these are deployed.

MCU can be deployed only if all tiles required to construct the building (i.e. 3x3 or 1x1) are free. There are a few exceptions (see below).

After deployment is initiated:
- MCU rotates down
- MCU transforms into temporary flat construction "deck"
- this "deck" can be selected, but building cannot be controlled (i.e. no unit can be produced from it)
- a "construction pyramid" is raised to "hide" building construction
- after a period of time, pyramid descends to ground and is hidden
- building is ready

Since original animation (and its sprites) is a bit quirky, [here's slowed down version of the construction](https://dalek.zone/w/nzQMX2DDB4mn5CTapibtia) (for reference).

### Air base

Air base occupies one extra tile, which is not mandatory for deployment of MCU, but if it's not free when initiating deployment, this tile won't be occupied after construction and air base will be missing one heliport.

### Naval base

Naval base requires extra tile for dock built on water. Ground part has to be near shore otherwise it's not possible to deploy MCU. Dock position is determined automatically by the game and can't be changed (other than moving and deploying the MCU on different tile). Tiles on shore required to deploy MCU seem to be just 2x2 (see video above).

### Bunker

# Building destruction

https://dalek.zone/w/sshYfFD5CQUkzsTWaadRVL

When building health gets into red:
- crew starts to evacuate, one after another

When building health reaches zero:
- building becomes invulnerable, unselectable, health bar disappers, building disappears from building list in sidebar
- building destruction effects are played
- on click building name does flash in status bar when exploding
- after destruction animation completes, smudge and fire appears
- if there are people inside, when building starts to explode, they just die (i.e. they don't appear after building explodes)
- after some short time, fire dies down, smudges remain

When power plant explodes, crater appears in its place (2x2 tiles) and nothing can be built in that place.

Turrets don't have period of invulnerability and explode immediately:
https://dalek.zone/w/crE4H6s1fTn92hhRmVkYRx

## Building destruction and unit production

If building is producing unit and is destroyed, when elevator with unit is still below ground level, the unit disappears
and building starts to play explosion animation

https://dalek.zone/w/o7tasdzVsjjG9TUnPi5VbU
https://dalek.zone/w/qY2fDsg1NxRssfJ9LJXAKB



# Unit production

https://dalek.zone/w/pWk4kAinss2zGS817zKmUt


When unit production is completed:
- production exit door is opened
- unit starts to move up on elevator
- you can't select unit being produced and manually target as player
- when elevator gets to ground level, produced unit is selectable and targetable
- produced unit moves out of the elevator onto ground (or flies out)
