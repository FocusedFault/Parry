using RoR2;
using EntityStates;
using EntityStates.Merc;
using UnityEngine;

namespace Parry
{
    public class FocusedStrike : BaseState
    {
        private float blastAttackDamageCoefficient = 5f;
        private bool hasReleased = false;
        private float delayStopwatch = 0f;

        public override void OnEnter()
        {
            base.OnEnter();
            Util.PlaySound("Play_merc_sword_impact", this.gameObject);
            this.PlayCrossfade("FullBody, Override", "GroundLight2", "GroundLight.playbackRate", 99f, 0.05f);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (this.inputBank.skill2.justReleased)
            {
                hasReleased = true;
            }
            if (hasReleased)    //State transitions need to be locked behind authority.
            {
                delayStopwatch += Time.fixedDeltaTime;
                if (delayStopwatch > 0.1f)
                {
                    this.PlayCrossfade("FullBody, Override", nameof(Uppercut), "Uppercut.playbackRate", 1f, 0.1f);
                    if (this.characterBody.HasBuff(RoR2Content.Buffs.Immune))
                        CounterAttack(true);
                    else
                        CounterAttack(false);

                    this.outer.SetNextStateToMain();
                }
            }
        }

        private void CounterAttack(bool parry)
        {
            EffectManager.SimpleImpactEffect(Evis.hitEffectPrefab, this.gameObject.transform.position, Vector3.one, false);
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
        }

        public override void OnExit()
        {
            base.OnExit();
            this.PlayAnimation("FullBody, Override", "UppercutExit");
        }
    }
}