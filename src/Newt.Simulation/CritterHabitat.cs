namespace Newt.Simulation;

/// <summary>Reusable movement and occupancy rules shared by groups of species.</summary>
public enum CritterHabitat
{
    OceanDweller,
    ShallowSeeker,
    ShorelineHunter,
    LandDweller,
    FreshwaterDweller,
    Flier,
}

public static class CritterHabitats
{
    public static CritterHabitat GetHabitat(CritterSpecies species) => species switch
    {
        CritterSpecies.Plankton or CritterSpecies.Jellyfish or CritterSpecies.Trilobite or
            CritterSpecies.Nautilus or CritterSpecies.Fish or CritterSpecies.Squid or
            CritterSpecies.SquidEgg or CritterSpecies.ToothedWhale or CritterSpecies.BaleenWhale =>
            CritterHabitat.OceanDweller,
        CritterSpecies.SeaScorpion => CritterHabitat.ShorelineHunter,
        CritterSpecies.ApeSailor => CritterHabitat.ShorelineHunter,
        CritterSpecies.Worm => CritterHabitat.ShallowSeeker,
        CritterSpecies.Newt or CritterSpecies.MegaToad or CritterSpecies.Therapsid or
            CritterSpecies.Monkey or CritterSpecies.Ape or CritterSpecies.Deer or CritterSpecies.Elk or
            CritterSpecies.Gazelle or
            CritterSpecies.Wolf or CritterSpecies.Crab =>
            CritterHabitat.LandDweller,
        _ => throw new ArgumentOutOfRangeException(nameof(species)),
    };

    public static bool CanOccupy(
        CritterHabitat habitat,
        Terrain terrain,
        SurfaceWaterKind freshwater,
        Biome biome = Biome.None,
        SurfaceCover surfaceCover = SurfaceCover.None)
    {
        if (habitat is CritterHabitat.Flier)
        {
            return terrain is not Terrain.Mountain || biome is not Biome.Arctic;
        }

        if (surfaceCover is not SurfaceCover.None)
        {
            return false;
        }

        // Land critters travel on top of an ice sheet, while aquatic critters
        // are treated as moving through the water beneath it.
        if (terrain is Terrain.Ice)
        {
            return true;
        }

        return habitat switch
        {
            CritterHabitat.OceanDweller =>
            terrain is Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows,

            // Worms may cross open saltwater while following adjacent chemical
            // cues, but they can feed only after reaching shallows.
            CritterHabitat.ShallowSeeker =>
            terrain is Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows,

            CritterHabitat.ShorelineHunter =>
            terrain is Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Beach,

            // Land species may ford a river, but lakes remain aquatic habitat.
            CritterHabitat.LandDweller =>
            freshwater is not SurfaceWaterKind.FreshwaterLake &&
            terrain is Terrain.Shallows or Terrain.Beach or Terrain.Lowlands or
                Terrain.Canyon or Terrain.Trench or Terrain.Plains or Terrain.Hills or Terrain.Ice,

            CritterHabitat.FreshwaterDweller =>
            freshwater is SurfaceWaterKind.River or SurfaceWaterKind.FreshwaterLake,

            _ => false,
        };
    }
}
