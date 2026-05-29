# Safehouse Reputation BETA 

Safehouse Reputation is a MelonLoader mod for The Long Dark that gives indoor shelters a sense of familiarity and progression.

The more time you spend inside a shelter, the more reputation that location gains. Each interior is tracked separately, so places like Camp Office, Trapper’s Homestead, or other indoor shelters can develop their own reputation level over time.

At higher levels, a shelter becomes more than just a place to rest. Once a safehouse reaches the required level, it unlocks the Shelter’s Embrace buff, which provides a small warmth bonus while you are inside that location.

## Features

* Per-shelter reputation tracking
* Safehouse level system
* Configurable hours required per level
* Configurable maximum safehouse level
* Shelter’s Embrace warmth buff
* Configurable warmth bonus
* ModSettings support
* Reputation data saved automatically
* Debug logging option for testing

## Shelter Levels

* Level 1: Familiar Shelter
* Level 2: Trusted Shelter
* Level 3: Safehouse
* Level 4: Home Base
* Level 5: Survivor Haven

## Shelter’s Embrace

Shelter’s Embrace is unlocked when a shelter reaches the configured required level. While inside that safehouse, the player receives a small warmth bonus.

By default:

* Required Level: 3
* Warmth Bonus Per Level: +0.5°C
* Maximum Warmth Bonus: +1.5°C

## Requirements

* MelonLoader
* ModSettings

## Installation

Place `SafehouseReputation.dll` into your `TheLongDark/Mods/` folder.

## Notes

* Reputation is tracked by interior scene name.
* Each shelter has its own separate reputation level.
* Shelter’s Embrace only works while inside a leveled safehouse.
* To reset all reputation progress, delete `UserData/SafehouseReputation.txt`.

Created by Bloodtroo.
