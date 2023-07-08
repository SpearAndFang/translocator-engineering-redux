namespace TranslocatorEngineering.ModSystem
{
    using Vintagestory.API.Common;
    public class ModConfig
    {

        //public bool TODO = true;
        public int MaximumLinkRange = 8000;

        // static helper methods
        public static string filename = "TranslocatorEngineeringMod.json";
        public static ModConfig Load(ICoreAPI api)
        {
            var config = api.LoadModConfig<ModConfig>(filename);
            if (config == null)
            {
                config = new ModConfig();
                Save(api, config);
            }
            return config;
        }
        public static void Save(ICoreAPI api, ModConfig config)
        {
            api.StoreModConfig(config, filename);
        }
    }
}
