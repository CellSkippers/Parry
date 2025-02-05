using Agents;
using FX_EffectSystem;
using Gear;
using HarmonyLib;
using Player;
using UnityEngine;
using SNetwork;

#nullable disable
namespace Parry;

public enum ParryType
{
    Tentacle,
    Projectile,
    Bullet
}

[HarmonyPatch]
public static class Patch
{
    public static bool parryEnabled = true;

    public static float parryDuration = 0.3f;
    public static float parryExplosionDamage;
    public static float parryExplosionRadius;
    public static float parryBulletDamage;
    public static float parryBulletPrecisionMultiplier;
    public static float parryBulletStaggerMultiplier;
    public static float parryBulletFalloffStart;
    public static float parryBulletFalloffEnd;
    public static float parryHealMulti;
    public static float parryMainAmmoMulti;
    public static float parrySpecAmmoMulti;
    public static float parryToolMulti;
    public static float parryFriendlyBulletMulti;

    public static float shoveTime;

    // Return value is used by the relevant receive damage prefix to determine whether the original receive damage method should run.
    public static bool SuccessfullyParry(Agent damagingAgent, float damageTaken, ParryType parried)
    {
        // Parry not enabled, do nothing and run the original receive damage method.
        if (!parryEnabled)
            return true;

        PlayerAgent localPlayerAgent = PlayerManager.GetLocalPlayerAgent();

        // Play the parry sound.
        localPlayerAgent.Sound.Post(1256202815, true);

        switch(parried)
        {
            case ParryType.Tentacle:
                {
                    //If host, heal relative to damage taken
                    if (SNetwork.SNet.IsMaster)
                        localPlayerAgent.GiveHealth(localPlayerAgent, damageTaken * parryHealMulti);
                    //If client, heal extra to compensate for host's perception of damage taken
                    else
                        localPlayerAgent.GiveHealth(localPlayerAgent, damageTaken * parryHealMulti + damageTaken);
                    //Give main/special/tool ammo respectively
                    localPlayerAgent.GiveAmmoRel(localPlayerAgent,
                    damageTaken * parryMainAmmoMulti / 5f,
                    damageTaken * parrySpecAmmoMulti / 5f,
                    damageTaken * parryToolMulti / 5f
                    );
                    
                    //Tentacle parries explode the enemy
                    if (damagingAgent != null)
                        DoExplosionDamage(damagingAgent.Position, parryExplosionRadius, parryExplosionDamage, 1500f);

                    break;
                }
            case ParryType.Projectile:
                {
                    if (SNetwork.SNet.IsMaster)
                        localPlayerAgent.GiveHealth(localPlayerAgent, damageTaken * parryHealMulti);
                    else
                        localPlayerAgent.GiveHealth(localPlayerAgent, damageTaken * parryHealMulti + damageTaken);
                    localPlayerAgent.GiveAmmoRel(localPlayerAgent,
                    damageTaken * parryMainAmmoMulti / 5f,
                    damageTaken * parrySpecAmmoMulti / 5f,
                    damageTaken * parryToolMulti / 5f
                    );

                    //Projectile parries shoot a static damage bullet
                    Weapon.WeaponHitData parryShot = new()
                    {
                        randomSpread = 0,
                        maxRayDist = 100f
                    };
                    Vector3 originPos = localPlayerAgent.FPSCamera.Position;
                    parryShot.fireDir = (localPlayerAgent.FPSCamera.CameraRayPos - originPos).normalized;
                    parryShot.owner = localPlayerAgent;
                    parryShot.damage = parryBulletDamage;
                    parryShot.staggerMulti = parryBulletStaggerMultiplier;
                    parryShot.precisionMulti = parryBulletPrecisionMultiplier;
                    parryShot.damageFalloff = new Vector2(parryBulletFalloffStart, parryBulletFalloffEnd);
                    if (Weapon.CastWeaponRay(localPlayerAgent.FPSCamera.transform, ref parryShot, originPos))
                    {
                        BulletWeapon.BulletHit(parryShot, true);
                        FX_Manager.EffectTargetPosition = parryShot.rayHit.point;
                    }
                    else
                        FX_Manager.EffectTargetPosition = localPlayerAgent.FPSCamera.CameraRayPos;
                    FX_Manager.PlayLocalVersion = false;
                    BulletWeapon.s_tracerPool.AquireEffect().Play((FX_Trigger)null, localPlayerAgent.EyePosition, Quaternion.LookRotation(parryShot.fireDir));

                    break;
                }
            case ParryType.Bullet:
                {
                    if (!SNetwork.SNet.IsMaster)
                        localPlayerAgent.GiveHealth(localPlayerAgent, damageTaken);

                    //Parried friendly fire shoots a new bullet with damage scaled up based off the bullet parried
                    Weapon.WeaponHitData parryShot = new()
                    {
                        randomSpread = 0,
                        maxRayDist = 100f
                    };
                    Vector3 originPos = localPlayerAgent.FPSCamera.Position;
                    parryShot.fireDir = (localPlayerAgent.FPSCamera.CameraRayPos - originPos).normalized;
                    parryShot.owner = localPlayerAgent;
                    //Scaled by 50 to compensate for player health and friendly fire scalings
                    parryShot.damage = damageTaken * 50f * parryFriendlyBulletMulti;
                    Logger.DebugOnly(parryShot.damage);
                    parryShot.staggerMulti = 1f;
                    parryShot.precisionMulti = 2f;
                    parryShot.damageFalloff = new Vector2(80f, 100f);
                    if (Weapon.CastWeaponRay(localPlayerAgent.FPSCamera.transform, ref parryShot, originPos))
                    {
                        BulletWeapon.BulletHit(parryShot, true);
                        FX_Manager.EffectTargetPosition = parryShot.rayHit.point;
                    }
                    else
                        FX_Manager.EffectTargetPosition = localPlayerAgent.FPSCamera.CameraRayPos;
                    FX_Manager.PlayLocalVersion = false;
                    BulletWeapon.s_tracerPool.AquireEffect().Play((FX_Trigger)null, localPlayerAgent.EyePosition, Quaternion.LookRotation(parryShot.fireDir));

                    break;
                }
        }

        // Don't run the original receive damage method - player doesn't take the damage.
        return false;
    }

    public static void DoExplosionDamage(Vector3 sourcePos, float radius, float damage, float explosionForce)
    {
        Logger.DebugOnly("Doing custom explosion damage.");
        int numCollidersHit = Physics.OverlapSphereNonAlloc(sourcePos, radius, DamageUtil.s_tempColliders, LayerManager.MASK_EXPLOSION_TARGETS);
        if (numCollidersHit < 1)
            return;

        DamageUtil.IncrementSearchID();
        for (int index = 0; index < numCollidersHit; ++index)
        {
            Collider tempCollider = DamageUtil.s_tempColliders[index];

            // Don't move towards applying damage if target is a player.
            if (tempCollider.GetComponent<Dam_PlayerDamageBase>() || tempCollider.GetComponent<Dam_PlayerDamageLimb>())
                continue;

            // Add force to rigid bodies.
            Rigidbody rigidbodyComponent = tempCollider.GetComponent<Rigidbody>();
            if (rigidbodyComponent)
                rigidbodyComponent.AddExplosionForce(explosionForce, sourcePos, radius, 3f);
            
            // Deal damage!
            IDamageable iDamageableComponent = tempCollider.GetComponent<IDamageable>();
            if (iDamageableComponent != null)
            {
                IDamageable baseDamagable = iDamageableComponent.GetBaseDamagable();
                Logger.DebugOnly("baseDam: " + baseDamagable + " baseDam.TempSearchID: " + baseDamagable?.TempSearchID + " searchID: " + DamageUtil.SearchID);
                if (baseDamagable != null && baseDamagable.TempSearchID != DamageUtil.SearchID)
                {
                    Vector3 explosionVector = iDamageableComponent.DamageTargetPos - sourcePos;
                    if (!Physics.Raycast(sourcePos, explosionVector.normalized, out RaycastHit hitInfo, explosionVector.magnitude, LayerManager.MASK_EXPLOSION_BLOCKERS) || hitInfo.collider == tempCollider)
                    {
                        baseDamagable.TempSearchID = DamageUtil.SearchID;
                        iDamageableComponent.ExplosionDamage(damage, sourcePos, Vector3.up * explosionForce);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(MWS_Push), nameof(MWS_Push.Enter))]
    [HarmonyPostfix]
    public static void ParryEnter()
    {
        shoveTime = Clock.Time;
    }

    [HarmonyPatch(typeof(Dam_PlayerDamageLocal), nameof(Dam_PlayerDamageLocal.ReceiveShooterProjectileDamage))]
    [HarmonyPrefix]
    public static bool ParryShooterProjectileDamage(pMediumDamageData data)
    {
        Logger.DebugOnly("Received shooter projectile damage.");
        float tookDamageTime = Clock.Time;
        if (tookDamageTime > shoveTime && tookDamageTime - shoveTime < parryDuration)
        {
            return SuccessfullyParry(null, data.damage.Get(1f), ParryType.Projectile);
        }
        return true;
    }

    [HarmonyPatch(typeof(Dam_PlayerDamageLocal), nameof(Dam_PlayerDamageLocal.ReceiveTentacleAttackDamage))]
    [HarmonyPrefix]
    public static bool ParryTentacleAttackDamage(pMediumDamageData data)
    {
        Logger.DebugOnly("Received tentacle attack damage.");
        float tookDamageTime = Clock.Time;
        if (tookDamageTime > shoveTime && tookDamageTime - shoveTime < parryDuration)
        {
            data.source.TryGet(out Agent damagingAgent);
            return SuccessfullyParry(damagingAgent, data.damage.Get(1f), ParryType.Tentacle);
        }
        return true;
    }

    [HarmonyPatch(typeof(Dam_PlayerDamageLocal), nameof(Dam_PlayerDamageLocal.ReceiveBulletDamage))]
    [HarmonyPrefix]
    public static bool ParryBulletAttackDamage(pBulletDamageData data)
    {
        Logger.DebugOnly("Received bullet attack damage.");
        float tookDamageTime = Clock.Time;
        if (tookDamageTime > shoveTime && tookDamageTime - shoveTime < parryDuration)
        {
            return SuccessfullyParry(null, data.damage.Get(1f), ParryType.Bullet);
        }
        return true;
    }

    //Ensure clients are not killed/trading with enemies based off hosts perception of them parrying
    [HarmonyPatch(typeof(Dam_PlayerDamageLocal), nameof(Dam_PlayerDamageLocal.ReceiveSetDead))]
    [HarmonyPrefix]
    public static bool ParrySetDead()
    {
        Logger.DebugOnly("Received set dead.");
        float tookDamageTime = Clock.Time;
        if (tookDamageTime > shoveTime && tookDamageTime - shoveTime < parryDuration)
        {
            return false;
        }
        return true;
    }
}
