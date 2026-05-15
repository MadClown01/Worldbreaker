# Copilot Instructions - Worldbreaker

## Project Overview

**Worldbreaker** is a 2D turn-based tile strategy game built with Godot 4.6. The game features grid-based gameplay where players control units on a tile-based battlefield, taking turns to move, attack, and interact with the environment.

**Tech Stack:**
- Engine: Godot 4.6
- Language: C#
- Rendering: Forward Plus
- Platform: Linux

---

## Project Structure

```
scenes/
├── entities/             # Unit scenes (.tscn)
└── ui/                   # UI scenes (.tscn)
    ├── BattleScreen.tscn
    ├── panels/
    │   ├── UnitInfoPanel.tscn
    │   └── ActionPanel.tscn
    └── components/
        └── HealthBar.tscn

scripts/
├── autoloads/            # Singletons registered in Project Settings → Autoloads
│   ├── GameEvents.cs     # Global signal bus — all cross-boundary signals live here
│   └── GameState.cs      # Single source of truth for runtime battle state
├── systems/              # Pure game logic — no UI references allowed
│   └── CombatSystem.cs
├── entities/             # Scripts attached to entity scenes
│   └── Unit.cs
├── ui/                   # Scripts attached to UI scenes — no game logic allowed
│   ├── BattleScreen.cs
│   ├── panels/
│   │   ├── UnitInfoPanel.cs
│   │   └── ActionPanel.cs
│   └── components/
│       └── HealthBar.cs
└── shared/               # Plain C# types — no Node inheritance
    ├── UnitData.cs        # Promote to resources/ when Inspector editing is needed
    └── enums/
        ├── GamePhase.cs   # PlayerTurn, EnemyTurn, ResolvingAction, BattleOver
        └── UnitAction.cs  # Move, Attack, EndTurn

resources/                # Godot .tres assets — unit stats, abilities, levels
assets/                   # Art, audio, visual assets
```

---

## Architecture

### Core Principles

1. **UI never calls game logic directly.** UI scripts only emit signals via `GameEvents`.
2. **Game logic never references UI nodes.** Systems only listen to signals and mutate `GameState`.
3. **All cross-boundary communication goes through `GameEvents`.** No exceptions.
4. **Only `GameState` holds mutable runtime data.** Only `scripts/systems/` writes to it.
5. **Always unsubscribe in `_ExitTree()`.** Godot does not clean up C# delegates automatically — this is the most common crash source.

### Layer Responsibilities

| Layer | Allowed to | Not allowed to |
|---|---|---|
| `scripts/ui/` | Read `GameState`, emit signals via `GameEvents` | Call methods on any `scripts/systems/` or `scripts/entities/` type |
| `scripts/systems/` | Mutate `GameState`, emit output signals | Reference any `scripts/ui/` type or node |
| `GameState` | Hold and mutate data, emit signals on change | Know about either layer |
| `GameEvents` | Declare signals | Contain any logic |

### Autoloads

Register in **Project → Project Settings → Autoloads** in this order:

| Name | Path |
|---|---|
| `GameEvents` | `res://scripts/autoloads/GameEvents.cs` |
| `GameState` | `res://scripts/autoloads/GameState.cs` |

Order matters — `GameState` subscribes to `GameEvents` in `_Ready()`.

### Turn-Based Game Flow

```
Turn Start → Player Input (UI emits signal) → CombatSystem resolves →
GameState mutates → Output signals → UI updates → Win Condition Check
```

---

## Code Conventions

### C# Guidelines

- Use **PascalCase** for classes, methods, and properties
- Use **_camelCase** for private fields
- Use **camelCase** for local variables and parameters
- Prefer explicit types over `var` except where the type is obvious from context
- Add XML doc comments to public methods
- Keep methods focused and single-responsibility
- Always specify `public`, `private`, or `protected` — never rely on defaults

### Godot-Specific Rules

- Signal delegate names **must** end in `EventHandler` (required by Godot's C# source generator)
- Use `[Export]` instead of `GetNode<T>()` paths where possible — survives scene refactors
- Use `QueueFree()` not `Free()` — deferred deletion is safe mid-frame
- Never use `FindChild()` or equivalent tree searches to locate nodes across boundaries — use autoloads instead
- Scenes in `scenes/` own their visual representation; scripts in `scripts/` own their behaviour

### Example Class Structure

```csharp
/// <summary>
/// Handles player and enemy combat resolution for a single battle.
/// Listens to input signals from UI and mutates GameState.
/// </summary>
public partial class CombatSystem : Node
{
    public override void _Ready()
    {
        GameEvents.Instance.PlayerRequestedAttack += OnAttackRequested;
    }

    /// <summary>Resolves an attack action between two units.</summary>
    private void OnAttackRequested(int attackerId, int targetId)
    {
        var attacker = GameState.Instance.Units[attackerId];
        var target   = GameState.Instance.Units[targetId];

        int damage = Mathf.Max(0, attacker.AttackPower - target.Defence);
        GameState.Instance.ApplyDamage(targetId, damage);

        GameEvents.Instance.EmitSignal(GameEvents.SignalName.UnitAttacked,
            attackerId, targetId, damage);
    }

    public override void _ExitTree()
    {
        GameEvents.Instance.PlayerRequestedAttack -= OnAttackRequested;
    }
}
```

---

## Common Development Tasks

### Adding a New Unit Type

1. Create a new scene in `scenes/entities/` inheriting from the base unit scene
2. Attach a script in `scripts/entities/` inheriting from `Unit.cs`
3. Define stats in a `UnitData` record in `scripts/shared/UnitData.cs` (or a `.tres` Resource if Inspector editing is needed)
4. Register the unit in `GameState` at battle start
5. No UI changes required — `UnitInfoPanel` reads from `GameState` via signals

### Adding a New Signal

1. Declare the delegate in `scripts/autoloads/GameEvents.cs` in the appropriate comment group
2. Emit it from a `scripts/systems/` class after a state change
3. Subscribe to it in the relevant `scripts/ui/` class
4. Unsubscribe in `_ExitTree()`

### Adding a New UI Panel

1. Create the scene in `scenes/ui/panels/`
2. Create the script in `scripts/ui/panels/`
3. Script subscribes to relevant `GameEvents` output signals
4. Script emits `GameEvents` input signals on user interaction
5. Script never holds a reference to any system or entity node

### Modifying Turn Flow

- Turn state lives in `GameState.Phase` (`GamePhase` enum)
- `CombatSystem` owns turn transitions — update there
- Emit `TurnChanged` after any phase transition so UI stays in sync
- Enemy AI placeholder lives in `CombatSystem.OnEndTurnRequested()` — expand in place

### Promoting Hardcoded Data to Resources

When unit stats need designer editing without recompiling:
1. Add `[GlobalClass]` to the data class and inherit from `Resource` instead of plain C#
2. Move the file to `resources/`
3. Create `.tres` instances per unit type in `resources/units/`
4. Load at runtime with `GD.Load<UnitData>("res://resources/units/warrior.tres")`

---

## Signal Reference (MVP)

### Input Signals (UI → game logic)

| Signal | Emitted by | Heard by |
|---|---|---|
| `PlayerRequestedMove(int unitId, Vector2I cell)` | `ActionPanel` | `CombatSystem` |
| `PlayerRequestedAttack(int unitId, int targetId)` | `ActionPanel` | `CombatSystem` |
| `PlayerRequestedEndTurn()` | `ActionPanel` | `CombatSystem` |
| `PlayerSelectedUnit(int unitId)` | `BattleScreen` | `UnitInfoPanel` |

### Output Signals (game logic → UI)

| Signal | Emitted by | Heard by |
|---|---|---|
| `UnitMoved(int unitId, Vector2I cell)` | `CombatSystem` | `BattleScreen` |
| `UnitAttacked(int attackerId, int targetId, int damage)` | `CombatSystem` | `BattleScreen`, `HealthBar` |
| `UnitDied(int unitId)` | `GameState` | `BattleScreen`, `UnitInfoPanel` |
| `TurnChanged(int turn, GamePhase phase)` | `GameState` | `BattleScreen` |
| `ActiveUnitChanged(int unitId)` | `GameState` | `ActionPanel`, `UnitInfoPanel` |
| `BattleEnded(bool playerWon)` | `CombatSystem` | `BattleScreen` |

---

## Testing & Debugging in Godot

### Running the Project

```bash
# Open project in Godot editor
godot --path /home/callumb/Worldbreaker

# Run the project — press F5 or click Play from within the editor
```

### Debugging Tips

- Use `GD.Print()` for state tracking (C# equivalent of `print()`)
- Check autoload signal connections in the Remote tab during a running session
- Monitor `GameState` properties via the debugger to verify correct mutation
- Use Godot's built-in debugger (Debug menu) for stepping through logic
- If a node method is called after free, a missing `_ExitTree()` unsubscribe is almost always the cause

### Testing Turn Mechanics

- Verify `GameState.Phase` transitions correctly through the `GamePhase` enum
- Check that `CombatSystem` correctly blocks input signals during `EnemyTurn` and `ResolvingAction` phases
- Validate movement and attack ranges before emitting output signals
- Test `BattleEnded` fires correctly when all units on one side reach 0 HP

---

## MCP Server Integration

### Godot MCP Server

This project can use **godot-mcp**, an MCP server that enables direct interaction with the Godot engine from AI agents.

**Features:**
- Launch and control the Godot editor
- Run the project and capture debug output
- Execute operations without generating temporary files
- Real-time feedback on code changes

**Setup:**
To enable godot-mcp in your Copilot CLI or coding agent:
1. Install Node.js dependencies for the MCP server (requires npm)
2. Add the godot-mcp server configuration to your agent's environment
3. Restart your agent/session to activate the tools

For setup instructions, see: https://github.com/Coding-Solo/godot-mcp

---

## Deferred (Post-MVP)

| Feature | Trigger to add |
|---|---|
| `SaveManager` autoload | When a campaign loop exists |
| Domain-split event files (`CombatEvents`, `TurnEvents` etc.) | When `GameEvents` exceeds ~30 signals |
| `GameState` decomposition into sub-state classes | When `GameState` exceeds ~150 lines |
| `UiManager` screen stack | When there are 3+ distinct screens |
| `SimulationRunner` with ordered system pipeline | When a second system is added |
| Promote `UnitData` to Godot `Resource` | When stats need Inspector editing |
| Command pattern / undo | When action history or replay is needed |
| Economy, Research, Population systems | Post-battle MVP |

---

## Collaboration Guidelines

- Write descriptive commit messages mentioning the layer modified (e.g., `CombatSystem: add attack range validation`, `ActionPanel: emit PlayerRequestedMove on cell click`)
- Never commit a `scripts/ui/` file that holds a reference to a `scripts/systems/` type
- Never commit a `scripts/systems/` file that holds a reference to a `scripts/ui/` type
- Test changes in the Godot editor before committing
- Document new signals in the Signal Reference table above when added
