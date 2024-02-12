using System.Collections;
using BepInEx;
using R2API;
using RoR2;
using RoR2.Skills;
using EntityStates;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Parry
{
  [BepInPlugin("com.Nuxlar.Parry", "Parry", "1.1.0")]

  public class Parry : BaseUnityPlugin
  {
    private AssetBundle parryAssets;
    private Sprite parryIcon;
    private GameObject merc = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Merc/MercBody.prefab").WaitForCompletion();
    public static GameObject parryImpact = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Merc/ImpactMercFocusedAssault.prefab").WaitForCompletion();
    private SkillDef parrySkillDef = ScriptableObject.CreateInstance<SkillDef>();

    private static BodyIndex mercBodyIndex;


    public void Awake()
    {

      parryAssets = AssetBundle.LoadFromFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(this.Info.Location), "parrybundle.bundle"));
      parryIcon = parryAssets.LoadAsset<Sprite>("Assets/parryIconNux.png");
      ContentAddition.AddEntityState<FocusedStrike>(out _);
      CreateParrySkill();
      On.RoR2.HealthComponent.TakeDamage += AddParryDelay;

      RoR2Application.onLoad += OnLoad;
    }

    private void OnLoad()
    {
      mercBodyIndex = BodyCatalog.FindBodyIndex("MercBody");
    }

    public void AddParryDelay(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
    {
      if (self.body.bodyIndex == mercBodyIndex && self.body.inputBank.skill2.down && self.body.GetComponent<EntityStateMachine>().state.GetType() == typeof(FocusedStrike))
      {
        EffectManager.SimpleImpactEffect(HealthComponent.AssetReferences.executeEffectPrefab, damageInfo.position, -damageInfo.force, true);
        StartCoroutine(ParryDelay(orig, self, damageInfo));
      }
      else
        orig(self, damageInfo);
    }

    private IEnumerator ParryDelay(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
    {
      float elapsedTime = 0f;
      while (elapsedTime < 0.75f)
      {
        if (self.body.inputBank.skill2.justReleased)
        {
          damageInfo.rejected = true;
          if (!self.body.HasBuff(RoR2Content.Buffs.Immune))
            self.body.AddTimedBuff(RoR2Content.Buffs.Immune, 1f);
          break; // Exit the loop if condition is met
        }

        elapsedTime += Time.deltaTime;
        yield return null; // Yield null to wait for next frame
      }

      orig(self, damageInfo);
    }

    private void CreateParrySkill()
    {
      parrySkillDef.skillName = "FocusedStrike";
      (parrySkillDef as ScriptableObject).name = "FocusedStrike";
      parrySkillDef.skillNameToken = "Focused Strike";
      parrySkillDef.skillDescriptionToken = "Ready your blade, release before an incoming strike to <style=cIsUtility>parry</style> enemy attacks for <style=cIsDamage>500%-1000% damage to all nearby enemies.</style>";
      parrySkillDef.icon = parryIcon;

      parrySkillDef.activationState = new SerializableEntityStateType(typeof(FocusedStrike));
      parrySkillDef.activationStateMachineName = "Body";
      parrySkillDef.interruptPriority = InterruptPriority.PrioritySkill;

      parrySkillDef.baseMaxStock = 1;
      parrySkillDef.baseRechargeInterval = 5f;

      parrySkillDef.rechargeStock = 1;
      parrySkillDef.requiredStock = 1;
      parrySkillDef.stockToConsume = 1;

      parrySkillDef.dontAllowPastMaxStocks = false;
      parrySkillDef.beginSkillCooldownOnSkillEnd = false;
      parrySkillDef.canceledFromSprinting = false;
      parrySkillDef.forceSprintDuringState = false;
      parrySkillDef.fullRestockOnAssign = true;
      parrySkillDef.resetCooldownTimerOnUse = false;
      parrySkillDef.isCombatSkill = true;
      parrySkillDef.mustKeyPress = false;
      parrySkillDef.cancelSprintingOnActivation = true;

      ContentAddition.AddSkillDef(parrySkillDef);

      SkillFamily skillFamily = merc.GetComponent<SkillLocator>().secondary.skillFamily;
      Array.Resize(ref skillFamily.variants, skillFamily.variants.Length + 1);
      skillFamily.variants[skillFamily.variants.Length - 1] = new SkillFamily.Variant()
      {
        skillDef = parrySkillDef,
        viewableNode = new ViewablesCatalog.Node(parrySkillDef.skillNameToken, false)
      };
    }
  }
}