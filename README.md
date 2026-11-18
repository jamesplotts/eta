# ETA

Source: https://github.com/jamesplotts/eta

A Railroader mod for the 0.6 "Maintenance of Way" built-in mod loader. When the selected engine is running in **AE Waypoint** mode, the locomotive window (lower left) gets a line under the cars, tons and length:

```
ETA: 14:32, 2.3 mi to waypoint
```

- **ETA** is game time, from the distance left and the train's speed averaged over about 20 seconds, so it settles as the train gets up to speed. It takes the game's clock rate into account. While the train is stopped or crawling under 1 mph it shows `--:--`.
- **Distance** is the Auto Engineer's own figure for the route left to the waypoint, the same one the fast-forward screen shows.
- The line is blank for Manual, AE Road and AE Yard, or when there's no waypoint route.
- In multiplayer the Auto Engineer runs on the host, so only the host sees the line.

## Install

1. In Railroader, turn on **Preferences > Features > Experimental > Allow Unsafe DLL Mods**, then restart the game.
2. Unzip `ETA_<version>.zip` into `Documents\Railroader\Mods\`. You should end up with `Mods\ETA\` holding `Info.json` and `ETA.dll`.
   - On Linux with Proton, that's the same folder inside the game's prefix: `steamapps/compatdata/1683150/pfx/drive_c/users/steamuser/Documents/Railroader/Mods/`.
3. On the main menu, open **Mods** and enable ETA.

## Build

You need the .NET SDK (6 or later) and the game installed. The project compiles against the game's own DLLs, and none of them are committed.

```sh
cp Paths.user.example Paths.user   # then set GameDir (and ModDevDir) for your machine
dotnet build ETA/ETA.csproj              # Debug: copies the mod to ModDevDir
dotnet build ETA/ETA.csproj -c Release   # writes bin/ETA_<version>.zip
```

Debug builds go to `Documents\Railroader\ModDev\ETA`, which the game loads as a development pack. The game only loads a rebuilt DLL after a restart. The version number is set in `Directory.Build.props`.

## How it works

- The new line is a copy of the locomotive window's "cars, tons, ft" label (`infoALabel` on `UI.EngineControls.LocomotiveControlsUIAdapter`), placed one line below it, so it has the same font and size.
- Once a second it reads `AutoEngineerPlanner.RemainingWaypointDistance` for the selected engine and works out the arrival time on the game clock.
- No Harmony: the mod doesn't patch any game code.

## License

MIT.
