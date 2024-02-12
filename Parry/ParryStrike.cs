using RoR2;
using EntityStates;
using EntityStates.Merc;
using UnityEngine;
using UnityEngine.Networking;

namespace Parry
{
    public class ParryStrike : BaseState
    {
        float totalDuration = 0.1f; //Total duration of state, should be fixed.
        float attackDelay = 0.05f;	//Delay before the attack starts, parry active frames
        public static float iframes = 1f;  //iframes to grant on successful parry
        bool hasFiredServer = false;	//Used to determine whether the attack was fired. If false during OnExit, force fire the attack.
        float blastAttackDamageCoefficient = 5f;    //Damage coefficient for the attack.

        public override void OnEnter()
        {
            base.OnEnter();
            if (NetworkServer.active)
            {
                //Start the parry.
                this.characterBody.AddBuff(Parry.parryActivatedBuffDef);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (NetworkServer.active && this.isAuthority)
            {
                if (!hasFiredServer && base.fixedAge >= attackDelay)
                {
                    DoAttackServer();
                }
            }
            if (base.isAuthority)
            {
                //This will often not line up with the server's timing. Keep that in mind.
                if (base.fixedAge > totalDuration) this.outer.SetNextStateToMain();
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
            base.OnExit();
            this.PlayAnimation("FullBody, Override", "UppercutExit");
        }

        //Reset Buffs
        private void CleanBuffsServer()
        {
            if (!NetworkServer.active) return;
            if (this.characterBody.HasBuff(Parry.parryActivatedBuffDef)) this.characterBody.RemoveBuff(Parry.parryActivatedBuffDef);
            if (this.characterBody.HasBuff(Parry.parryBuffDef)) this.characterBody.RemoveBuff(Parry.parryBuffDef);
        }

        //Since everything about parrying is handled server-side, do this on the server.
        private void DoAttackServer()
        {
            if (!NetworkServer.active) return;
            this.PlayCrossfade("FullBody, Override", nameof(Uppercut), "Uppercut.playbackRate", 1f, 0.1f);
            hasFiredServer = true;
            bool parry = this.characterBody.HasBuff(RoR2Content.Buffs.Immune);

            //Scale attack damage based on whether or not the attack successfully landed.
            EffectManager.SimpleImpactEffect(Evis.hitEffectPrefab, this.characterBody.corePosition, Vector3.one, false);
            EffectManager.SimpleImpactEffect(Evis.hitEffectPrefab, this.characterBody.corePosition, Vector3.zero, false);
            EffectManager.SimpleImpactEffect(Evis.hitEffectPrefab, this.characterBody.corePosition, Vector3.left, false);
            EffectManager.SimpleImpactEffect(Evis.hitEffectPrefab, this.characterBody.corePosition, Vector3.right, false);
            if (parry)
            {
                Util.PlaySound("Play_merc_utility_variant", this.gameObject);
                Util.PlaySound("Play_merc_utility_variant", this.gameObject);
                Util.PlaySound("Play_merc_utility_variant", this.gameObject);
                Util.PlaySound("Play_merc_utility_variant", this.gameObject);
                Util.PlaySound("Play_merc_utility_variant", this.gameObject);
                Util.PlaySound("Play_merc_utility_variant", this.gameObject);
            }
            else
            {
                Util.PlaySound("Play_merc_m2_uppercut", this.gameObject);
                Util.PlaySound(Evis.impactSoundString, this.gameObject);
            }
            new BlastAttack()
            {
                impactEffect = EffectCatalog.FindEffectIndexFromPrefab(Evis.hitEffectPrefab),
                attacker = this.gameObject,
                inflictor = this.gameObject,
                teamIndex = TeamComponent.GetObjectTeam(this.gameObject),
                baseDamage = this.damageStat * (parry ? (this.blastAttackDamageCoefficient * 2) : this.blastAttackDamageCoefficient),
                baseForce = 250,
                position = this.characterBody.corePosition,
                radius = this.characterBody.radius + 13f,
                falloffModel = BlastAttack.FalloffModel.None,
                damageType = parry ? DamageType.ApplyMercExpose : DamageType.Stun1s,
                attackerFiltering = AttackerFiltering.NeverHitSelf
            }.Fire();
            //Once attack has been fired, there is no more need for the Parry buffs.
            CleanBuffsServer();
        }
    }
}