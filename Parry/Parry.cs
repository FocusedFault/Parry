using System.Collections;
using BepInEx;
using R2API;
using RoR2;
using RoR2.Skills;
using EntityStates;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace Parry
{
  [BepInPlugin("com.Nuxlar.Parry", "Parry", "1.1.0")]

  public class Parry : BaseUnityPlugin
  {
    private AssetBundle parryAssets;
    private Sprite parryIcon;
    private Sprite parryBuffIcon;
    private Sprite parryActivatedBuffIcon;
    public static BuffDef parryBuffDef;
    public static BuffDef parryActivatedBuffDef;
    private GameObject merc = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Merc/MercBody.prefab").WaitForCompletion();
    public static GameObject parryImpact = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Merc/ImpactMercFocusedAssault.prefab").WaitForCompletion();

    public static SkillDef parrySkillDef = ScriptableObject.CreateInstance<SkillDef>();

    private static BodyIndex mercBodyIndex;


    public void Awake()
    {
      parryAssets = AssetBundle.LoadFromFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(this.Info.Location), "parrybundle.bundle"));
      parryIcon = parryAssets.LoadAsset<Sprite>("Assets/parryIconNux.png");
      parryBuffIcon = parryAssets.LoadAsset<Sprite>("Assets/parryBuffIconNux.png");
      parryActivatedBuffIcon = parryAssets.LoadAsset<Sprite>("Assets/parryActivatedBuffIconNux.png");
      ContentAddition.AddEntityState<ParryHold>(out _);
      ContentAddition.AddEntityState<ParryStrike>(out _);
      CreateParryBuffs();
      CreateParrySkill();
      On.RoR2.HealthComponent.TakeDamage += TakeDamageHook;

      RoR2Application.onLoad += OnLoad;
    }

    private void OnLoad()
    {
      mercBodyIndex = BodyCatalog.FindBodyIndex("MercBody");
    }

    private void TakeDamageHook(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
    {
        if (NetworkServer.active && self.body && self.body.bodyIndex == mercBodyIndex && self.body.HasBuff(parryBuffDef))
        {
            self.body.RemoveBuff(parryBuffDef);
            if (!self.body.HasBuff(parryActivatedBuffDef)) self.body.AddBuff(parryActivatedBuffDef);

            self.body.AddTimedBuff(RoR2Content.Buffs.Immune, ParryStrike.invulnDuration);
            EffectManager.SimpleImpactEffect(HealthComponent.AssetReferences.executeEffectPrefab, damageInfo.position, -damageInfo.force, true);
        }

        orig(self, damageInfo);
    }

    /*private IEnumerator ParryDelay(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
    {
      float elapsedTime = 0f;
      while (elapsedTime < 0.75f)
      {
        if (self.body.HasBuff(parryActivatedBuffDef))
        {
          damageInfo.rejected = true;
          if (!self.body.HasBuff(RoR2Content.Buffs.Immune))
            self.body.AddTimedBuff(RoR2Content.Buffs.Immune, ParryStrike.invulnDuration);
          break; // Exit the loop if condition is met
        }

        elapsedTime += Time.deltaTime;
        yield return null; // Yield null to wait for next frame
      }

      orig(self, damageInfo);
    }*/

    private void CreateParryBuffs()
    {
      parryBuffDef = ScriptableObject.CreateInstance<BuffDef>();
      parryBuffDef.name = "ParryBuffNux";
      parryBuffDef.canStack = false;
      parryBuffDef.isCooldown = false;
      parryBuffDef.isDebuff = false;
      parryBuffDef.buffColor = Color.cyan;
      parryBuffDef.iconSprite = parryBuffIcon;
      (parryBuffDef as UnityEngine.Object).name = parryBuffDef.name;

      parryActivatedBuffDef = ScriptableObject.CreateInstance<BuffDef>();
      parryActivatedBuffDef.name = "ParryActivatedBuffNux";
      parryActivatedBuffDef.canStack = false;
      parryActivatedBuffDef.isCooldown = false;
      parryActivatedBuffDef.isDebuff = false;
      parryActivatedBuffDef.buffColor = Color.cyan;
      parryActivatedBuffDef.iconSprite = parryActivatedBuffIcon;
      (parryActivatedBuffDef as UnityEngine.Object).name = parryActivatedBuffDef.name;

      ContentAddition.AddBuffDef(parryBuffDef);
      ContentAddition.AddBuffDef(parryActivatedBuffDef);
    }

    private void CreateParrySkill()
    {
      parrySkillDef.skillName = "FocusedStrike";
      (parrySkillDef as ScriptableObject).name = "FocusedStrike";
      parrySkillDef.skillNameToken = "Focused Strike";
      parrySkillDef.skillDescriptionToken = "Ready your blade, release before an incoming strike to <style=cIsUtility>parry</style> enemy attacks for <style=cIsDamage>500%-1000% damage to all nearby enemies.</style>";
      parrySkillDef.icon = parryIcon;

      parrySkillDef.activationState = new SerializableEntityStateType(typeof(ParryHold));
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