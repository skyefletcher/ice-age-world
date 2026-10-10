# ice-age-world

An ice-age animal survival game, inspired by Savannah Life, built with Godot 4 (.NET) and C#.

## Requirements

- [Godot 4.7 .NET edition](https://godotengine.org/download): portable, so just unzip it anywhere (e.g. `~/code/tools/`)
- .NET SDK 10 (`winget install Microsoft.DotNet.SDK.10`)

## Running

Open the editor, import this folder (`project.godot`), and press **F5**, or from the command line:

```
dotnet build
godot --path .
```

## Controls

| Key | Action |
|---|---|
| WASD / arrows | Move |
| Shift | Run (eagle: dive) |
| Space | Jump; eagle: take off, hold to flap and climb; snow leopard: leap out of a tree |
| Walk into a tree | Snow leopard: climb it. W / S climb up or down (all the way to the top), A / D go round the trunk, E steps out onto a branch beside you, then W / S walk along it |
| E | Eat grass or drink water; snow leopard, wolf, eagle or polar bear: eat from a kill, on the ground or up a tree (when the prompt shows) |
| F / left click | Snow leopard, wolf, eagle or polar bear: pounce on a wild animal close in front and bite it; beside a kill, pick it up to carry (the leopard even up a tree); press again to put it down. Eagle in flight: skim low over a hare (or an otter) to snatch it in your talons and carry it off, then land to eat it (when the prompt shows) |
| G / right click | Snow leopard: leap onto a wild animal close in front and clamp your jaws on. You hold on and ride along, doing far more damage than a bite, while it can only stagger, until it falls, you run out of stamina, or you press G or Space to let go |
| C | Sit down; press again (or move) to get up |
| X | Lie down; press again (or move) to get up. Resting gets your breath back faster |
| Tab | Switch animal (woolly mammoth, snow leopard, arctic wolf, sea otter, bald eagle, reindeer, moose or polar bear) |
| M | Show or hide the map of the world, with where you are and every other animal of your kind: wild ones in gold, your own pack, herd or family in green |
| Mouse | Look around |
| Mouse wheel | Zoom camera |
| Esc / left click | Release / recapture mouse |
| F11 | Toggle fullscreen |

## Layout

- `scenes/Main.tscn` — the world: sky, sun, terrain, wildlife, player and HUD
- `scenes/Player.tscn` — the player animal and its camera
- `scripts/Terrain.cs` — generates the landscape (with great snowy mountains, caves, giant old-growth trees, up to about 40 m tall), lake basins (and one big lake in the north-east corner), collision and mixed forest patches from noise, and remembers where each tree trunk, branch and treetop is for climbers
- `scripts/Terrain.Mountains.cs` — places the great mountains that rise out of the steppe, gentle in the foothills and craggy, ridged and snow-capped higher up
- `scripts/Terrain.Caves.cs` — digs a cave into the foot of each mountain: a winding tunnel under a rock roof you can walk on, opening into a big domed chamber at the back, dim inside, and marked on the map
- `scripts/TreeBuilder.cs` — builds randomised meshes for five kinds of tree (spruce, Scots pine, golden larch, white birch and narrow fir), each with lots of branches held out level, so no two trees look alike, remembering which branches are sturdy enough to walk along
- `scripts/Water.cs` — lake water surfaces, and where the water is for drinking and swimming
- `scripts/Animal.cs` — base class for playable animals: their 1–10 scores (jump height and length, land and water speed, agility, stamina) and the speeds, jumps, turning and stamina worked out from them, their abilities (climbing, flying, pack, herd), size, diet and camera, how far they are sitting, lying or gone limp in death, and shared mesh-building and posing helpers
- `scripts/Mammoth.cs` — builds the woolly mammoth in code (body, trunk, tusks, tail, hair) and animates it, sitting back on its rump or lying on its side as elephants sleep
- `scripts/SnowLeopard.cs` — builds the snow leopard in code to real proportions (fur patterned with rosettes and spots, ringed tail, cat face) and animates its walk, gallop, drinking crouch, sitting and sphinx-like lying, and forepaws-first landing when it leaps out of a tree
- `scripts/ArcticWolf.cs` — builds the arctic wolf in code (white winter coat, neck ruff, bushy tail, a big, cute head with large amber eyes) and animates its walk, gallop, drinking, sitting and lying
- `scripts/SeaOtter.cs` — builds the sea otter in code (dense brown fur, grizzled head, whiskers, webbed hind flippers, flat tail) and animates its bounding walk, belly-down swimming, floating on its back, sitting up on its haunches and lying on its back on land
- `scripts/ArcticHare.cs` — builds the arctic hare in code (white winter coat, black-tipped ears, side-set eyes, long snowshoe hind feet) and animates its bounding run, nibbling, sitting up on its haunches and lying flat; it is wild prey only, not a playable animal
- `scripts/Quadruped.cs` — shared skeleton and movement for the big four-legged animals: legs that walk, gallop and fold, drinking and grazing, sitting up (bears) or bedding down with the legs folded under (deer), lying and going limp, and cloven hooves
- `scripts/Reindeer.cs` — builds the reindeer (grey-brown winter coat, cream throat mane, white rump, dark legs with pale socks, a dark face and pale furry muzzle, and caribou antlers: long C-shaped beams with forward bez tines, a back tine, palmate tops and a toothed shovel over the face); it travels with a herd
- `scripts/Moose.cs` — builds the moose (dark coat, high humped shoulders, long pale legs, drooping overhanging nose, the bell under its throat, broad palmate antlers)
- `scripts/PolarBear.cs` — builds the polar bear, an ice-age giant three times the size of a modern one (dense creamy coat, long neck, long Roman-nosed head with a black nose and lips, little round ears, huge furry paws with black pads and claws); a hunter whose bite can bring down a mammoth, and that wolves fear
- `scripts/BaldEagle.cs` — builds the bald eagle in code (white head and tail, hooked yellow beak, talons, folding feathered wings) and animates its waddle, wingbeats, glide, dive, settling down on its legs and resting on its breast
- `scripts/ProceduralTextures.cs` — generates the fur, rosette-spotted coat and ivory textures with normal maps at startup
- `scripts/MeshBuilder.cs` — small helper for building meshes in code
- `shaders/fur.gdshader` — sways the hair strands
- `icons/` — HUD icons (heart for health, leaf for hunger, water drop for thirst, lightning bolt for stamina)
- `scripts/Grassland.cs` — the edible meadow covering the green steppe; eaten grass flattens and regrows
- `scripts/Player.cs` — movement, jumping, swimming, eating, drinking, sitting and lying down, hunger, thirst and stamina, switching animal, and the camera
- `scripts/Player.Flying.cs` — flight for animals that fly: take off, flap to climb, glide, dive, bank and land
- `scripts/Player.Climbing.cs` — tree climbing for animals that climb: up, down and round the trunk, out along branches, up onto the treetop, and leaping off
- `scripts/Player.Falling.cs` — falling and landing: heavy gravity that pulls harder on the way down, a cat twisting upright in mid-air, reaching its forepaws down and spreading its legs wide in a long fall, a crouch that soaks up a hard landing, and fall damage from drops higher than the animal can take
- `scripts/Player.Family.cs` — starting a family: stay beside the wild snow leopard for ten seconds as a grown-up snow leopard and the two pair up and have a cub; the mate and cub then travel with the player
- `scripts/Player.Hunting.cs` — hunting for animals that hunt (snow leopard, arctic wolf and bald eagle): pouncing on wild animals, eating from kills, carrying a kill in the jaws, up a tree if need be, to put it down and eat it there, and for the eagle, snatching prey on the wing and carrying it off in its talons
- `scripts/Player.Gripping.cs` — the snow leopard's special attack: leaping onto prey and holding on with its jaws, riding along at its side and wearing it down while it drags the cat, until it falls or the cat tires
- `scripts/Player.Growing.cs` — growing up: the player is born a calf, cub, pup or eaglet under half its grown size, and grows into an adult over ten minutes of being fed and watered, slower, weaker and lower to the ground until it does
- `scripts/Player.Dying.cs` — being hunted and dying: wild wolves bite a player that is a mammoth, otter or eagle on the ground, its health heals once it gets away, and if it dies it lies where it fell until the player chooses an animal to be reborn as, a newborn at the start
- `scripts/Herd.cs` — the computer-controlled wolf pack (bunched close round the player) and mammoth herd that follow the player, and sit or lie down when it does; when the player attacks a wild animal as a wolf, the pack runs in, surrounds it and helps bring it down, and when the player eats from a kill, the pack eats with it; and a wild wolf the player kills as a wolf is reborn twice, as a pup in the player's pack that grows up there and back in its own pack; and the snow leopard's family (its mate and cubs), which hunts and eats with it the same way, leaping on and clamping their jaws on the prey with the special attack (letting go to get their breath back now and then), and has a new cub each time the last one grows up, up to eight. Any pack or family turns on whatever bites the player, wolves or polar bear
- `scripts/Wildlife.cs` — wild mammoth and reindeer herds, moose cows with calves, wolf packs, sea otter rafts, groups of arctic hares, a lone snow leopard and a giant polar bear by the big lake (which every pack flees, and which hunts anything it can catch, the player included, eats and sleeps it off) that live in the world on their own: the mammoths graze and stampede from hunters, the wolves hunt them (and the player, unless it is a wolf or snow leopard) down, feed and sleep it off, and run from any snow leopard unless the pack is seven strong or more, when it hunts the snow leopard instead, the snow leopard prowls and lies up, and climbs the nearest tree to escape a big pack, the otters float about the lakes and haul out on the shore to rest, every animal running and tiring by its own scores, and anything that falls (to the wolves or to a snow leopard player) goes limp and is reborn as a youngster
- `scripts/Hud.cs` — current animal name (and how grown it is), health, hunger, thirst and stamina bars, the on-screen action prompt, a warning while wolves are hunting the player, and the rebirth screen
- `scripts/WorldMap.cs` — the map (M): the whole world painted from the terrain, with an arrow for the player and dots for every other animal of its kind, wild or travelling with it
- `scripts/InputSetup.cs` — key bindings
- `scripts/Main.cs` — scene startup and mouse/window handling
