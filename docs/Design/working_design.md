
# high concept
Old-school Rogue clone using ANSI art, but multiplayer.
functionally a mix between diablo and Rogue; rogue-style art and "one-input-equals-one-time-step" game loop, but diablo loot/equipment randomness/farming and character concepts

# technical
Browser launched, simple authoritative server
Simple React client, host as azure static web app
ASP.NET Core server app in container, probably host on azure container apps
WebSockets for communication
SQLite for persistance, mostly about storing player/character data & progression between sessions, store in azure files
EFCore for ORM to SQLite
authentication is OAuth through microsoft entra ID (allow any MS account), use MSAL.js and auth code flow with PKCE

Nice to have: possibility of a true terminal-based client, launch locally with a server URL argument to connect to the server and play

# processes
players would launch the 'game', login, optionally join a party with other players, select a character, host can configure options/difficulty/etc, and then start a 'dive' into the dungeon
progress earned such as acquiring equipment, XP, or reaching milestones like reaching new levels or defeating certain bosses would be saved to the character record in the database, but the state of any give dive is ephemeral to that session.

# loop
game loop would not be a 'tick' system, would react to player inputs similar to OG Rogue
due to multiplayer, once any player begins an input, all players have a maximum of 30 seconds (configurable per dive) to select their action for that loop before it is executed. any player who does not select an action just holds position.
the intent behind this loop is to allow players to work together and play together, but not hinder each other, and to create a sense of urgency (you have to have a plan to deal with whatever is happening around you because someone might just be walking across an empty room while you're surrounded by enemies) and cooperation (you should be careful about just running across an empty space, the other player(s) might be dealing wiht something complex)

# loot
items are randomly generated (diablo/borderlands style loot) and scaled to a combination of the player's rank and the level's difficulty
detailed equipment system would allow for diablo-like equipment screen

# character stats
start with RPG basics
hitpoints
mana pool

armor class (reduces incoming non-magic damage, flat rate up until an AC cap (around 50% reduction) then diminishing returns after that until around 90% max reduction, AC cap increases by character level so maintaining the same AC as you level up results in less damage reduction)
magic defense (similar to armor class, but only for magical damage)

Melee Attack value (chance to hit and damage bonus)
Ranged Attack Value (chance to hit and damage bonus)
Magic Attack Value (chance to hit and damage bonus)

Melee Defense value (chance to not be hit)
Range Defense value (chance to not be hit)
Magic Defense value (chance to not be hit)

Luck (increases attack and defense chances by a small amount, also affects loot randomization and also mob spawning randomization; better luck means rarer mobs and loot but also probably more risky mobs)

characters should also have a pool of talents/feats/alternate-abilities that give them special modifiers or additional abilities. e.g. starting characters cannot cast magic, unlocking magic requires a talent, number of spells known could be a talent, using specific types of equipment could be talents. need build out a large and deep talent pool


# server-side internals
## Controllers / Hubs
WebSocket hub
Accepts player actions
Sends updated game state

example
``` csharp
public class GameHub : Hub
{
    private readonly GameEngine _engine;

    public async Task PlayerAction(PlayerAction action)
    {
        var state = _engine.ProcessAction(Context.ConnectionId, action);
        await Clients.All.SendAsync("StateUpdated", state);
    }
}
```


## Game Engine Layer
A single service responsible for:
Dungeon generation
Player movement
Monster AI
Combat resolution
Turn processing
This is your “domain” layer, but you don’t need DDD patterns.

example
``` csharp
public class GameEngine
{
    private readonly GameState _state;
    private readonly ICharacterRepository _repo;

    public GameState ProcessAction(string playerId, PlayerAction action)
    {
        // update player
        // move monsters
        // resolve combat
        // update dungeon
        // save progress
        return _state;
    }
}

```

## State Management Layer
In-memory game state
Player sessions
Dungeon map
Monster positions

example
``` csharp
public class GameState
{
    public Dictionary<string, Player> Players { get; set; }
    public List<Monster> Monsters { get; set; }
    public Tile[,] Dungeon { get; set; }
}
```

## Persistence Layer
SQLite repository
Save/load character progress
Very small schema


## Message Contracts
Simple DTOs for client/server communication

## Utilities
RNG
Map helpers



# player input
player input should be a combination of an action and a direction
direction comes from 10-key inputs or arrow-key chords (if the player doesn't have a 10-key keyboard, they can use the arrow keys for cardinal directions or two-key chords for diagonals), "5" on 10-key or left+right on arrow keys indicates "here" (same space)
actions should include:
A for attack (melee)
S for shoot (ranged)
D for defend (reduces incoming damage)
W for walk
R for run (move two spaces, but build up a stacking defense/attack penalty that slow drops on subsequent actions)
C for cast (followed by a number for which prepped spell to cast)
q, e, f, z, x, v are reserved for further inputs later
if only a 10-key input is entered then a default is evaluated: if the space is clear it would just be a walk to that space, if it is occupied by a hostile monster it would be a melee attack, otherwise no action.

if the player presses T, they have to tap it a second time to confirm, when they do confirm they're in "talk mode" where they can free-type a message to all players, this ignores the normal inputs in order to allow the user to type the message, but pressing enter sends the message and returns to normal keybinds. this does not trigger, pause, or interrupt the countdown for player actions starting a game loop


# UI
React rendering the world as ANSI art
include a display of the dungeon and everything the player has already revealed, but only show non-terrain out to a specific orthagonal range (starts low, around 10, but player can unlock higher sight range)
include a mini-map (still ansi art, just very small font size, terrain only)
include a summary of the player's current condition (e.g. hitpoints, mana pool, status effects)
include a brief list of valid actions (including the hotkey that activates those actions)
make the list of actions clickable, if the player clicks on an action that is directional then show a small input like 10key input allowing the user to click the direction
include a scrolling "adventure log" field that gives text descriptions of what is happening (including the random rolls, math, and results of each action)
stylize everything to look like old-school Rogue, heavily based on ANSI characters and ANSI art. Default to Rogue-like "white text on black background", but allow players to pick from a set of color combinations for text-color/background-color values if they prefer other styles.

## mobile
how would this look on a mobile screen? the UI would have to be significantly shortened and everything would have to be optmizied for touch-based input and the layout would have to be designed for a vertical alignment instead of horizontal (desktops would typically be widescreen, phones would be vertical)

