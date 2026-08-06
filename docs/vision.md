# Product vision

## The promise

Newt is a living terrarium.

The world continues without requiring the player. A player should be able to
leave it running, return later, and discover that species have evolved, migrated,
established themselves in an overlooked habitat, or disappeared. Sentient species
should found villages, construct monuments, form relationships, wage wars, and
eventually fall to rivals, ecological pressure, catastrophe, or time.

The central pleasure is observation: witnessing an ecosystem produce histories
that were simulated rather than scripted. Newt has no winning objective. The
world is a place to shape, briefly inhabit, and watch.

## Design pillars

### 1. The world does not wait for the player

Ecology, evolution, civilization, conflict, and natural forces operate
autonomously. Player absence is a normal state, not a failure state. No essential
system may require player commands to progress.

### 2. History emerges from interacting systems

A species taking root, a settlement flourishing, or a civilization collapsing
should result from understandable world conditions and agent decisions. Authored
events may create pressure, but they should not replace simulation with a fixed
story.

### 3. Individuals and civilizations share one world

Individual critters remain real simulation participants when civilizations form.
Villages, monuments, alliances, and wars are expressions of the same ecological
world rather than a separate strategy-game layer.

### 4. Interaction is optional and embodied

The player's three fundamental activities are:

1. Paint and shape the world.
2. Possess a critter for a time.
3. Watch the autonomous simulation.

Possession must join the existing simulation rather than granting exemption from
it. Its intended feeling is like directly micro-managing a special unit in a
classic strategy sandbox: immediate and personal for a while, but still occurring
inside the larger world.

A possessed critter may eventually:

- Move and act directly.
- Improve a small set of stats through deliberately shallow progression.
- Participate in the same hunger, danger, ecology, and social systems as AI peers.
- Found or join a settlement when its species and circumstances permit.
- Transfer the player's focus from individual control to settlement control.

### 5. Settlement control is simple city building

City and village control provides light construction and minor resource
management. The player may set a few priorities, place buildings, organize basic
defense, and understand allies or enemies. It is not intended to become a deep
RTS, production-chain game, or empire-management game. The controlled settlement
remains subject to ecology, geography, rivals, disasters, and internal limits.

### 6. Failure belongs in the world

Species can go extinct. Villages can be abandoned. Monuments can become ruins.
Player-controlled critters and settlements can die. Loss creates history and new
ecological opportunities rather than requiring the world to reset.

## Desired player experience

The player opens Newt and wonders:

- What changed while I was away?
- What evolved here, and why did it survive?
- Who built this monument?
- What destroyed this settlement?
- Can this one critter survive long enough to change its world?
- If I help this village, can it endure without breaking the ecosystem around it?

These questions arise from simulation rather than authored narrative. Newt does
not require a story, named cast, quests, dialogue, or scripted character arcs.

## Scope boundary

The maximum intended direct-control scope is:

1. Possession of one critter with modest stat progression.
2. A transition into control of one city or village.
3. Simple city building, minor resource management, basic priorities, and local
   relationships with allies and enemies.

Newt is not intended to become a conventional action RPG, a global grand-strategy
game, a deep city-management game, or an RTS whose world exists only as a
battlefield. It has no victory condition and does not build toward a final
conquest, ending, or authored story.

## Architectural consequences

- Simulation state must advance headlessly and independently of rendering or UI.
- AI and player control must issue compatible commands into the same systems.
- Agents must remain simulated when off-screen and when not player-controlled.
- Long-running worlds need save files, versioned migrations, and durable identity.
- History needs an event ledger so discoveries can be explained after the fact.
- Performance work must prioritize large populations and long simulation sessions.
- Determinism and diagnostics remain essential for reproducing emergent failures.
- Civilizations must consume and transform ecological resources rather than use a
  disconnected economy.

## Decision test

When evaluating a feature, ask:

> Does this make the autonomous terrarium more alive, legible, and worth returning
> to—and does player control participate in that world rather than replace it?

If the answer is no, the feature is outside Newt's core vision.

The [canonical emergent scenario](emergent-scenario.md) illustrates the intended
causal depth: a small player intervention can propagate through autonomous
diplomacy, logistics, warfare, ecology, terrain damage, and evolution without
becoming a scripted story.
