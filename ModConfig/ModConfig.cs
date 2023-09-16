namespace TranslocatorEngineering.ModConfig
{
    public class ModConfig
    {
        public static ModConfig Loaded { get; set; } = new ModConfig();
        public int MaximumLinkRange { get; set; } = 8000;

        public bool AlwaysDropAllCrystalShards { get; set; } = false;
        public double RecoveryChanceGateArray { get; set; } = 0.8;
        public double RecoveryChanceParticulationComponent { get; set; } = 0.8;
    }
}
