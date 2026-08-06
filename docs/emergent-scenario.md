# Canonical emergent scenario

This scenario describes the depth and causal continuity Newt should eventually
support. It is not a scripted campaign, required progression path, or promise that
every run produces these events. It is an architectural acceptance test: the
systems should be capable of producing a comparable chain without special-case
story logic.

## 1. Discovery and embodiment

A sentient species evolves and establishes a small primitive population. The
player notices it and possesses one warrior. While possessed, the warrior remains
an ordinary simulation participant. The player hunts nearby predators, and the
warrior carries the resulting food back to its village using the same food and
transport systems an AI warrior would use.

The player selects the village and places a farm on a suitable tile. At any time,
the player can release the warrior and village, inspect another region, paint
terrain, spawn organisms, or trigger geological events. Released agents resume AI
control without changing identity or losing their accumulated state.

## 2. Settlement growth

With guidance and favorable conditions, the village grows into a city. Population,
accessible resources, terrain, trade, and constructed prerequisites unlock new
structures. Development is material rather than an abstract technology menu:

- Farms and industries occupy specific tiles.
- Advanced structures require population and resource inputs.
- Military structures recruit progressively capable units.
- Ports and coastal access permit navies and trade.
- Late industry may produce aircraft.
- Power plants consume fuel and supply structures that require power.

The city fields an army, navy, industries, and eventually early air power. These
systems remain restrained enough for light city building but deep enough to alter
the surrounding world.

## 3. Player-instigated war

The player orders the city to declare war on a nearby settlement belonging to
another sentient species. The declaration is an explicit intervention rather than
the result of deteriorating diplomatic relations, and other societies remember
that distinction.

Armies autonomously march toward strategic objectives. The player may redirect a
small number of units or possess one directly, but does not need to command every
soldier. The player may even switch to the defending settlement and reposition
some defenders. Control changes commands, not allegiance, identity, or simulation
rules.

## 4. Defeat, displacement, and regional consequences

The smaller settlement is defeated because it has fewer people, less advanced
military infrastructure, and weaker recruits. Its survivors do not simply vanish.
Depending on circumstance, they may:

- Flee to friendly settlements.
- Found refugee colonies.
- Become nomadic populations.
- Fragment into pirates, raiders, or barbarians.

The destroyed settlement had supplied excess food to a coastal trade partner.
That trade route ends. The coastal city detects its declining food reserves,
prioritizes farms, and attempts to buy food from remaining markets. Regional food
demand and prices rise.

## 5. Diplomacy and autonomous retaliation

The coastal city attributes the disruption and destruction of its partner to the
aggressor. Relations deteriorate. It expands military production, constructs
warships, and plans an invasion without further player input.

Its forces cross the sea and conduct a beach landing. Combat creates a persistent
front rather than a temporary visual effect:

- Units die and leave corpses.
- Scavengers consume the dead.
- Farms and grasslands become mud or craters.
- Logistics and terrain influence the front.
- Population losses weaken both cities beyond the battlefield.

## 6. Escalation and ecological aftermath

Facing severe losses, the technologically advanced defending city uses an aircraft
and a strategic uranium deposit from a nearby mountain to deliver a nuclear
weapon. The attacking city is destroyed. Population and buildings are lost, and
radioactive waste remains as persistent terrain.

Radiation then re-enters the ecological simulation:

- Critters occupying contaminated tiles have increased mutation probability.
- Some species can produce wasteland variants.
- Habitats, migration, scavenging, and recolonization respond to the new terrain.
- Ruins and contamination become the starting conditions for future history.

Everything after the initial declaration of war can occur without another player
command.

## Required system chain

```text
Player command
    -> diplomacy and historical responsibility
    -> autonomous strategic planning
    -> recruitment and material logistics
    -> movement and combat
    -> refugees and population displacement
    -> disrupted trade and market prices
    -> settlement reprioritization
    -> retaliatory war
    -> persistent terrain and corpse ecology
    -> strategic-resource weapon
    -> radiation-driven mutation and succession
```

No link should exist solely to make this scenario happen. Each link must be a
general system capable of combining differently in another world.

## Design requirements revealed by the scenario

- Player and AI commands use the same command model.
- Control can transfer without replacing an entity or faction.
- Settlements operate autonomously before, during, and after player control.
- Diplomacy records causes, obligations, aggression, and trade dependence.
- Resources occupy world locations and flow through explicit routes.
- Prices respond to supply, demand, and route disruption.
- Technology depends on population, structures, energy, and strategic resources.
- Armies can receive high-level objectives plus limited positional overrides.
- Defeat produces refugees, colonies, nomads, pirates, or raiders.
- Combat modifies terrain and feeds scavenger ecology.
- Destruction leaves persistent ruins, contamination, and demographic effects.
- Environmental damage feeds back into evolution and habitat suitability.
- A historical event ledger explains why autonomous actors made major decisions.

## Scope guardrail

The scenario calls for systemic breadth, not interface complexity. The player does
not need detailed worker schedules, individual dialogue, elaborate equipment,
dozens of resource bars, or continuous military micromanagement. Most behavior is
automatic. A small number of legible choices should be capable of producing large
downstream consequences.
