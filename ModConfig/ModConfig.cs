namespace TranslocatorEngineering.ModConfig
{
    public class ModConfig
    {
        public static ModConfig Loaded { get; set; } = new ModConfig();
        public int MaximumLinkRange { get; set; } = 8000;
    }
}
