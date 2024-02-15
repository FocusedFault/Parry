using RoR2;
using EntityStates;
using EntityStates.Merc;
using UnityEngine;
using UnityEngine.Networking;
using System;
using RoR2.Projectile;
using System.Collections.Generic;
using HG;

namespace Parry
{
    public class ParryStrike : BaseState
    {
        public static NetworkSoundEventDef parrySoundDef;
        public static NetworkSoundEventDef evisSoundDef;

        public static float totalDuration = 0.35f; //Total duration of state, should be fixed.
        public static float attackDelay = 0.3f;	//Delay before the attack starts, parry active frames
        public static float invulnDuration = 1f;  //iframes to grant on successful parry
        public static float blastAttackDamageCoefficient = 5f;    //Damage coefficient for the attack.
        public static float projectileGrazeRadius = 3f; //Radius that counts as a projectile graze to trigger parry.

        private bool hasFiredServer = false;	//Used to determine whether the attack was fired. If false during OnExit, force fire the attack.
        private bool hasFiredClient = false;    //Used to determine whether clientside visuals have played

        public override void OnEnter()
        {
            base.OnEnter();
            if (NetworkServer.active)
            {
                CleanBuffsServer();
                if (!base.characterBody.HasBuff(Parry.parryBuffDef)) base.characterBody.AddBuff(Parry.parryBuffDef);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (base.fixedAge >= attackDelay)
            {
                if (!hasFiredClient)
                {
                    DoAttackClient();
                }

                if (NetworkServer.active && !hasFiredServer)
                {
                    DoAttackServer();
                }
            }
            else
            {
                CheckProjectileGrazeServer();
            }

            //Keep in mind that FixedAge on client can differ from FixedAge on server.
            if (base.isAuthority)
            {
                if (base.fixedAge >= totalDuration) this.outer.SetNextStateToMain();
            }
        }

        public override void OnExit()
        {
            if (NetworkServer.active)
            {
                //Fire attack if it didn't get fired for whatever reason (ex. Authority ends the state before server can fire off the attack)
                if (!hasFiredServer) DoAttackServer();

                //Reset buffs at the end, no more need for Parry stuff.
                CleanBuffsServer();
            }

            this.PlayAnimation("FullBody, Override", "UppercutExit");
            base.OnExit();
        }

        //Reset Buffs
        private void CleanBuffsServer()
        {
            if (!NetworkServer.active) return;
            if (this.characterBody.HasBuff(Parry.parryActivatedBuffDef)) this.characterBody.RemoveBuff(Parry.parryActivatedBuffDef);
            if (this.characterBody.HasBuff(Parry.parryBuffDef)) this.characterBody.RemoveBuff(Parry.parryBuffDef);
        }

        private void DoAttackClient()
        {
            hasFiredClient = true;
            this.PlayCrossfade("FullBody, Override", nameof(Uppercut), "Uppercut.playbackRate", 1f, totalDuration - attackDelay);
            Util.PlaySound("Play_merc_m2_uppercut", base.gameObject);
        }

        //Since everything about parrying is handled server-side, do this on the server.
        private void DoAttackServer()
        {
            if (!NetworkServer.active || !this.characterBody) return;
            hasFiredServer = true;
            bool parry = this.characterBody.HasBuff(Parry.parryActivatedBuffDef);

            //Always stun to prevent you from getting instakilled once parry ends
            DamageType damageType = DamageType.Stun1s;

            float damageCoefficient = blastAttackDamageCoefficient;

            if (parry)
            {
                if (Parry.parryFunEnabled.Value)
                {
                    DeleteProjectilesServer(this.characterBody.radius + Parry.parryFunRadius.Value);
                    damageCoefficient *= Parry.parryFunDamageMultiplier.Value;
                }
                else
                {
                    DeleteProjectilesServer(this.characterBody.radius + 13f);
                    damageCoefficient *= 3f;
                }
                damageType |= DamageType.ApplyMercExpose;
                if (parrySoundDef) EffectManager.SimpleSoundEffect(parrySoundDef.index, this.characterBody.corePosition, true);
            }

            //Scale attack damage based on whether or not the attack successfully landed.
            EffectManager.SimpleImpactEffect(Evis.hitEffectPrefab, this.characterBody.corePosition, Vector3.one, true);
            EffectManager.SimpleImpactEffect(Evis.hitEffectPrefab, this.characterBody.corePosition, Vector3.zero, true);
            EffectManager.SimpleImpactEffect(Evis.hitEffectPrefab, this.characterBody.corePosition, Vector3.left, true);
            EffectManager.SimpleImpactEffect(Evis.hitEffectPrefab, this.characterBody.corePosition, Vector3.right, true);

            BlastAttack.Result result;
            if (Parry.parryFunEnabled.Value)
            {
                result = new BlastAttack()
                {
                    impactEffect = EffectCatalog.FindEffectIndexFromPrefab(Parry.parryFunImpact),
                    attacker = this.gameObject,
                    inflictor = this.gameObject,
                    teamIndex = TeamComponent.GetObjectTeam(this.gameObject),
                    baseDamage = this.damageStat * damageCoefficient,
                    baseForce = 10000f,
                    position = this.characterBody.corePosition,
                    radius = this.characterBody.radius + Parry.parryFunRadius.Value,
                    falloffModel = BlastAttack.FalloffModel.None,
                    damageType = damageType,
                    attackerFiltering = AttackerFiltering.NeverHitSelf
                }.Fire();
            }
            else
            {
                result = new BlastAttack()
                {
                    impactEffect = EffectCatalog.FindEffectIndexFromPrefab(Evis.hitEffectPrefab),
                    attacker = this.gameObject,
                    inflictor = this.gameObject,
                    teamIndex = TeamComponent.GetObjectTeam(this.gameObject),
                    baseDamage = this.damageStat * damageCoefficient,
                    baseForce = 250,
                    position = this.characterBody.corePosition,
                    radius = this.characterBody.radius + 13f,
                    falloffModel = BlastAttack.FalloffModel.None,
                    damageType = damageType,
                    attackerFiltering = AttackerFiltering.NeverHitSelf
                }.Fire();
            }

            if (result.hitCount > 0)
            {
                if (evisSoundDef) EffectManager.SimpleSoundEffect(evisSoundDef.index, this.characterBody.corePosition, true);
            }


            //Once attack has been fired, there is no more need for the Parry buffs.
            CleanBuffsServer();
        }

        private void CheckProjectileGrazeServer()
        {
            if (!NetworkServer.active || projectileGrazeRadius <= 0f || !this.characterBody || !this.characterBody.HasBuff(Parry.parryBuffDef)) return;

            Collider[] array = Physics.OverlapSphere(this.characterBody.corePosition, ParryStrike.projectileGrazeRadius + this.characterBody.radius, LayerIndex.projectile.mask);
            for (int i = 0; i < array.Length; i++)
            {
                ProjectileController pc = array[i].GetComponentInParent<ProjectileController>();
                if (pc && !pc.cannotBeDeleted && pc.owner != base.gameObject && !(pc.teamFilter && pc.teamFilter.teamIndex == base.GetTeam()))
                {
                    //Prevent stationary grounded "projectiles" from counting
                    bool cannotDelete = false;
                    ProjectileSimple ps = pc.gameObject.GetComponent<ProjectileSimple>();
                    ProjectileCharacterController pcc = pc.gameObject.GetComponent<ProjectileCharacterController>();

                    if ((!ps || (ps && ps.desiredForwardSpeed == 0f)) && !pcc)
                    {
                        cannotDelete = true;
                    }

                    if (!cannotDelete)
                    {
                        Parry.HandleParryBuffsServer(this.characterBody);
                        return;
                    }
                }
            }
        }

        //NetworkServer.active and Characterbody are already checked in DoAttackServer which calls this
        private void DeleteProjectilesServer(float radius)
        {
            List<ProjectileController> projectileControllers = new List<ProjectileController>();

            Collider[] array = Physics.OverlapSphere(this.characterBody.corePosition, radius, LayerIndex.projectile.mask);
            for (int i = 0; i < array.Length; i++)
            {
                ProjectileController pc = array[i].GetComponentInParent<ProjectileController>();
                if (pc && !pc.cannotBeDeleted && pc.owner != base.gameObject && !(pc.teamFilter && pc.teamFilter.teamIndex == base.GetTeam()))
                {
                    //Prevent stationary grounded "projectiles" from being deleted
                    bool cannotDelete = false;
                    ProjectileSimple ps = pc.gameObject.GetComponent<ProjectileSimple>();
                    ProjectileCharacterController pcc = pc.gameObject.GetComponent<ProjectileCharacterController>();

                    if ((!ps || (ps && ps.desiredForwardSpeed == 0f)) && !pcc)
                    {
                        cannotDelete = true;
                    }

                    if (!cannotDelete && !projectileControllers.Contains(pc))
                    {
                        projectileControllers.Add(pc);
                    }
                }
            }

            int projectilesDeleted = projectileControllers.Count;
            for (int i = 0; i < projectilesDeleted; i++)
            {
                GameObject toDelete = projectileControllers[i].gameObject;
                if (toDelete)
                {
                    EntityState.Destroy(toDelete);
                }
            }
        }
    }
}