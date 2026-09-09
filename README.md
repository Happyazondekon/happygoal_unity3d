# happygoal_unity3d

Unity 3D prototype for [HappyGoal](https://github.com/Happyazondekon/happygoal)'s penalty-shootout screen — kicker/goalkeeper animation, ball/net/goal physics, weather, and cosmetic skins (ball/boots/pitch) that mirror the Flutter app's shop.

Lives at `unity/happygoal_unity3d` inside the Flutter project so [flutter_unity_widget_2](https://github.com/juicycleff/flutter-unity-view-widget) can export straight into `android/unityLibrary` and `ios/UnityLibrary`.

## Editor menu (`Assets/Editor/PrototypeSceneBuilder.cs`)

- **Happygoal > 1) Configure Mixamo Imports** — run once after adding/updating character FBX files.
- **Happygoal > 2) Build Penalty Prototype Scene** — rebuilds the whole scene procedurally. Re-run after any script/asset change.
- **Happygoal > 3) Inspect Freekick Assets** — logs the recovered Freekick model hierarchy.
- **Happygoal > 4) Inspect Character Renderers** — logs kicker/keeper material names (for wiring cosmetic skins).

## In-game test keys

- `G` — swap kicker/goalkeeper control mode
- `1` / `2` / `3` — weather: sunny / rain / snow
- `7` / `8` / `9` — pitch skin: municipal / night / snow
- `F1`-`F4` — ball skin: classic / flame / gold / galaxy
- `F5`-`F7` — boots skin: turf / lightning / neon
