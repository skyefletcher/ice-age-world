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

## Playing with friends

The game opens on a start menu. Type your name, then:

- **Play alone**, or
- **Host a game for friends**: the screen shows your address for friends to join with. Friends on the same Wi-Fi use it as shown; friends elsewhere need your internet address, and port 7777 (UDP) forwarded to your computer.
- **Join game**: type the host's address and press the button.

Everyone shares the same world and sees each other's animals, with names and health bars over them. F bites another player's animal. Up to 16 can play in one game.

To start straight away from the command line: `godot --path . -- --host --name Skye` or `godot --path . -- --join 192.168.1.20 --name Sam`.

## Coins and the animal shop

As in Savannah Life, animals cost coins. The arctic wolf, reindeer and sea otter are free. The others have to be bought: moose 250, bald eagle or snowy owl 400, cave hyena 500, snow leopard 600, cave lion 900, woolly mammoth or woolly rhino 1000, smilodon 1200, polar bear 1500. You start with 250 coins.

Coins are earned by doing chores. Three are always listed in the top right corner, with how far along each is. When one is done its coins are paid and a new chore takes its place. Chores only ask what your animal can do: only hunters get hunting chores, only the birds get flying ones.

| Chore | Coins | Who |
|---|---|---|
| Travel 500 metres / 1 kilometre | 60 / 120 | Everyone |
| Swim 100 metres | 70 | All but the birds |
| Fly 1 kilometre | 100 | Eagle, owl |
| Eat 20 mouthfuls of grass | 60 | Grazers |
| Drink water for 20 seconds | 50 | Everyone |
| Eat 15 mouthfuls of meat | 80 | Hunters |
| Hunt an animal / 3 animals | 100 / 250 | Hunters (your pack's kills count) |
| Climb 3 trees | 90 | Snow leopard |
| Stay underwater for 30 seconds | 60 | Sea otter, polar bear |
| Dive for 3 shellfish and eat them | 90 | Sea otter, polar bear |
| Sit or lie down for 30 seconds | 30 | Everyone |

Coins and bought animals are saved on your computer. The shop comes up when you start and when you die, and B opens it at any time.

## Controls

| Key | Action |
|---|---|
| WASD / arrows | Move |
| Right mouse button | Hold to walk forward |
| Shift | Run (eagle or owl: dive). Sea otter or polar bear swimming in deep water: dive underwater, and hold to swim deeper |
| Space | Jump; sea otter or polar bear underwater: swim up; eagle or owl: take off (from the ground or a branch), hold to flap and climb; snow leopard: leap onto a tree to climb it, or out of one |
| Glide onto a branch | Eagle or owl: perch on it (not while flapping, diving or carrying live prey). W / S walk along it, C sits, X lies down, Space flies off |
| Walk uphill | Snow leopard: climbs any slope, right up the steepest mountainsides |
| Jump at a tree | Snow leopard: grab hold and climb it. W / S climb up or down (all the way to the top), A / D go round the trunk, E steps out onto a branch beside you, then W / S walk along it |
| E / left click | Start eating grass or drinking water; sea otter or polar bear underwater: pick up a sea urchin or clam from the bed, then at the surface eat it (the otter floating on its back), or snap up a fish; snow leopard, wolf, eagle, owl, polar bear, cave lion or smilodon: start eating from a kill, on the ground or up a tree (when the prompt shows). The animal keeps feeding until you press it again or the food runs out |
| F | Any animal, in a game with friends: spring at another player's animal close in front and bite it. Snow leopard, wolf, eagle, owl, polar bear, cave lion or smilodon: pounce on a wild animal close in front and bite it; beside a kill, pick it up to carry (the leopard even up a tree); press again to put it down. Eagle or owl in flight: skim low over a hare (or an otter) to snatch it in your talons and carry it off, then land to eat it (when the prompt shows). With nothing in reach, you still spring forward and snap your jaws (or, in flight, strike out with your talons) |
| G | Snow leopard or smilodon: leap onto a wild animal close in front and clamp your jaws on, up on its back if it's big enough to ride (a reindeer or bigger), where it can only stagger along under you, or pinned flat to the ground from the side if not (a wolf, otter or hare), so it can't get away. You hold on, doing far more damage than a bite, until it falls, you run out of stamina, or you press G or Space to let go. Once you let go, the wound keeps bleeding for a while, and it keeps losing health until the bleeding stops. Eagle or owl in flight: skim low over a hare, an otter or a youngster to seize it alive in your talons and lift it into the air; carrying it tires you even while gliding and slows your climb, and you drop it when you press G again or run out of stamina. Dropped from high up, the fall kills it; landing sets it down unhurt. On the ground beside one, G seizes it and takes off with it. Anything too heavy to lift gets a talon slash instead: you throw your feet forward and rake it, wings flared, springing up to do it on the ground, which strikes hard and leaves it bleeding. With nothing in reach, G still leaps (snow leopard, smilodon) or slashes at the air (eagle, owl) |
| C | Sit down; press again (or move) to get up |
| X | Lie down; press again (or move) to get up. Resting gets your breath back faster |
| Tab | Switch to the next animal you own (woolly mammoth, snow leopard, arctic wolf, sea otter, bald eagle, reindeer, moose, polar bear, snowy owl, cave lion, smilodon, woolly rhino or cave hyena) |
| V | Switch between male and female. In most animals the female is smaller, and she has no mane (lion) or antlers (moose), and smaller antlers (reindeer) or tusks (mammoth); a female eagle, owl or hyena is bigger than the male, and a female snowy owl is barred all over with dark crescents where the male is nearly pure white. A snow leopard only pairs up with one of the other sex. When you are reborn you come back the same sex |
| B | Open the animal shop: buy animals with coins, or pick one you own to play as it. B or Esc closes it |
| M | Show or hide the map of the world, with where you are and every other animal of your kind: wild ones in gold, your own pack, herd or family in green |
| Mouse | Look around. The animal turns its head to look the same way as the camera, so you see the back of its head, as far as it can turn: hardly at all for a mammoth or rhino, round to the shoulder for a deer, and right round over its back for an owl |
| Mouse wheel | Zoom camera |
| Esc / left click | Release / recapture mouse |
| F11 | Toggle fullscreen |

## Layout

- `scenes/Main.tscn` — the world: sky, sun, terrain, wildlife, player and HUD
- `scenes/Player.tscn` — the player animal and its camera
- `scripts/Terrain.cs` — generates the landscape, about one and a half kilometres across (with ranges of rocky hills, caves, a cliff, giant old-growth trees, up to about 40 m tall), lake basins 8 m deep with beds of pale glacial silt (and an ocean filling the north-east corner, a quarter circle 600 m in radius walled in by the edge mountains), collision and mixed forest patches from noise, and remembers where each tree trunk, branch and treetop is for climbers
- `scripts/Terrain.Mountains.cs` — heaps rounded, rocky hills together into a couple of ranges, their slopes running into one another in saddles, stepped in rough ledges, scored by gullies and strewn with tors, with bare rock showing through the snow
- `scripts/Terrain.Caves.cs` — digs caves into the foot of the tallest hills: a winding tunnel under a rock roof you can walk on, opening into a big domed chamber at the back, dim inside, and marked on the map
- `scripts/Terrain.Cliff.cs` — raises a great cliff south-west of the start: a sheer, bare rock face 26 m high, facing the meadow, with a long, gentle slope down its back that you can walk up to look out over the edge
- `scripts/Terrain.Seabed.cs` — shapes the ocean floor into sandy deeps, rocky reefs and pinnacles, and colours it
- `scripts/TreeBuilder.cs` — builds randomised meshes for five kinds of tree (spruce, Scots pine, golden larch, white birch and narrow fir), each with a few long branches held out level, so no two trees look alike, remembering which branches are sturdy enough to walk along
- `scripts/Water.cs` — lake and ocean water surfaces, rolling with waves and seen from above and below, and where (and how high) the water is for drinking, swimming and diving
- `scripts/Seabed.cs` — the underwater world in the ocean: kelp forests, weed-crusted boulders, starfish, urchins and clams on the floor, and schools of cod that scatter from a diver and can be caught
- `scripts/Underwater.cs` — the world under the small lakes: green waterweed, glacial boulders, schools of arctic char that dart away from a diver, and beds of green sea urchins and clams for a diver to eat, which grow back after a while
- `shaders/weed.gdshader` — sways the waterweed in the current
- `scripts/Animal.cs` — base class for playable animals: their 1–10 scores (jump height and length, land and water speed, agility, stamina) and the speeds, jumps, turning and stamina worked out from them, their abilities (climbing, flying, pack, herd, diving), size, diet and camera, their sex (a female grows to her own size, without the parts only males grow, or with smaller ones), how far they are sitting, lying or gone limp in death, turning the head to look the way the camera does, as far as each animal can, and shared mesh-building and posing helpers
- `scripts/Mammoth.cs` — builds a giant woolly mammoth in code, half as big again as a real one (body, trunk, tusks, tail, hair), five times as hard to hurt as anything else, and animates it, sitting back on its rump or lying on its side as elephants sleep
- `scripts/SnowLeopard.cs` — builds the snow leopard in code to real proportions (fur patterned with rosettes and spots, ringed tail, cat face) and animates its walk, gallop, drinking crouch, sitting and sphinx-like lying, and forepaws-first landing when it leaps out of a tree
- `scripts/ArcticWolf.cs` — builds the arctic wolf in code (white winter coat, neck ruff, bushy tail, a big, cute head with large amber eyes) and animates its walk, gallop, drinking, sitting and lying
- `scripts/SeaOtter.cs` — builds the sea otter in code (dense brown fur, grizzled head, whiskers, webbed hind flippers, flat tail) and animates its bounding walk, belly-down swimming (above and under the water), floating on its back, sitting up on its haunches and lying on its back on land
- `scripts/ArcticHare.cs` — builds a giant arctic hare in code, twice a real one's size (white winter coat, black-tipped ears, side-set eyes, long snowshoe hind feet) and animates its bounding run, nibbling, sitting up on its haunches and lying flat; it is wild prey only, not a playable animal
- `scripts/Quadruped.cs` — shared skeleton and movement for the big four-legged animals: legs that walk, gallop and fold, drinking and grazing, sitting up (bears) or bedding down with the legs folded under (deer), lying and going limp, and cloven hooves
- `scripts/Reindeer.cs` — builds the reindeer (grey-brown winter coat, cream throat mane, white rump, dark legs with pale socks, a dark face and small, pale furry muzzle, and a big bull caribou's antlers: thick, wide-spreading C-shaped beams with a burr at the base, forward bez tines, a back tine, long flat palms with points rising along their top edge, and a toothed shovel over the face); it travels with a herd
- `scripts/Moose.cs` — builds the moose (dark coat, a big, barrel-chested body a quarter bigger than a real moose's with a straight, level back, long pale legs, a big, long head with a smaller drooping nose, the bell under its throat, huge broad palmate antlers spreading nearly two metres)
- `scripts/PolarBear.cs` — builds the polar bear, an ice-age giant twice the size of a modern one (dense creamy coat, a straight, level back, short, thick neck, a big, round, cute head with a stubby muzzle, black nose and lips, big shiny eyes and big round ears, huge furry paws with black pads and claws); a hunter whose bite can bring down a mammoth, and that wolves fear
- `scripts/BigCat.cs` — shared body for the big cats, built on the four-legged skeleton and drawn nearly three times life size: a deep-chested body tucking up at the waist, big round paws, a broad cat's head with whisker pads, a pink nose, amber eyes and small rounded dark-backed ears, a jaw that drops open to eat, and a swaying tail
- `scripts/CaveLion.cs` — builds the cave lion with a tawny gold coat, cream beneath, and a long tail ending in a dark brown tuft; a grown male has a great shaggy mane, golden round the face and brown over the neck and chest, and the lioness none; it hunts with a pride of three, males and lionesses
- `scripts/Smilodon.cs` — builds the sabre-toothed smilodon: heavy and short-bodied, with massive forelegs, a back sloping to shorter hind legs, a bobtail, a faintly spotted tawny coat, long blade-like sabres with flanges on the chin, and a huge gape; it hunts alone and can leap on prey and hold on
- `scripts/WoollyRhino.cs` — builds the woolly rhino on the four-legged skeleton, drawn nearly twice life size: a long, shaggy red-brown barrel of a body with a shoulder hump, short pillar legs on three-toed feet, a long head carried low with square grazing lips, a long, flattened front horn swept forward and a short second horn, small furred ears and a tufted tail; it grazes, lives alone with its calf, and shrugs off harm three times over
- `scripts/CaveHyena.cs` — builds the cave hyena on the four-legged skeleton: long forelegs and short hind legs under a sloping back, a pale cream coat crowded with bold, ragged dark blotches, a bristly reddish-brown mane, a big, broad, pale head with heavy cheeks, a black muzzle, rounded ears dark round the inside, and a short bushy tail darkening to a black tip; it hunts with a clan of three, led by the biggest female
- `scripts/BaldEagle.cs` — builds a giant bald eagle in code, twice a real one's size with wings some four metres across (white head and tail, hooked yellow beak, talons, folding feathered wings), flying as every bird does (see `Bird.cs`)
- `scripts/Bird.cs` — what the eagle and owl share: the bird skeleton (body pivoting at the hips, legs folding at a backwards ankle, folding wings, fanning tail) and its animation, waddling, wingbeats, glide, dive, settling down on its legs, resting on its breast and clutching prey in the talons
- `scripts/SnowyOwl.cs` — builds a giant snowy owl in code, nearly twice a real one's size (round head with a flat facial disc, huge yellow eyes, small black beak, feathered feet, broad rounded wings clothed in the same fluffy feathers as its body), the male nearly pure white and the female barred all over, its head turning up to 135 degrees either way
- `scripts/ProceduralTextures.cs` — generates the fur, rosette-spotted coat, blotched hyena coat, barred owl plumage and ivory textures with normal maps at startup
- `scripts/MeshBuilder.cs` — small helper for building meshes in code
- `shaders/fur.gdshader` — sways the hair strands
- `shaders/water.gdshader` — rolls the ocean's swell and colours its shallow and deep water (the small lakes lie still, in one plain colour), and puts foam on the crests and along the shore
- `shaders/kelp.gdshader` — sways the kelp in the swell
- `icons/` — HUD icons beside the bars (heart for health, food for hunger (meat for hunters, a leaf for grazers, a clam for the otter), water drop for thirst, paw for stamina, bubbles for breath)
- `scripts/Grassland.cs` — the edible meadow covering the green steppe; eaten grass flattens and regrows
- `scripts/Player.cs` — movement, jumping, swimming, eating, drinking, sitting and lying down, hunger, thirst and stamina, switching animal, and the camera
- `scripts/Player.Flying.cs` — flight for animals that fly: take off, flap to climb, glide, dive, bank and land
- `scripts/Player.Climbing.cs` — tree climbing for animals that climb: up, down (head first, as cats do) and round the trunk, out along branches, up onto the treetop, and leaping off
- `scripts/Player.Perching.cs` — perching for birds: gliding in onto a sturdy branch lands on it, to walk along, rest or eat there and take off again
- `scripts/Player.Falling.cs` — falling and landing: heavy gravity that pulls harder on the way down, a cat twisting upright in mid-air, reaching its forepaws down and spreading its legs wide in a long fall, a crouch that soaks up a hard landing, and fall damage from drops higher than the animal can take (never for the snow leopard, or the eagle or owl, which open their wings)
- `scripts/Player.Family.cs` — starting a family: stay beside the wild snow leopard for ten seconds as a grown-up snow leopard and the two pair up and have a cub; the mate and cub then travel with the player. Sometimes the wild one wants no mate and fights you with ordinary bites instead; beat it and it gives in and pairs up. Sometimes two wild snow leopards near you fight each other over you, and the winner pairs up with you. You keep only one mate: once you have one, any other wild snow leopard that comes close fights your mate to the death, and if your mate dies the newcomer goes after the cubs one by one; any it fails to kill live on, and it becomes your new mate, with new cubs. Everyone in these fights uses the special attack
- `scripts/Player.Hunting.cs` — hunting for animals that hunt (snow leopard, arctic wolf, cave hyena, bald eagle, snowy owl, polar bear and the big cats): pouncing on wild animals, eating from kills, carrying a kill in the jaws, up a tree if need be, to put it down and eat it there, and for the eagle and owl, snatching prey on the wing and carrying it off in its talons
- `scripts/Player.Gripping.cs` — the snow leopard's (and smilodon's) special attack: leaping onto prey (up on its back if it is big enough) and holding on with its jaws, riding along on it and wearing it down while it drags the cat, until it falls (when a cat on its back rides it down to the ground, then leaps off) or the cat tires, and leaving it bleeding when it lets go
- `scripts/Player.Diving.cs` — diving, for the sea otter and polar bear: Shift takes it under from deep water and Space brings it up, it drifts back up on its buoyant fur when left alone, breath runs down underwater (drowning once it is gone) and comes back at the surface, and E picks up urchins and clams off the bed, in the ocean or a lake, to carry up and eat at the surface (the otter floating on its back), or snaps up a fish
- `scripts/Player.Lifting.cs` — the eagle's and owl's special attack: seizing small prey alive in the talons, carrying it up into the air at a cost in stamina, and dropping it to fall, or slashing with the talons at anything too heavy to lift, leaving it bleeding
- `scripts/Player.Growing.cs` — growing up: the player is born a calf, cub, pup, eaglet or owlet under half its grown size, and grows into an adult over ten minutes of being fed and watered, slower, weaker and lower to the ground until it does. The player can be a male or a female, and every animal it switches to is the same age and sex
- `scripts/Player.Dying.cs` — being hunted and dying: wild wolves bite a player that is a mammoth, otter, eagle or owl on the ground, its health heals once it gets away, and if it dies it lies where it fell until the player chooses an animal to be reborn as, a newborn of the same sex at the start. Packs, herds and families are a mix of males and females, and the mate is always the other sex from the player
- `scripts/Herd.cs` — the computer-controlled wolf pack (bunched close round the player) and mammoth herd that follow the player, and sit or lie down when it does; when the player attacks a wild animal as a wolf, the pack runs in, surrounds it and helps bring it down, and when the player eats from a kill, the pack eats with it; and a wild wolf the player kills as a wolf is reborn twice, as a pup in the player's pack that grows up there and back in its own pack; and the snow leopard's family (its mate and cubs), which hunts and eats with it the same way, leaping on and clamping their jaws on the prey with the special attack (letting go to get their breath back now and then), and has a new cub each time the last one grows up, up to three; the mate can fight to the death against a wild snow leopard that challenges it, and dead family members lie where they fell a while. Any pack or family turns on whatever bites the player, wolves or polar bear
- `scripts/Wildlife.cs` — wild mammoth and reindeer herds, moose and woolly rhino cows with calves, wolf packs and cave hyena clans, sea otter rafts, groups of arctic hares, a lone snow leopard and a giant polar bear by the ocean, and lone cave lions and smilodons on the steppe (which every pack flees, and which hunt anything they can catch, the player included, eat and sleep it off) that live in the world on their own: the mammoths graze and stampede from hunters, the wolves hunt them (and the player, unless it is a wolf, hyena or snow leopard) down, feed and sleep it off, and run from any snow leopard unless the pack is seven strong or more, when it hunts the snow leopard instead, the snow leopard prowls and lies up, sometimes fights off the player's snow leopard rather than pairing up with it, and climbs the nearest tree to escape a big pack, the otters float about the lakes and haul out on the shore to rest, every animal running and tiring by its own scores, and anything that falls (to the wolves or to a snow leopard player) goes limp, is eaten down until nothing is left (small kills fast, big ones slowly), and is reborn as a youngster
- `scripts/Blood.cs` — blood: drops that run from the neck and shoulders of anything bitten or mauled (wild animals and the player alike) and fall to the snow, stretched as they fall, and the ragged, lobed pool, never the same twice, that spreads with spatter round it where an animal falls
- `scripts/BloodTrail.cs` — the spots of blood those drops leave in the snow, so a wounded animal leaves a trail
- `scripts/HealthBar.cs` — the health bar floating over every wild animal, facing the camera: green when healthy, yellow when hurt, red when close to falling
- `scripts/Hud.cs` — current animal name, sex (and how grown it is), health (coloured green, yellow or red like the wild animals' bars), food (its icon showing what the animal eats), water, stamina and, while diving, breath bars, the on-screen action prompt, your coins and chores, messages such as who joined the game, and a warning while wolves are hunting the player
- `scripts/WorldMap.cs` — the map (M): the whole world painted from the terrain, with an arrow for the player and dots for every other animal of its kind, wild or travelling with it
- `scripts/InputSetup.cs` — key bindings
- `scripts/StartMenu.cs` — the start menu: your name, and play alone, host a game, or join a friend's by address
- `scripts/Network.cs` — playing together: hosting and joining, sending your animal to the other players twenty times a second, and carrying bites between players
- `scripts/RemotePlayer.cs` — another player's animal as seen on your screen, gliding between updates, with their name and a health bar over it
- `scripts/Player.Fighting.cs` — biting other players' animals (F), the way every animal can fight in Savannah Life
- `scripts/Player.Chores.cs` — the player's chores: counting what the animal does towards them, paying their coins and setting new ones
- `scripts/Chore.cs` — the chores there are (travel, swim, fly, graze, drink, eat meat, hunt, climb trees, rest, stay underwater, dive for shellfish), who can do each and what it pays
- `scripts/Wallet.cs` — your coins and the animals you have bought, saved on your computer: what each animal costs, and which are free
- `scripts/AnimalShop.cs` — the animal shop, for choosing an animal at the start, buying one with coins (B), or being reborn
- `scripts/Main.cs` — scene startup, mouse/window handling, the gloom inside caves, and the blue-green murk under the water, darker the deeper you go
