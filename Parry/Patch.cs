﻿using Agents;
using FX_EffectSystem;
using Gear;
using HarmonyLib;
using Player;
using UnityEngine;

#nullable disable
namespace Parry;

[HarmonyPatch]
internal static class Patch
{
    private const float PARRYDURATION = 0.3f;

    private static float shoveTime;
    private static bool parrySuccess = false;

    // Return value is used by the relevant receive damage prefix to determine whether the original receive damage method should run.
    private static bool SuccessfullyParry(pMediumDamageData damageData, bool isTentacle)
    {
        parrySuccess = true;
        PlayerAgent localPlayerAgent = PlayerManager.GetLocalPlayerAgent();

        // Play the parry sound.
        localPlayerAgent.Sound.Post(1256202815, true);

        // Heal the player.
        float damageTaken = damageData.damage.Get(1f);
        localPlayerAgent.GiveHealth(localPlayerAgent, damageTaken * 1f);
        localPlayerAgent.GiveAmmoRel(localPlayerAgent, damageTaken * 0.25f, damageTaken * 0.25f, damageTaken * 0.25f);

        // Parry the attack.
        damageData.source.TryGet(out Agent damagingAgent);
        if (damagingAgent != null && isTentacle)
        {
            // Parried a tentacle, explode the enemy.
            //DamageUtil.DoExplosionDamage(damagingAgent.Position, 2f, 100f, LayerManager.MASK_EXPLOSION_TARGETS, LayerManager.MASK_EXPLOSION_BLOCKERS, true, 1500f);
            DoExplosionDamage(damagingAgent.Position, 2f, 100f, 1500f);
        }
        else
        {
            // Parried a projectile, fire a shot.
            Weapon.WeaponHitData parryShot = new()
            {
                randomSpread = 0,
                maxRayDist = 100f
            };
            Vector3 originPos = localPlayerAgent.FPSCamera.Position;
            parryShot.fireDir = (localPlayerAgent.FPSCamera.CameraRayPos - originPos).normalized;
            parryShot.owner = localPlayerAgent;
            parryShot.damage = 150.1f;
            parryShot.staggerMulti = 1f;
            parryShot.precisionMulti = 1f;
            parryShot.damageFalloff = new Vector2(40, 100);
            if (Weapon.CastWeaponRay(localPlayerAgent.FPSCamera.transform, ref parryShot, originPos))
            {
                BulletWeapon.BulletHit(parryShot, true);
                FX_Manager.EffectTargetPosition = parryShot.rayHit.point;
            }
            else
                FX_Manager.EffectTargetPosition = localPlayerAgent.FPSCamera.CameraRayPos;
            FX_Manager.PlayLocalVersion = false;
            BulletWeapon.s_tracerPool.AquireEffect().Play((FX_Trigger)null, localPlayerAgent.EyePosition, Quaternion.LookRotation(parryShot.fireDir));
        }
        parrySuccess = false;

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

            Rigidbody rigidbodyComponent = tempCollider.GetComponent<Rigidbody>();
            if (rigidbodyComponent)
                rigidbodyComponent.AddExplosionForce(explosionForce, sourcePos, radius, 3f);

            IDamageable iDamageableComponent = tempCollider.GetComponent<IDamageable>();
            if (iDamageableComponent != null)
            {
                IDamageable baseDamagable = iDamageableComponent.GetBaseDamagable();
                Logger.DebugOnly("baseDam: " + baseDamagable + " baseDam.TempSearchID: " + baseDamagable?.TempSearchID + " searchID: " + DamageUtil.SearchID);
                if (baseDamagable != null)
                {
                    if (baseDamagable.TempSearchID != DamageUtil.SearchID)
                    {
                        Vector3 vector3 = iDamageableComponent.DamageTargetPos - sourcePos;
                        if (!Physics.Raycast(sourcePos, vector3.normalized, out RaycastHit hitInfo, vector3.magnitude, LayerManager.MASK_EXPLOSION_BLOCKERS) || hitInfo.collider == tempCollider)
                        {
                            baseDamagable.TempSearchID = DamageUtil.SearchID;
                            iDamageableComponent.ExplosionDamage(damage, sourcePos, Vector3.up * explosionForce);
                        }
                    }
                }
                else
                    Logger.DebugOnly("DoExplosionDamage got IDamageable that has no baseDam..");
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
        if (tookDamageTime > shoveTime && tookDamageTime - shoveTime < PARRYDURATION)
        {
            return SuccessfullyParry(data, false);
        }
        return true;
    }

    [HarmonyPatch(typeof(Dam_PlayerDamageLocal), nameof(Dam_PlayerDamageLocal.ReceiveTentacleAttackDamage))]
    [HarmonyPrefix]
    public static bool ParryTentacleAttackDamage(pMediumDamageData data)
    {
        Logger.DebugOnly("Received tentacle attack damage.");
        float tookDamageTime = Clock.Time;
        if (tookDamageTime > shoveTime && tookDamageTime - shoveTime < PARRYDURATION)
        {
            return SuccessfullyParry(data, true);
        }
        return true;
    }

    [HarmonyPatch(typeof(Dam_PlayerDamageLocal), nameof(Dam_PlayerDamageLocal.ReceiveExplosionDamage))]
    [HarmonyPrefix]
    public static bool IgnoreParryExplosionDamage()
    {
        Logger.DebugOnly("Received explosion damage.");
        if (parrySuccess)
        {
            return false;
        }
        return true;
    }
}
