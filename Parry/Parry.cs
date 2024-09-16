using BepInEx;
using BepInEx.Configuration;
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
  [BepInPlugin("com.Nuxlar.Parry", "Parry", "1.3.1")]

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
    public static GameObject parryFunImpact = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Captain/CaptainAirstrikeAltImpact.prefab").WaitForCompletion();

    public static SkillDef parrySkillDef = ScriptableObject.CreateInstance<SkillDef>();

    private static BodyIndex mercBodyIndex;
    public static ConfigEntry<float> parryFunDamageMultiplier;
    public static ConfigEntry<float> parryFunRadius;
    public static ConfigEntry<bool> parryFunEnabled;

    private static ConfigFile ParryConfig { get; set; }

    public void Awake()
    {
      ParryConfig = new ConfigFile(Paths.ConfigPath + "\\com.Nuxlar.Parry.cfg", true);
      parryFunDamageMultiplier = ParryConfig.Bind<float>("General", "Fun Mode Damage Multiplier", 20f, "This number is multiplied by the base 500% damage, so 20 would be 10000% damage.");
      parryFunRadius = ParryConfig.Bind<float>("General", "Fun Mode Damage Radius", 26f, "How large the retalitory strike radius is in fun mode.");
      parryFunEnabled = ParryConfig.Bind<bool>("General", "Enable Fun Mode", false, "Parry but fun.");

      parryAssets = AssetBundle.LoadFromFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(this.Info.Location), "parrybundle.bundle"));
      parryIcon = parryAssets.LoadAsset<Sprite>("Assets/parryIconNux.png");
      parryBuffIcon = parryAssets.LoadAsset<Sprite>("Assets/parryBuffIconNux.png");
      parryActivatedBuffIcon = parryAssets.LoadAsset<Sprite>("Assets/parryActivatedBuffIconNux.png");
      ContentAddition.AddEntityState<ParryHold>(out _);
      ContentAddition.AddEntityState<ParryStrike>(out _);
      CreateParryBuffs();
      CreateParrySkill();

      ParryStrike.parrySoundDef = CreateNetworkSoundEventDef("Play_nux_parry");
      ParryStrike.evisSoundDef = CreateNetworkSoundEventDef("Play_merc_sword_impact");

      On.RoR2.HealthComponent.TakeDamage += TakeDamageHook;

      RoR2Application.onLoad += OnLoad;
    }

    private void OnLoad()
    {
      mercBodyIndex = BodyCatalog.FindBodyIndex("MercBody");
    }

    private void TakeDamageHook(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
    {
      if (NetworkServer.active && self.body.bodyIndex == mercBodyIndex && self.body.HasBuff(parryBuffDef) && damageInfo.damage > 0f)
      {
        HandleParryBuffsServer(self.body);
        return;
      }

      orig(self, damageInfo);
    }

    public static void HandleParryBuffsServer(CharacterBody body)
    {
      if (body.HasBuff(parryBuffDef)) body.RemoveBuff(parryBuffDef);
      if (!body.HasBuff(parryActivatedBuffDef)) body.AddBuff(parryActivatedBuffDef);

      body.AddTimedBuff(RoR2Content.Buffs.Immune, ParryStrike.invulnDuration);
      return;
    }


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
      parrySkillDef.skillDescriptionToken = "Ready your blade, release before an incoming strike to <style=cIsUtility>parry</style> enemy attacks for <style=cIsDamage>500%-1500% damage to all nearby enemies.</style>";
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

    public static NetworkSoundEventDef CreateNetworkSoundEventDef(string eventName)
    {
      NetworkSoundEventDef networkSoundEventDef = ScriptableObject.CreateInstance<NetworkSoundEventDef>();
      networkSoundEventDef.akId = AkSoundEngine.GetIDFromString(eventName);
      networkSoundEventDef.eventName = eventName;

      ContentAddition.AddNetworkSoundEventDef(networkSoundEventDef);

      return networkSoundEventDef;
    }
  }
}