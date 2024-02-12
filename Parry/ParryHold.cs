using RoR2;
using EntityStates;

namespace Parry
{
    public class ParryHold : BaseState
    {
        public static float baseMinDuration = 0f;   //Set this if you want to require a bit of a delay before you can release like in RoRR. Scales with attack speed.
        public static string readySoundString = "Play_merc_sword_impact"; //Plays once minDuration has been crossed

        private bool playedSound;
        private float minDuration;

        public override void OnEnter()
        {
            base.OnEnter();

            playedSound = false;
            minDuration = baseMinDuration / this.attackSpeedStat;

            this.PlayCrossfade("FullBody, Override", "GroundLight2", "GroundLight.playbackRate", 99f, 0.05f);
        }


        public override void FixedUpdate()
        {
            base.FixedUpdate();

            bool minDurationPassed = base.fixedAge >= minDuration;
            if (minDurationPassed && !playedSound)
            {
                playedSound = true;
                Util.PlaySound(readySoundString, this.gameObject);
            }

            if (this.isAuthority)
            {
                bool keyReleased = !(this.inputBank && this.inputBank.skill2.down);
                if (keyReleased && minDurationPassed)
                {
                    this.outer.SetNextState(new ParryStrike());
                }
            }
        }
    }
}