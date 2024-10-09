using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Parry;

[BepInPlugin(GUID, MODNAME, VERSION)]
public class Plugin : BasePlugin
{
    internal const string AUTHOR = "CellSkippers";
    internal const string MODNAME = "Parry";
    internal const string GUID = AUTHOR + "." + MODNAME;
    internal const string VERSION = "1.0.0";

    public override void Load()
    {
        Logger.SetupFromInit(this.Log);
        Logger.Info(MODNAME + " is loading...");
        ReadConfigFile();
        new Harmony(GUID).PatchAll();
        Logger.Info(MODNAME + " loaded!");
    }

    public static void ReadConfigFile()
    {
        ConfigFile configFile = new(Path.Combine(Paths.ConfigPath, GUID + ".cfg"), true);
        Patch.parryExplosionDamage = configFile.Bind<float>(
            "Damage", "parryExplosionDamage", 100.01f,
            "The damage of the explosion for parrying tentacles.").Value;
        Patch.parryExplosionRadius = configFile.Bind<float>(
            "Damage", "parryExplosionRadius", 2f,
            "The radius of the explosion for parrying tentacles.").Value;
        Patch.parryBulletDamage = configFile.Bind<float>(
            "Damage", "parryBulletDamage", 150.01f,
            "The damage of the bullet for parrying projectiles.").Value;
        Patch.parryBulletPrecisionMultiplier = configFile.Bind<float>(
            "Damage", "parryBulletPrecisionMultiplier", 1f,
            "The precision multiplier of the bullet for parrying projectiles.").Value;
        Patch.parryBulletStaggerMultiplier = configFile.Bind<float>(
            "Damage", "parryBulletStaggerMultiplier", 1f,
            "The stagger multiplier of the bullet for parrying projectiles.").Value;
        Patch.parryBulletFalloffStart = configFile.Bind<float>(
            "Damage", "parryBulletFalloffStart", 40f,
            "The falloff start distance of the bullet for parrying projectiles.").Value;
        Patch.parryBulletFalloffEnd = configFile.Bind<float>(
            "Damage", "parryBulletFalloffEnd", 100f,
            "The falloff end distance of the bullet for parrying projectiles.").Value;
    }
}