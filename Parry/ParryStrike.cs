using RoR2;
using EntityStates;
using EntityStates.Merc;
using UnityEngine;
using UnityEngine.Networking;

namespace Parry
{
    public class ParryStrike : BaseState
    {
        public static string enterSoundString = "";

        public static NetworkSoundEventDef parrySoundDef = Parry.networkSoundEventDef;

        public static float totalDuration = 0.35f; //Total duration of state, should be fixed.
        public static float attackDelay = 0.3f;	//Delay before the attack starts, parry active frames
        public static float invulnDuration = 1f;  //iframes to grant on successful parry
        public static float blastAttackDamageCoefficient = 5f;    //Damage coefficient for the attack.

        private bool hasFiredServer = false;	//Used to determine whether the attack was fired. If false during OnExit, force fire the attack.

        public override void OnEnter()
        {
            base.OnEnter();
            Util.PlaySound(enterSoundString, this.gameObject);
            if (NetworkServer.active)
            {
                CleanBuffsServer();
                if (!base.characterBody.HasBuff(Parry.parryBuffDef)) base.characterBody.AddBuff(Parry.parryBuffDef);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (NetworkServer.active)
            {
                if (!hasFiredServer && base.fixedAge >= attackDelay)
                {
                    DoAttackServer();
                }
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

        //Since everything about parrying is handled server-side, do this on the server.
        private void DoAttackServer()
        {
            if (!NetworkServer.active) return;
            this.PlayCrossfade("FullBody, Override", nameof(Uppercut), "Uppercut.playbackRate", 1f, 0.1f);
            hasFiredServer = true;
            bool parry = this.characterBody.HasBuff(Parry.parryActivatedBuffDef);

            //Always stun to prevent you from getting instakilled once parry ends
            DamageType damageType = DamageType.Stun1s;

            float damageCoefficient = blastAttackDamageCoefficient;

            if (parry)
            {
                damageCoefficient *= 3f;
                damageType |= DamageType.ApplyMercExpose;
                if (parrySoundDef) EffectManager.SimpleSoundEffect(parrySoundDef.index, this.characterBody.corePosition, true);
            }
            else
            {
                Util.PlaySound("Play_merc_m2_uppercut", this.gameObject);
                Util.PlaySound(Evis.impactSoundString, this.gameObject);
            }

            //Scale attack damage based on whether or not the attack successfully landed.
            EffectManager.SimpleImpactEffect(Evis.hitEffectPrefab, this.characterBody.corePosition, Vector3.one, false);
            EffectManager.SimpleImpactEffect(Evis.hitEffectPrefab, this.characterBody.corePosition, Vector3.zero, false);
            EffectManager.SimpleImpactEffect(Evis.hitEffectPrefab, this.characterBody.corePosition, Vector3.left, false);
            EffectManager.SimpleImpactEffect(Evis.hitEffectPrefab, this.characterBody.corePosition, Vector3.right, false);

            new BlastAttack()
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
            //Once attack has been fired, there is no more need for the Parry buffs.
            CleanBuffsServer();
        }
    }
}