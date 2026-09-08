# Ape research

Villages can build one library at 300 residents for six wood and five food. The library uses
the military district silhouette with a purple banner. Hovering over it shows
its village's scholars and researched technologies in the Building panel.

Every 30 simulated seconds, a library can recruit an eligible civilian as an
ape scholar, up to two per village. Recruitment preserves the resident's identity,
energy, and village assignment and leaves at least one civilian. Scholars use
village food, stay near their library, and do not reproduce or hunt.

On the same interval, research has a 5% discovery chance per scholar (10% with
two). A successful roll selects an unknown technology uniformly from those whose
prerequisites are met. No scholars means no research. Discoveries belong to the
village and survive losing its library; colonists carry a copy of their origin's
knowledge when dispatched and give it to their new village.

The initial independent technologies are:

- **Sailing:** permits harbors, sailor recruitment, and colonist travel through
  ocean and freshwater tiles.
- **Aquaculture:** permits aquaculture food districts.

Definitions live in `ApeTechnology.cs` as a flat list with prerequisite lists.
Research timing and probability live in `ApeResearch.cs`.

The optional scholar sprite is `src/Newt.Game/Content/Sprites/Critters/ape-scholar.png`.
Until that PNG is supplied, scholars use a purple marker.
