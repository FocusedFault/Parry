using RoR2;
using EntityStates;

namespace Parry
{
    public class ParryHold : BaseState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            Util.PlaySound("Play_merc_sword_impact", this.gameObject);
            this.PlayCrossfade("FullBody, Override", "GroundLight2", "GroundLight.playbackRate", 99f, 0.05f);
            //Start the parry.
            this.characterBody.AddBuff(Parry.parryBuffDef);
        }


        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (this.isAuthority)
            {
                bool keyReleased = !(this.inputBank && this.inputBank.skill2.down);
                if (keyReleased)
                {
                    this.outer.SetNextState(new ParryStrike());
                }
            }
        }
    }
}