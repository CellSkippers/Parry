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
    internal const string VERSION = "1.1.1";

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
            "Explosion", "parryExplosionDamage", 100.01f,
            "The damage of the explosion for parrying tentacles.").Value;
        Patch.parryExplosionRadius = configFile.Bind<float>(
            "Explosion", "parryExplosionRadius", 2f,
            "The radius of the explosion for parrying tentacles.").Value;
        Patch.parryBulletDamage = configFile.Bind<float>(
            "Bullet", "parryBulletDamage", 150.01f,
            "The damage of the bullet for parrying projectiles.").Value;
        Patch.parryBulletPrecisionMultiplier = configFile.Bind<float>(
            "Bullet", "parryBulletPrecisionMultiplier", 1f,
            "The precision multiplier of the bullet for parrying projectiles.").Value;
        Patch.parryBulletStaggerMultiplier = configFile.Bind<float>(
            "Bullet", "parryBulletStaggerMultiplier", 1f,
            "The stagger multiplier of the bullet for parrying projectiles.").Value;
        Patch.parryBulletFalloffStart = configFile.Bind<float>(
            "Bullet", "parryBulletFalloffStart", 40f,
            "The falloff start distance of the bullet for parrying projectiles.").Value;
        Patch.parryBulletFalloffEnd = configFile.Bind<float>(
            "Bullet", "parryBulletFalloffEnd", 100f,
            "The falloff end distance of the bullet for parrying projectiles.").Value;
        Patch.parryHealMulti = configFile.Bind<float>(
            "OnParry", "parryHealMulti", 1f,
            "The multiplier for receiving health on a successful parry.\n(Default value gives damage-of-hit percent health.)").Value;
        Patch.parryMainAmmoMulti = configFile.Bind<float>(
            "OnParry", "parryMainAmmoMulti", 1f,
            "The multiplier for receiving main ammo on a successful parry.\n(Default value gives damage-of-hit percent of a regular ammo pack.)").Value;
        Patch.parrySpecAmmoMulti = configFile.Bind<float>(
            "OnParry", "parrySpecAmmoMulti", 1f,
            "The multiplier for receiving special ammo on a successful parry.\n(Default value gives damage-of-hit percent of a regular ammo pack.)").Value;
        Patch.parryToolMulti = configFile.Bind<float>(
            "OnParry", "parryToolMulti", 1f,
            "The multiplier for receiving tool on a successful parry.\n(Default value gives damage-of-hit percent of a regular tool pack.)").Value;
        Patch.parryFriendlyBulletMulti = configFile.Bind<float>(
            "OnParry", "parryFriendlyBulletMulti", 4f,
            "The damage multiplier for the bullet produced by parrying someone else's bullet.\n(Default value multiplies damage by 4.)").Value;
    }
}