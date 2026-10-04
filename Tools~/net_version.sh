#!/usr/bin/env bash
# Online compatibility version of this checkout. The phone and the game server can play together only
# when theirs are equal, so it changes only with what both must agree on: the network code, the map and
# the world built from it (doors, loot spots), and the ids/enums sent over the wire. Other changes
# (menus, offline features, balance) keep it, so installed APKs keep working with a newer server.
set -euo pipefail
cd "$(dirname "$0")/.."
git ls-files -z -- \
  Assets/Scripts/Game/Net \
  Assets/Resources/Map \
  Assets/Scripts/Game/World.cs \
  Assets/Scripts/Game/CityBuilder.cs \
  Assets/Scripts/Game/MapData.cs \
  Assets/Scripts/Game/MapCatalog.cs \
  Assets/Scripts/Game/Door.cs \
  Assets/Scripts/Game/LootSystem.cs \
  Assets/Scripts/Game/WeaponData.cs \
  Assets/Scripts/Game/ModelLibrary.cs \
  Assets/Scripts/Game/SafeZoneController.cs \
  Assets/Scripts/Game/AirPlane.cs \
  Assets/Scripts/Game/Grenade.cs \
  | sort -z | xargs -0 sha1sum | sha1sum | cut -c1-10
