namespace Newt.Simulation;

/// <summary>Reusable movement and occupancy rules shared by groups of species.</summary>
public enum CritterHabitat
{
    OceanDweller,
    LandDweller,
    FreshwaterDweller,
    Flier,
}

public static class CritterHabitats
{
    public static CritterHabitat GetHabitat(CritterSpecies species) => species switch
    {
        CritterSpecies.Plankton => CritterHabitat.OceanDweller,
        CritterSpecies.Crab or CritterSpecies.Ape => CritterHabitat.LandDweller,
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

        return habitat switch
        {
            CritterHabitat.OceanDweller =>
            terrain is Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows,

            // Land species may ford a river, but lakes remain aquatic habitat.
            CritterHabitat.LandDweller =>
            freshwater is not SurfaceWaterKind.FreshwaterLake &&
            terrain is Terrain.Shallows or Terrain.Trench or Terrain.Canyon or
                Terrain.Plains or Terrain.Hills or Terrain.Ice,

            CritterHabitat.FreshwaterDweller =>
            freshwater is SurfaceWaterKind.River or SurfaceWaterKind.FreshwaterLake,

            _ => false,
        };
    }
}
