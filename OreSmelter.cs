using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class OreSmelter : MachineEntity, OreSmelterInterface, PowerConsumerInterface
{
    public enum eState
    {
        eObtainingMats,
        eWaitingOnMatTrigger,
        eHeating,
        eSmelting
    }

    public const int OBTAINING_MATS_RETRY_COUNT = 10;

    public const int IDLE_UPDATE_COUNT = 10;

    public const float FEED_ORE_COOLDOWN = 0.05f;

    public float mrMaxPower = 128f;

    public bool mbAllowT4;

    public int mnCollectionRate = 1;

    public float mrBurnRate = 1f;

    public float mrPowerRate = 1f;

    public float mrCurrentPower;

    public float mrRemainingCapacity;

    public float mrSparePowerCapacity;

    public float mrNormalisedPower;

    public int mnOrePerBar = 16;

    public ushort mOreType;

    public int mnOreCount;

    public ItemBase mOutputHopper;

    private int mnIdleCount;

    private bool mbSFXQueued;

    public float mrWorkTime;

    public float mrIdleTime;

    public OreSmelter.eState meState;

    public float mrTemperature;

    public float mrTargetTemp;

    public float mrTempGainRate = 25f;

    public float mrSmeltTime = 15f;

    public float mrRemainingSmeltTime;

    private bool mbLinkedToGO;

    private ParticleSystem CompletionParticles;

    private ParticleSystem HeatParticles;

    private float mrTimeSinceSmelt;

    private GameObject TextQuad;

    private GameObject GlowObject;

    private GameObject SmelterObj;

    private Light GlowLight;

    private TextMesh mReadout;

    private int mnUnityUpdates;

    public bool mbTooDeep;

    public Vector3 mForwards;

    public Vector3 mRight;

    private ushort[] maSourceMaterialCubeTypes;

    private int[] maSourceMaterialCounts;

    private bool mbIsBehindPlayer;

    private bool mbCloseToPlayer = true;

    private GameObject Hopper1;

    private GameObject Hopper2;

    private bool HopperLeftDetected;
    private bool HopperRightDetected;

    private eHopperPermissions mLeftPermissions;

    private eHopperPermissions mRightPermissions;

    public bool IsStartHopper;

    public static bool SmelterIsAttachedToAddRemoveHopper;

    private bool mbSmelterStuck;

    public string SmelterError = string.Empty;

    private float mrReadoutTick;

    private int mnObtainingFails;

    public float mrAveragePPS;

    public ushort RESINEDTINORE = ModManager.mModMappings.CubesByKey["Nedriths.RefinedTinOre"].CubeType;
    public OreSmelter(Segment segment, long x, long y, long z, ushort cube, byte flags, ushort lValue) : base(eSegmentEntity.OreSmelter, SpawnableObjectEnum.OreSmelter, x, y, z, cube, flags, lValue, Vector3.zero, segment)
    {
        this.mValue = lValue;
        this.mbNeedsLowFrequencyUpdate = true;
        this.mbNeedsUnityUpdate = true;
        this.meState = OreSmelter.eState.eWaitingOnMatTrigger;
        this.mOutputHopper = null;
        this.mOreType = 0;
        this.mnOreCount = 0;
        this.mrTimeSinceSmelt = 99.9f;
        this.mForwards = SegmentCustomRenderer.GetRotationQuaternion(flags) * Vector3.forward;
        this.mForwards.Normalize();
        this.mRight = SegmentCustomRenderer.GetRotationQuaternion(this.mFlags) * Vector3.right;
        this.mRight.Normalize();
        this.mnOrePerBar = (int)Mathf.Ceil(16f * DifficultySettings.mrResourcesFactor);
        if (DifficultySettings.mbCasualResource)
        {
            this.mrSmeltTime = 10f;
        }
        if (DifficultySettings.mbRushMode)
        {
            this.mrSmeltTime = 3f;
        }
        if (this.mValue == 1)
        {
            this.mrSmeltTime /= 2f;
            this.mrPowerRate *= 2f;
            this.mnOrePerBar *= 4;
        }
        List<CraftData> recipesForSet = CraftData.GetRecipesForSet("Smelter");
        this.maSourceMaterialCubeTypes = new ushort[recipesForSet.Count];
        this.maSourceMaterialCounts = new int[recipesForSet.Count];
        for (int i = 0; i < recipesForSet.Count; i++)
        {
            if (recipesForSet[i].Costs.Count > 0)
            {
                this.maSourceMaterialCubeTypes[i] = recipesForSet[i].Costs[0].CubeType;
            }
            else
            {
                this.maSourceMaterialCubeTypes[i] = 0;
            }
            this.maSourceMaterialCounts[i] = 0;
        }
        if (y - 4611686017890516992L < -40L)
        {
            this.mbTooDeep = true;
            this.mrSmeltTime = 1024f;
        }
        else
        {
            this.mbTooDeep = false;
        }
    }

    public override void OnUpdateRotation(byte newFlags)
    {
        base.OnUpdateRotation(newFlags);
        this.mFlags = newFlags;
        this.mForwards = SegmentCustomRenderer.GetRotationQuaternion(this.mFlags) * Vector3.forward;
        this.mForwards.Normalize();
        this.mRight = SegmentCustomRenderer.GetRotationQuaternion(this.mFlags) * Vector3.right;
        this.mRight.Normalize();
    }

    public override void OnDelete()
    {
        if (!WorldScript.mbIsServer)
        {
            return;
        }
        if (this.mnOreCount > 0 && this.mOreType != 0)
        {
            ItemManager.DropNewCubeStack(this.mOreType, global::TerrainData.GetDefaultValue(this.mOreType), this.mnOreCount, this.mnX, this.mnY, this.mnZ, Vector3.zero);
        }
        if (this.mOutputHopper != null)
        {
            ItemStack itemStack = this.mOutputHopper as ItemStack;
            if (itemStack.mnAmount > 0)
            {
                ItemManager.instance.DropItem(itemStack, this.mnX, this.mnY, this.mnZ, Vector3.zero);
                this.mOutputHopper = null;
            }
        }
    }

    public override void SpawnGameObject()
    {
        if (this.mValue == 0)
        {
            this.mObjectType = SpawnableObjectEnum.OreSmelter;
        }
        if (this.mValue == 1)
        {
            this.mObjectType = SpawnableObjectEnum.OreSmelterBasic;
        }
        base.SpawnGameObject();
    }

    public override void DropGameObject()
    {
        base.DropGameObject();
        this.mbLinkedToGO = false;
    }

    public override void UnitySuspended()
    {
        this.CompletionParticles = null;
        this.GlowObject = null;
        this.GlowLight = null;
        this.mReadout = null;
        this.TextQuad = null;
        this.HeatParticles = null;
    }

    private void ConfigLOD()
    {
        if (this.mDotWithPlayerForwards < 0f && this.mDistanceToPlayer > 4f)
        {
            if (!this.mbIsBehindPlayer)
            {
                this.mbIsBehindPlayer = true;
                this.TextQuad.SetActive(false);
                this.mReadout.gameObject.SetActive(false);
            }
        }
        else if (this.mbIsBehindPlayer)
        {
            this.mbIsBehindPlayer = false;
            if (this.mbCloseToPlayer)
            {
                this.TextQuad.SetActive(true);
                this.mReadout.gameObject.SetActive(true);
            }
        }
        if (this.mDistanceToPlayer < 16f)
        {
            if (!this.mbCloseToPlayer)
            {
                this.mbCloseToPlayer = true;
                if (!this.mbIsBehindPlayer)
                {
                    this.TextQuad.SetActive(true);
                    this.mReadout.gameObject.SetActive(true);
                }
            }
        }
        else if (this.mbCloseToPlayer)
        {
            this.mbCloseToPlayer = false;
            this.TextQuad.SetActive(false);
            this.mReadout.gameObject.SetActive(false);
        }
    }

    public override void UnityUpdate()
    {
        if (!this.mbLinkedToGO)
        {
            if (this.mWrapper == null || !this.mWrapper.mbHasGameObject)
            {
                if (WorldScript.meGameMode == eGameMode.eSurvival && WorldScript.mbIsServer && SurvivalPlayerScript.meTutorialState <= SurvivalPlayerScript.eTutorialState.PutFuelInCPH)
                {
                    this.mSegment.RequestRegenerateGraphics();
                }
                return;
            }
            if (this.mWrapper.mGameObjectList == null)
            {
                Debug.LogError("Smelter missing game object #0?");
            }
            if (this.mWrapper.mGameObjectList[0].gameObject == null)
            {
                Debug.LogError("Smelter missing game object #0 (GO)?");
            }
            this.mReadout = this.mWrapper.mGameObjectList[0].transform.Search("ReadoutText").GetComponent<TextMesh>();
            this.mbLinkedToGO = true;
            this.CompletionParticles = this.mWrapper.mGameObjectList[0].transform.Search("SmeltCompleteParticles").GetComponent<ParticleSystem>();
            this.HeatParticles = this.mWrapper.mGameObjectList[0].transform.Search("HeatParticles").GetComponent<ParticleSystem>();
            this.GlowObject = this.mWrapper.mGameObjectList[0].transform.Search("SmelterGlow").gameObject;
            this.GlowLight = this.mWrapper.mGameObjectList[0].transform.Search("Smelting Light").gameObject.GetComponent<Light>();
            this.SmelterObj = this.mWrapper.mGameObjectList[0].transform.Search("Smelter").gameObject;
            this.TextQuad = this.mWrapper.mGameObjectList[0].transform.Search("Quad").gameObject;
            if (this.mValue == 0)
            {
                this.Hopper1 = this.mWrapper.mGameObjectList[0].transform.Search("Hopper_Tut Left").gameObject;
                this.Hopper2 = this.mWrapper.mGameObjectList[0].transform.Search("Hopper_Tut Right").gameObject;
                this.Hopper1.SetActive(false);
                this.Hopper2.SetActive(false);
            }
        }
        if (this.mValue == 0)
        {
            if (!DifficultySettings.mbEasyResources)
            {
                this.IsStartHopper = false;
            }
            if (MobSpawnManager.mrSmoothedBaseThreat > 0.5f)
            {
                this.IsStartHopper = false;
            }
            if (this.IsStartHopper)
            {
                if (SurvivalPlayerScript.meTutorialState != SurvivalPlayerScript.eTutorialState.NowFuckOff)
                {
                    this.Hopper1.SetActive(false);
                    this.Hopper2.SetActive(false);
                }
                else if (CentralPowerHub.TutorialActive)
                {
                    this.Hopper1.SetActive(false);
                    this.Hopper2.SetActive(false);
                }
                else if (GameManager.mrTotalTimeSimulated < 20f)
                {
                    this.Hopper1.SetActive(false);
                    this.Hopper2.SetActive(false);
                }
                else
                {
                    if (!this.HopperLeftDetected || !this.HopperRightDetected)
                    {
                        Mission mission = MissionManager.instance.AddMission("Place Hoppers next to Ore Smelter.", 5f, Mission.ePriority.eImportant, false, false, false);
                        if (mission != null)
                        {
                            mission.mbRewardWhenComplete = true;
                            mission.ShowPopup = true;
                            mission.PopUpString = "Automation is going to be the key to success. Your Smelter can interface with adjacent Hoppers of any type. \nHowever, if you're not specific with your instructions, then all sorts of bad things could happen! Set the permissions now, to have one Add-Only hopper and one Remove-Only hopper. Put Ore into the Remove-Only Hopper and Ingots will be placed into the Add-Only Hopper.";
                            mission.PopUpSprite = "StorageHopper";
                            mission.PopUpTitle = "Smelting Automation";
                        }
                    }
                    else
                    {
                        if (this.mLeftPermissions != eHopperPermissions.AddOnly && this.mLeftPermissions != eHopperPermissions.RemoveOnly && this.mnUnityUpdates % 90 == 0)
                        {
                            SurvivalParticleManager.instance.EHint.transform.position = this.Hopper1.transform.position + Vector3.up;
                            SurvivalParticleManager.instance.EHint.transform.forward = this.Hopper1.transform.forward;
                            SurvivalParticleManager.instance.EHint.Emit(1);
                        }
                        if (this.mRightPermissions != eHopperPermissions.AddOnly && this.mRightPermissions != eHopperPermissions.RemoveOnly && this.mnUnityUpdates % 90 == 0)
                        {
                            SurvivalParticleManager.instance.EHint.transform.position = this.Hopper2.transform.position + Vector3.up;
                            SurvivalParticleManager.instance.EHint.transform.forward = this.Hopper2.transform.forward;
                            SurvivalParticleManager.instance.EHint.Emit(1);
                        }
                        if (this.mLeftPermissions != eHopperPermissions.AddOnly && this.mRightPermissions != eHopperPermissions.AddOnly)
                        {
                            Mission mission2 = MissionManager.instance.AddMission("Set Smelter Hopper to Add-Only.", 5f, Mission.ePriority.eImportant, false, false, false);
                        }
                        if (this.mLeftPermissions != eHopperPermissions.RemoveOnly && this.mRightPermissions != eHopperPermissions.RemoveOnly)
                        {
                            Mission mission3 = MissionManager.instance.AddMission("Set Smelter Hopper to Remove-Only", 5f, Mission.ePriority.eImportant, false, false, false);
                        }
                    }
                    this.Hopper1.SetActive(!this.HopperLeftDetected);
                    this.Hopper2.SetActive(!this.HopperRightDetected);
                }
            }
            else
            {
                this.Hopper1.SetActive(false);
                this.Hopper2.SetActive(false);
            }
        }
        if (this.meState == OreSmelter.eState.eSmelting || this.meState == OreSmelter.eState.eHeating)
        {
            if (this.mrCurrentPower > 0f)
            {
                this.mrWorkTime += Time.deltaTime;
            }
            else
            {
                this.mrIdleTime += Time.deltaTime;
            }
        }
        else
        {
            this.mrIdleTime += Time.deltaTime;
        }
        if (this.mnUnityUpdates % 60 == 0)
        {
            this.ConfigLOD();
            if (this.mbSFXQueued)
            {
                if (this.mDistanceToPlayer < 32f)
                {
                    AudioSoundEffectManager.instance.PlayPositionEffect(AudioSoundEffectManager.instance.SmelterComplete, this.mWrapper.mGameObjectList[0].transform.position, 1f, 8f);
                }
                this.mbSFXQueued = false;
            }
            

            if (this.mDotWithPlayerForwards > 0f)
            {
                if (this.meState == OreSmelter.eState.eWaitingOnMatTrigger)
                {
                    this.mReadout.text = "Insert Ore to start";
                }
                if (this.meState == OreSmelter.eState.eObtainingMats)
                {
                    this.mReadout.text = "Needs more Ore!";
                }
                if (this.meState == OreSmelter.eState.eSmelting)
                {
                    this.mReadout.text = "Smelting...";
                }
                if (this.meState == OreSmelter.eState.eHeating)
                {
                    this.mReadout.text = "Heating...";
                }
                if (this.mbSmelterStuck)
                {
                    this.mReadout.text = "Please empty manually!";
                    this.mbSmelterStuck = false;
                }
                if (!string.IsNullOrEmpty(this.SmelterError))
                {
                    TextMesh expr_6B5 = this.mReadout;
                    expr_6B5.text = expr_6B5.text + "\n" + this.SmelterError;
                }
                TextMesh expr_6D6 = this.mReadout;
                expr_6D6.text = expr_6D6.text + "\n" + this.mrTemperature.ToString("F0") + "°C";
                if (this.mOutputHopper != null && this.mOutputHopper.mnItemID != -1 && ItemEntry.mEntries[this.mOutputHopper.mnItemID] != null)
                {
                    TextMesh expr_738 = this.mReadout;
                    string text = expr_738.text;
                    expr_738.text = string.Concat(new object[]
                    {
                        text,
                        "\n",
                        (this.mOutputHopper as ItemStack).mnAmount,
                        " ",
                        ItemEntry.mEntries[this.mOutputHopper.mnItemID].Plural
                    });
                }
            }
        }
        if (this.mrTimeSinceSmelt < 1f)
        {
            if (this.mrTimeSinceSmelt == 0f)
            {
            }
            if (this.mDotWithPlayerForwards > 0f && this.mDistanceToPlayer < 32f)
            {
                this.CompletionParticles.emissionRate = 30f;
            }
            this.mrTimeSinceSmelt += Time.deltaTime;
        }
        else
        {
            this.CompletionParticles.emissionRate = 0f;
        }
        if (!this.mbIsBehindPlayer)
        {
            this.GlowObject.GetComponent<Renderer>().enabled = true;
            if (this.mrTemperature > 5f)
            {
                this.GlowObject.SetActive(true);
                this.GlowLight.enabled = true;
                float num = this.mrTemperature / 550f;
                if (num > 1f)
                {
                    num = 1f;
                }
                float num2 = this.mrTemperature / 1250f;
                if (num2 > 0.8f)
                {
                    num2 = 0.8f;
                }
                this.GlowLight.color = new Color(num, num2, 0.1f, 1f);
                if (this.mrTargetTemp == 0f)
                {
                    this.HeatParticles.emissionRate *= 0.95f;
                }
                else
                {
                    this.HeatParticles.emissionRate = this.mrTemperature / this.mrTargetTemp * 250f / 2f;
                }
            }
            else
            {
                this.HeatParticles.emissionRate = 0f;
                this.GlowObject.SetActive(false);
            }
        }
        else
        {
            this.GlowLight.enabled = false;
            this.GlowObject.SetActive(false);
            this.HeatParticles.emissionRate = 0f;
            this.GlowObject.GetComponent<Renderer>().enabled = false;
        }
        this.mnUnityUpdates++;
    }

    private void AttemptToStoreOutputHopper()
    {
        ItemBase itemBase = this.mOutputHopper;
        if (itemBase == null)
        {
            return;
        }
        if (itemBase.mType == ItemType.ItemStack && (itemBase as ItemStack).mnAmount == 0)
        {
            this.mOutputHopper = null;
            return;
        }
        for (int i = 0; i < 6; i++)
        {
            long num = this.mnX;
            long num2 = this.mnY;
            long num3 = this.mnZ;
            if (i % 6 == 0)
            {
                num -= 1L;
            }
            if (i % 6 == 1)
            {
                num += 1L;
            }
            if (i % 6 == 2)
            {
                num2 -= 1L;
            }
            if (i % 6 == 3)
            {
                num2 += 1L;
            }
            if (i % 6 == 4)
            {
                num3 -= 1L;
            }
            if (i % 6 == 5)
            {
                num3 += 1L;
            }
            Segment segment = base.AttemptGetSegment(num, num2, num3);
            if (segment != null)
            {
                ushort cube = segment.GetCube(num, num2, num3);
                if (CubeHelper.HasEntity((int)cube))
                {
                    SegmentEntity segmentEntity = segment.SearchEntity(num, num2, num3);
                    if (segmentEntity == null)
                    {
                        return;
                    }
                    StorageMachineInterface storageMachineInterface = segmentEntity as StorageMachineInterface;
                    if (storageMachineInterface != null)
                    {
                        eHopperPermissions permissions = storageMachineInterface.GetPermissions();
                        if (permissions != eHopperPermissions.Locked)
                        {
                            if (permissions != eHopperPermissions.RemoveOnly)
                            {
                                if (this.mOutputHopper == null)
                                {
                                    return;
                                }
                                ItemStack itemStack = itemBase as ItemStack;
                                if (itemStack == null)
                                {
                                    Debug.LogError("ERROR, OUTPUT HOPPER WAS NOT ITEM STACK!");
                                    return;
                                }
                                if (itemStack.mnAmount == 0)
                                {
                                    Debug.LogError("ERROR, OUTPUT HOPPER CONTAINED ZERO!");
                                    return;
                                }
                                string lText = itemBase.ToString();
                                int num4 = storageMachineInterface.TryPartialInsert(this, ref itemBase, false, true);
                                if (num4 > 0)
                                {
                                    ARTHERPetSurvival.mbSmelterOutputAutomation = true;
                                    ((SegmentEntity)storageMachineInterface).RequestImmediateNetworkUpdate();
                                    if (permissions == eHopperPermissions.AddAndRemove)
                                    {
                                        OreSmelter.SmelterIsAttachedToAddRemoveHopper = true;
                                    }
                                    else
                                    {
                                        OreSmelter.SmelterIsAttachedToAddRemoveHopper = false;
                                    }
                                    if (this.mDistanceToPlayer < 16f && MobSpawnManager.mrSmoothedBaseThreat < 2f && FloatingCombatTextManager.instance != null)
                                    {
                                        if (itemBase == null)
                                        {
                                            FloatingCombatTextManager.instance.QueueText(segmentEntity.mnX, segmentEntity.mnY + 1L, segmentEntity.mnZ, 0.75f, lText, Color.green, 1f, 16f);
                                        }
                                        else
                                        {
                                            FloatingCombatTextManager.instance.QueueText(segmentEntity.mnX, segmentEntity.mnY + 1L, segmentEntity.mnZ, 0.75f, itemBase.ToString(), Color.green, 1f, 16f);
                                        }
                                    }
                                    if (itemBase == null)
                                    {
                                        this.mOutputHopper = null;
                                        if (this.mnOreCount == 0)
                                        {
                                            this.mOreType = 0;
                                        }
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private bool AttemptToCollectAnOre()
    {
        for (int i = 0; i < 6; i++)
        {
            long num = this.mnX;
            long num2 = this.mnY;
            long num3 = this.mnZ;
            if (i % 6 == 0)
            {
                num -= 1L;
            }
            if (i % 6 == 1)
            {
                num += 1L;
            }
            if (i % 6 == 2)
            {
                num2 -= 1L;
            }
            if (i % 6 == 3)
            {
                num2 += 1L;
            }
            if (i % 6 == 4)
            {
                num3 -= 1L;
            }
            if (i % 6 == 5)
            {
                num3 += 1L;
            }
            Segment segment = base.AttemptGetSegment(num, num2, num3);
            if (segment == null)
            {
                return false;
            }
            ushort cube = segment.GetCube(num, num2, num3);
            if (CubeHelper.HasEntity((int)cube))
            {
                StorageMachineInterface storageMachineInterface = segment.SearchEntity(num, num2, num3) as StorageMachineInterface;
                if (storageMachineInterface != null)
                {
                    eHopperPermissions permissions = storageMachineInterface.GetPermissions();
                    if (permissions != eHopperPermissions.Locked)
                    {
                        if (permissions != eHopperPermissions.AddOnly)
                        {
                            if (storageMachineInterface.TryExtractCubes(this, this.mOreType, 65535, 1))
                            {
                                ARTHERPetSurvival.mbSmelterInputAutomation = true;
                                ((SegmentEntity)storageMachineInterface).RequestImmediateNetworkUpdate();
                                return true;
                            }
                        }
                    }
                }
            }
        }
        return false;
    }

    private int GetOre(ushort lSearchType, out ushort lType)
    {
        if (WorldScript.mLocalPlayer.mResearch == null)
        {
            lType = 0;
            return 0;
        }
        Array.Clear(this.maSourceMaterialCounts, 0, this.maSourceMaterialCounts.Length);
        int num = 0;
        for (int i = 0; i < 6; i++)
        {
            long num2 = this.mnX;
            long num3 = this.mnY;
            long num4 = this.mnZ;
            if (i % 6 == 0)
            {
                num2 -= 1L;
            }
            if (i % 6 == 1)
            {
                num2 += 1L;
            }
            if (i % 6 == 2)
            {
                num3 -= 1L;
            }
            if (i % 6 == 3)
            {
                num3 += 1L;
            }
            if (i % 6 == 4)
            {
                num4 -= 1L;
            }
            if (i % 6 == 5)
            {
                num4 += 1L;
            }
            Segment segment = base.AttemptGetSegment(num2, num3, num4);
            if (segment != null)
            {
                ushort cube = segment.GetCube(num2, num3, num4);
                if (CubeHelper.HasEntity((int)cube))
                {
                    StorageMachineInterface storageMachineInterface = segment.SearchEntity(num2, num3, num4) as StorageMachineInterface;
                    if (storageMachineInterface != null)
                    {
                        eHopperPermissions permissions = storageMachineInterface.GetPermissions();
                        if (permissions != eHopperPermissions.Locked)
                        {
                            if (permissions != eHopperPermissions.AddOnly)
                            {
                                if (lSearchType != 0)
                                {
                                    num += storageMachineInterface.CountCubes(lSearchType, 65535);
                                }
                                else
                                {
                                    storageMachineInterface.IterateContents(new IterateItem(this.IterateHopperItem), null);
                                }
                            }
                        }
                    }
                }
            }
        }
        if (lSearchType == 0)
        {
            int num5 = 0;
            ushort num6 = 0;
            for (int j = 0; j < this.maSourceMaterialCubeTypes.Length; j++)
            {
                ushort num7 = this.maSourceMaterialCubeTypes[j];
                if (this.maSourceMaterialCounts[j] > num5 && WorldScript.mLocalPlayer.mResearch.IsKnown(num7, 0))
                {
                    if (num7 == 90 || num7 == 91)
                    {
                        if (!DLCOwnership.HasT4() && !DLCOwnership.HasPatreon())
                        {
                            this.mbAllowT4 = false;
                        }
                        if (!this.mbAllowT4)
                        {
                            goto IL_1ED;
                        }
                    }
                    num5 = this.maSourceMaterialCounts[j];
                    num6 = num7;
                }
                IL_1ED:;
            }
            lType = num6;
            return num5;
        }
        if (num > 0)
        {
            lType = lSearchType;
            return num;
        }
        lType = 0;
        return 0;
    }

    private bool IterateHopperItem(ItemBase item, object userState)
    {
        if (item == null || item.mType != ItemType.ItemCubeStack)
        {
            return true;
        }
        ItemCubeStack itemCubeStack = (ItemCubeStack)item;
        if (itemCubeStack.mnAmount <= 0)
        {
            return true;
        }
        for (int i = 0; i < this.maSourceMaterialCubeTypes.Length; i++)
        {
            if (this.maSourceMaterialCubeTypes[i] == itemCubeStack.mCubeType)
            {
                this.maSourceMaterialCounts[i] += itemCubeStack.mnAmount;
            }
        }
        return true;
    }

    public static int GetBarIDFromOreType(ushort lType)
    {
        List<CraftData> recipesForSet = CraftData.GetRecipesForSet("Smelter");
        if (recipesForSet != null)
        {
            foreach (CraftData current in recipesForSet)
            {
                if (current.Costs[0].CubeType == lType)
                {
                    return current.CraftableItemType;
                }
            }
            Debug.LogError("Error, unable to locate ItemID for Bar from type " + lType);
        }
        else
        {
            Debug.LogError("Error, recipes for smelter were not found. Note this can happen in editor when opening the handbook.");
        }
        return ItemEntry.GetIDFromKey("CopperBar", true);
    }

    public bool AddCubeTypeOre(ushort lOreType, int lnCount)
    {
        if (this.mOreType != lOreType)
        {
            if (this.mnOreCount != 0)
            {
                Debug.LogError("Ore Smelter: AddCubeTypeOre with different oretype on non-empty input!");
                return false;
            }
            this.mOreType = lOreType;
          
        }
        if (this.mOutputHopper == null)
        {
            this.PrepareOutputHopper();
        }
        if (this.mOreType != 0)
        {
            this.mnOreCount += lnCount;
            if (this.meState == OreSmelter.eState.eWaitingOnMatTrigger)
            {
                this.mnIdleCount = 0;
                this.SetNewState(OreSmelter.eState.eObtainingMats);
            }
            this.MarkDirtyDelayed();
        }
        return true;
    }

    private void PrepareOutputHopper()
    {
        int barIDFromOreType = OreSmelter.GetBarIDFromOreType(this.mOreType);
        if (this.mOutputHopper == null)
        {
            this.mOutputHopper = ItemManager.SpawnItem(barIDFromOreType);
            if (this.mOutputHopper == null)
            {
                Debug.LogError("OutputHopper failed to get " + barIDFromOreType);
            }
            if (this.mOutputHopper.mType != ItemType.ItemStack)
            {
                Debug.LogError("Can't use items that don't stack");
            }
            (this.mOutputHopper as ItemStack).mnAmount = 0;
        }
        else if (this.mOutputHopper.mnItemID != barIDFromOreType && (this.mOutputHopper as ItemStack).mnAmount == 0)
        {
            this.mOutputHopper.mnItemID = barIDFromOreType;
        }
    }

    private void SetNewState(OreSmelter.eState lState)
    {
        if (lState != this.meState)
        {
            this.RequestImmediateNetworkUpdate();
        }
        this.meState = lState;
        if (this.meState == OreSmelter.eState.eObtainingMats)
        {
            if (this.mOreType == 0)
            {
                Debug.LogException(new AssertException("Error, cannot switch to ObtainingMats when we don't have a cubetype!"));
                this.SetNewState(OreSmelter.eState.eWaitingOnMatTrigger);
            }
            if (this.mOreType > 32768)
            {
                Debug.LogWarning("Warning OreType was ridiculous!");
                this.mOreType = 0;
            }
        }
    }

    private void CalcOrePerBar(ushort lType)
    {
        List<CraftData> recipesForSet = CraftData.GetRecipesForSet("Smelter");
        int num = -1;
        foreach (CraftData current in recipesForSet)
        {
            foreach (CraftCost current2 in current.Costs)
            {
                if (current2.CubeType == lType)
                {
                    num = (int)current2.Amount;
                }
            }
        }
        if (num == -1)
        {
            Debug.LogError("Error, Smelter couldn't calculate ore-per-bar for type " + lType + ", assuming 16!");
            num = 16;
        }
        this.mnOrePerBar = (int)Mathf.Ceil((float)num * DifficultySettings.mrResourcesFactor);
        if (this.mValue == 1)
        {
            this.mnOrePerBar *= 2;
        }
    }

    private void UpdateMatTrigger()
    {
        if (this.mOreType != 0)
        {
            int num = this.mnOreCount;
            if (num > 0)
            {
                this.SmelterError = "Now Obtaining Materials";
                if (this.mOutputHopper == null)
                {
                    this.PrepareOutputHopper();
                    this.SmelterError = "Prepping IO hoppers";
                }
                this.SetNewState(OreSmelter.eState.eObtainingMats);
                return;
            }
        }
        if (this.mnOreCount != 0)
        {
            Debug.LogError("Ore Smelter UpdateMatTriggere: oretype NULL but count not zero");
            this.mnOreCount = 0;
        }
        ushort num2 = 0;
        int ore = this.GetOre(0, out num2);
        int num3 = ore;
        if (num2 != 0)
        {
            this.CalcOrePerBar(num2);
        }
        this.mrTargetTemp = 0f;
        if (num3 >= this.mnOrePerBar && num2 != 0)
        {
            this.mOreType = num2;
            if (this.mOreType > 32768)
            {
                Debug.LogWarning("Warning OreType was ridiculous!");
                this.mOreType = 0;
                return;
            }
            if (ARTHERPetSurvival.instance != null)
            {
                ARTHERPetSurvival.instance.GotOre(this.mOreType);
            }
            this.PrepareOutputHopper();
            if (this.mOreType == 0)
            {
                Debug.LogError("Error, InputHopper set, but mType is NULL");
            }
            this.SetNewState(OreSmelter.eState.eObtainingMats);
        }
        else
        {
            this.AttemptToStoreOutputHopper();
            if (num2 == 0 || num3 == 0)
            {
                this.SmelterError = "Unable to find any ore";
            }
            else if (num3 < this.mnOrePerBar)
            {
                this.SmelterError = string.Concat(new object[]
                {
                    "Need ",
                    this.mnOrePerBar,
                    " found ",
                    num3
                });
            }
            else if (this.mOutputHopper != null)
            {
                this.SmelterError = "Unable to clear output hopper";
            }
            if (this.mOutputHopper != null && this.mOreType != 0)
            {
                this.mnIdleCount = 10;
            }
        }
    }

    private void SetTemperature()
    {
        this.mrTargetTemp = 1139f;
        if (this.mOreType == 90)
        {
            this.mrTargetTemp = 1908f;
        }
        if (this.mOreType == 91)
        {
            this.mrTargetTemp = 2623f;
        }
    }

    private void UpdateObtainingMats()
    {
        if (this.mOutputHopper == null || this.mOreType == 0)
        {
            this.meState = OreSmelter.eState.eWaitingOnMatTrigger;
            return;
        }
        int num = this.mnOreCount;
        for (int i = 0; i < this.mnCollectionRate; i++)
        {
            if (num < this.mnOrePerBar)
            {
                if (this.AttemptToCollectAnOre())
                {
                    this.AddCubeTypeOre(this.mOreType, 1);
                    this.SmelterError = string.Empty;
                    this.mnObtainingFails = 0;
                }
                else if (num == 0)
                {
                    this.mnObtainingFails++;
                    this.SmelterError = "Obtaining materials...";
                    if (this.mnObtainingFails > 10)
                    {
                        this.mnObtainingFails = 0;
                        this.mOreType = 0;
                        this.SetNewState(OreSmelter.eState.eWaitingOnMatTrigger);
                    }
                }
            }
            if (num >= this.mnOrePerBar)
            {
                break;
            }
        }
        if (num >= this.mnOrePerBar)
        {
            if (this.mOutputHopper == null)
            {
                this.meState = OreSmelter.eState.eHeating;
                this.SetTemperature();
            }
            else if ((this.mOutputHopper as ItemStack).mnAmount == 0)
            {
                (this.mOutputHopper as ItemStack).mnItemID = OreSmelter.GetBarIDFromOreType(this.mOreType);
                this.meState = OreSmelter.eState.eHeating;
                this.SetTemperature();
            }
            else
            {
                int barIDFromOreType = OreSmelter.GetBarIDFromOreType(this.mOreType);
                if (this.mOutputHopper.mnItemID == barIDFromOreType)
                {
                    this.meState = OreSmelter.eState.eHeating;
                    this.SetTemperature();
                }
                else
                {
                    this.mbSmelterStuck = true;
                    this.SetNewState(OreSmelter.eState.eWaitingOnMatTrigger);
                }
            }
        }
    }

    private void UpdateSmelting()
    {
        this.SetTemperature();
        if (this.mOreType == 0)
        {
            this.SetNewState(OreSmelter.eState.eWaitingOnMatTrigger);
            return;
        }
        this.mrRemainingSmeltTime -= LowFrequencyThread.mrPreviousUpdateTimeStep * this.mrBurnRate;
        if (this.mrRemainingSmeltTime <= 0f)
        {
            if (this.mOreType == 151)
            {
                Achievements.mbSmeltedTitanium = true;
            }
            if (this.mOreType == 86)
            {
                Achievements.mbSmeltedGold = true;
            }
            if (this.mOreType == 150)
            {
                Achievements.mbSmeltedNickel = true;
            }
            int barIDFromOreType = OreSmelter.GetBarIDFromOreType(this.mOreType);
            if (barIDFromOreType < 0)
            {
                Debug.LogWarning("Warning, Smelter could not locate a valid BarID from OreType " + this.mOreType);
                return;
            }
            ItemBase itemBase = this.mOutputHopper;
            if (itemBase == null)
            {
                this.mOutputHopper = ItemManager.SpawnItem(barIDFromOreType);
            }
            else if (itemBase.mnItemID == barIDFromOreType)
            {
                int num = ItemManager.GetMaxStackSize(itemBase);
                if (num > 25)
                {
                    num = 25;
                }
                ItemStack itemStack = itemBase as ItemStack;
                if (itemStack.mnAmount >= num)
                {
                    this.AttemptToStoreOutputHopper();
                    return;
                }
                itemStack.mnAmount++;
            }
            this.mbSFXQueued = true;
            this.mnOreCount -= this.mnOrePerBar;
            if (this.mnOreCount < 0)
            {
                this.mnOreCount = 0;
            }
            this.mrTimeSinceSmelt = 0f;
            GameManager.BarSmelted();
            if (PlayerStats.mbCreated)
            {
                PlayerStats.instance.SurvivalOresSmelted++;
                PlayerStats.instance.MarkStatsDirty();
            }
            this.AttemptToStoreOutputHopper();
            this.RequestImmediateNetworkUpdate();
            itemBase = this.mOutputHopper;
            if (itemBase != null)
            {
                if ((itemBase as ItemStack).mnAmount > 0)
                {
                    this.SetNewState(OreSmelter.eState.eWaitingOnMatTrigger);
                    this.mrTargetTemp = 0f;
                }
                else
                {
                    this.mOreType = 0;
                    this.SetNewState(OreSmelter.eState.eWaitingOnMatTrigger);
                    this.mrTargetTemp = 0f;
                }
            }
            else if (this.mOreType != 0)
            {
                if (this.mnOreCount > 0)
                {
                    this.SetNewState(OreSmelter.eState.eObtainingMats);
                }
                else
                {
                    this.mOreType = 0;
                    this.mrTargetTemp = 0f;
                    this.SetNewState(OreSmelter.eState.eWaitingOnMatTrigger);
                }
            }
            else
            {
                this.mrTargetTemp = 0f;
                this.SetNewState(OreSmelter.eState.eWaitingOnMatTrigger);
            }
            this.MarkDirtyDelayed();
        }
    }

    private void UpdateTutorial()
    {
        if (!WorldScript.mbIsServer)
        {
            this.IsStartHopper = false;
            return;
        }
        if (this.mValue == 1)
        {
            this.IsStartHopper = false;
            return;
        }
        long x = this.mnX + (long)this.mForwards.x;
        long y = this.mnY + (long)this.mForwards.y;
        long z = this.mnZ + (long)this.mForwards.z;
        Segment segment = base.AttemptGetSegment(x, y, z);
        if (segment == null)
        {
            return;
        }
        ushort cube = segment.GetCube(x, y, z);
        if (cube == 502)
        {
            this.IsStartHopper = true;
            x = this.mnX + (long)this.mRight.x;
            y = this.mnY + (long)this.mRight.y;
            z = this.mnZ + (long)this.mRight.z;
            segment = base.AttemptGetSegment(x, y, z);
            if (segment == null)
            {
                return;
            }
            cube = segment.GetCube(x, y, z);
            if (cube == 505)
            {
                StorageHopper storageHopper = segment.SearchEntity(x, y, z) as StorageHopper;
                if (storageHopper != null)
                {
                    this.HopperRightDetected = true;
                    this.mRightPermissions = storageHopper.mPermissions;
                }
            }
            else
            {
                this.HopperRightDetected = false;
            }
            x = this.mnX - (long)this.mRight.x;
            y = this.mnY - (long)this.mRight.y;
            z = this.mnZ - (long)this.mRight.z;
            segment = base.AttemptGetSegment(x, y, z);
            if (segment == null)
            {
                return;
            }
            cube = segment.GetCube(x, y, z);
            if (cube == 505)
            {
                StorageHopper storageHopper2 = segment.SearchEntity(x, y, z) as StorageHopper;
                if (storageHopper2 != null)
                {
                    this.HopperLeftDetected = true;
                    this.mLeftPermissions = storageHopper2.mPermissions;
                }
            }
            else
            {
                this.HopperLeftDetected = false;
            }
        }
        else
        {
            this.IsStartHopper = false;
        }
    }

    public override void LowFrequencyUpdate()
    {
        this.UpdateTutorial();
        if (this.mbTooDeep)
        {
            ARTHERPetSurvival.mbBrokenSmelter = true;
        }
        float num = this.mrCurrentPower;
        this.UpdatePlayerDistanceInfo();
        if (this.mrCurrentPower > this.mrMaxPower)
        {
            this.mrCurrentPower = this.mrMaxPower;
        }
        this.mrNormalisedPower = this.mrCurrentPower / this.mrMaxPower;
        this.mrRemainingCapacity = this.mrMaxPower - this.mrCurrentPower;
        this.mrSparePowerCapacity = this.mrMaxPower - this.mrCurrentPower;
        this.mrReadoutTick -= LowFrequencyThread.mrPreviousUpdateTimeStep;
        if (this.mrReadoutTick < 0f)
        {
            this.mrReadoutTick = 5f;
        }
        if (this.meState == OreSmelter.eState.eWaitingOnMatTrigger)
        {
            if (this.mnIdleCount > 0)
            {
                this.mnIdleCount--;
            }
            else
            {
                this.UpdateMatTrigger();
            }
        }
        if (this.meState == OreSmelter.eState.eObtainingMats)
        {
            if (this.mOutputHopper != null && (this.mOutputHopper as ItemStack).mnAmount > 0)
            {
                this.AttemptToStoreOutputHopper();
            }
            this.UpdateObtainingMats();
        }
        if (this.meState == OreSmelter.eState.eHeating)
        {
            this.SmelterError = string.Empty;
            if (this.mOreType == 0)
            {
            }
            if (this.mrTemperature >= this.mrTargetTemp)
            {
                this.meState = OreSmelter.eState.eSmelting;
                this.mrRemainingSmeltTime = this.mrSmeltTime;
            }
        }
        if (this.meState == OreSmelter.eState.eSmelting)
        {
            this.SmelterError = string.Empty;
            this.UpdateSmelting();
        }
        if (this.mrTargetTemp > 0f && this.mrTemperature <= this.mrTargetTemp)
        {
            if (this.mrCurrentPower > 2f * this.mrPowerRate)
            {
                this.mrCurrentPower -= 2f * this.mrPowerRate;
                if (this.mrTemperature < this.mrTargetTemp)
                {
                    this.mrTemperature += this.mrTempGainRate * this.mrPowerRate * LowFrequencyThread.mrPreviousUpdateTimeStep;
                }
            }
            else
            {
                this.mrTemperature *= 0.999f;
            }
        }
        else if (this.mrTemperature > 0f)
        {
            this.mrTemperature -= this.mrTempGainRate * 0.5f * LowFrequencyThread.mrPreviousUpdateTimeStep;
            if (this.mrTemperature < 1f)
            {
                this.mrTemperature = 0f;
            }
        }
        float num2 = num - this.mrCurrentPower;
        num2 /= LowFrequencyThread.mrPreviousUpdateTimeStep;
        this.mrAveragePPS += (num2 - this.mrAveragePPS) / 32f;
    }

    public ItemBase GetInventory()
    {
        ItemBase itemBase = null;
        if (this.mOutputHopper != null)
        {
            ItemStack itemStack = this.mOutputHopper as ItemStack;
            if (itemStack.mnAmount == 0)
            {
                return null;
            }
            itemBase = ItemManager.SpawnItem(this.mOutputHopper.mnItemID);
            (itemBase as ItemStack).mnAmount = itemStack.mnAmount;
            if (itemStack.mnAmount == 0)
            {
                Debug.LogError("ERROR OUTPUT STACK HAD ZERO AMOUNT");
            }
            itemStack.mnAmount = 0;
            this.MarkDirtyDelayed();
            Debug.Log(string.Concat(new object[]
            {
                "Returning Output Hopper ",
                ItemEntry.mEntries[this.mOutputHopper.mnItemID].Name,
                " from Smelter, total amount ",
                (itemBase as ItemStack).mnAmount
            }));
            if (this.meState == OreSmelter.eState.eWaitingOnMatTrigger && this.mnOreCount == 0)
            {
                this.mOutputHopper = null;
                this.mOreType = 0;
            }
        }
        return itemBase;
    }

    public void ClearInput()
    {
        Debug.Log("Clearing input hopper");
        this.mOreType = 0;
        this.mnOreCount = 0;
        this.SetNewState(OreSmelter.eState.eWaitingOnMatTrigger);
        this.MarkDirtyDelayed();
    }

    public void ClearOutput()
    {
        this.mOutputHopper = null;
        if (this.meState == OreSmelter.eState.eWaitingOnMatTrigger && this.mnOreCount == 0)
        {
            this.mOreType = 0;
        }
        this.MarkDirtyDelayed();
    }

    public ushort FindFeedableOre(Player player)
    {
        if (this.meState == OreSmelter.eState.eWaitingOnMatTrigger)
        {
            if (this.mOreType == 0)
            {
                List<CraftData> recipesForSet = CraftData.GetRecipesForSet("Smelter");
                foreach (CraftData current in recipesForSet)
                {
                    ushort cubeType = current.Costs[0].CubeType;
                    if (WorldScript.mLocalPlayer.mResearch.IsKnown(cubeType, 0))
                    {
                        if (this.CheckPlayerOre(cubeType, player))
                        {
                            return cubeType;
                        }
                    }
                }
                return 0;
            }
            if (this.CheckPlayerOre(this.mOreType, player))
            {
                return this.mOreType;
            }
            return 0;
        }
        else
        {
            if ((this.meState == OreSmelter.eState.eObtainingMats || this.meState == OreSmelter.eState.eHeating) && this.CheckPlayerOre(this.mOreType, player))
            {
                return this.mOreType;
            }
            return 0;
        }
    }

    private bool CheckPlayerOre(ushort lOre, Player player)
    {
        if (lOre == 0)
        {
            return false;
        }
        if (lOre == 90)
        {
            return false;
        }
        if (lOre == 91)
        {
            return false;
        }
        int cubeTypeCount = player.mInventory.GetCubeTypeCount(lOre);
        int num = cubeTypeCount;
        int num2 = 0;
        if (cubeTypeCount == 0)
        {
            return false;
        }
        if (this.mOreType != 0)
        {
            if (this.mOreType != lOre)
            {
                return false;
            }
            num2 = this.mnOreCount;
            num += num2;
        }
        int capacity = this.GetCapacity(lOre);
        bool result = false;
        if (num2 < capacity)
        {
            if (this.mOreType == lOre)
            {
                result = true;
            }
            else if (this.mOreType == 0 && num >= this.GetOreLimitForOre(lOre))
            {
                result = true;
            }
        }
        return result;
    }

    public bool AttemptFeedOre(ushort oreType, Player player)
    {
        if (oreType == 90)
        {
            return false;
        }
        if (oreType == 91)
        {
            return false;
        }
        if (this.meState == OreSmelter.eState.eWaitingOnMatTrigger)
        {
            if (this.mOreType != 0 && this.mOreType != oreType)
            {
                return false;
            }
            this.mOreType = oreType;
        }
        if ((this.meState == OreSmelter.eState.eObtainingMats || this.meState == OreSmelter.eState.eHeating) && this.mOreType != oreType)
        {
            return false;
        }
        int num = this.mnOrePerBar;
        if (this.mnOrePerBar == 0)
        {
            Debug.LogError("Error, Smeter set to 0 ore per bar! DifficultySettings.mrResourcesFactor was " + DifficultySettings.mrResourcesFactor);
            this.mnOrePerBar = 8;
        }
        if (this.mnOreCount % this.mnOrePerBar > 0)
        {
            num = this.mnOrePerBar - this.mnOreCount % this.mnOrePerBar;
        }
        int capacity = this.GetCapacity(oreType);
        if (this.mnOreCount > capacity - num)
        {
            return false;
        }
        if (player == WorldScript.mLocalPlayer && !player.mInventory.AttemptToRemove(oreType, global::TerrainData.GetDefaultValue(oreType), num))
        {
            return false;
        }
        this.mnOreCount += num;
        return true;
    }

    public int GetInputCount()
    {
        return this.mnOreCount;
    }

    public ItemCubeStack GetInputHopper()
    {
        int num = 0;
        if (this.meState == OreSmelter.eState.eSmelting)
        {
            num = this.mnOrePerBar;
        }
        if (this.mnOreCount <= num)
        {
            return null;
        }
        return ItemManager.SpawnCubeStack(this.mOreType, global::TerrainData.GetDefaultValue(this.mOreType), this.mnOreCount - num);
    }

    public void DeductInput(int amount)
    {
        this.mnOreCount -= amount;
        if (this.mnOreCount <= 0)
        {
            this.mnOreCount = 0;
            this.mOreType = 0;
            this.SetNewState(OreSmelter.eState.eWaitingOnMatTrigger);
        }
        this.MarkDirtyDelayed();
    }

    public bool IsValidIngredient(ItemBase newItem)
    {
        ItemCubeStack itemCubeStack = newItem as ItemCubeStack;
        if (itemCubeStack == null)
        {
            return false;
        }
        if (this.mOreType != 0)
        {
            if (itemCubeStack.mCubeType == this.mOreType)
            {
                return true;
            }
            if (this.meState == OreSmelter.eState.eSmelting)
            {
                return false;
            }
        }
        if (!this.mbAllowT4)
        {
            if (itemCubeStack.mCubeType == 90)
            {
                return false;
            }
            if (itemCubeStack.mCubeType == 91)
            {
                return false;
            }
        }
        List<CraftData> recipesForSet = CraftData.GetRecipesForSet("Smelter");
        for (int i = 0; i < recipesForSet.Count; i++)
        {
            CraftData craftData = recipesForSet[i];
            foreach (CraftCost current in craftData.Costs)
            {
                if (current.CubeType == itemCubeStack.mCubeType)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public int GetCapacity(ushort cubeType)
    {
        return 32 / this.mnOrePerBar * this.mnOrePerBar;
    }

    public override bool ShouldSave()
    {
        return true;
    }

    public override int GetVersion()
    {
        return 2;
    }

    public override void Write(BinaryWriter writer)
    {
        float value = 0f;
        writer.Write(this.mrCurrentPower);
        writer.Write(this.mrTemperature);
        writer.Write(this.mOreType);
        writer.Write(0);
        writer.Write(this.mnOreCount);
        writer.Write(value);
        writer.Write(value);
        writer.Write(value);
        writer.Write(value);
        ItemFile.SerialiseItem(null, writer);
        ItemFile.SerialiseItem(this.mOutputHopper, writer);
    }

    public override void Read(BinaryReader reader, int entityVersion)
    {
        this.mrCurrentPower = reader.ReadSingle();
        if (this.mrCurrentPower > this.mrMaxPower)
        {
            this.mrCurrentPower = this.mrMaxPower;
        }
        this.mrTemperature = reader.ReadSingle();
        this.mOreType = reader.ReadUInt16();
        if (this.mOreType > 32768)
        {
            Debug.LogWarning("Warning OreType was ridiculous!");
            this.mOreType = 0;
        }
        reader.ReadUInt16();
        this.mnOreCount = reader.ReadInt32();
        reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        if (this.mrCurrentPower > this.mrMaxPower)
        {
            this.mrCurrentPower = this.mrMaxPower;
        }
        this.mrNormalisedPower = this.mrCurrentPower / this.mrMaxPower;
        this.mrRemainingCapacity = this.mrMaxPower - this.mrCurrentPower;
        if (entityVersion >= 2)
        {
            ItemBase itemBase = ItemFile.DeserialiseItem(reader);
            this.mOutputHopper = ItemFile.DeserialiseItem(reader);
            if (itemBase != null)
            {
                Debug.Log("Found old smelter, dropping.");
                this.mOreType = 0;
                this.mnOreCount = 0;
            }
        }
    }

    public override bool ShouldNetworkUpdate()
    {
        return true;
    }

    public float GetRemainingPowerCapacity()
    {
        return this.mrMaxPower - this.mrCurrentPower;
    }

    public float GetMaximumDeliveryRate()
    {
        return this.mrMaxPower / 2f;
    }

    public float GetMaxPower()
    {
        return this.mrMaxPower;
    }

    public bool DeliverPower(float amount)
    {
        if (amount > this.GetRemainingPowerCapacity())
        {
            return false;
        }
        this.mrCurrentPower += amount;
        return true;
    }

    public bool WantsPowerFromEntity(SegmentEntity entity)
    {
        return true;
    }

    public int GetOreLimitForOre(ushort type)
    {
        return this.mnOrePerBar;
    }

    public override HoloMachineEntity CreateHolobaseEntity(Holobase holobase)
    {
        HolobaseEntityCreationParameters holobaseEntityCreationParameters = new HolobaseEntityCreationParameters(this);
        HolobaseVisualisationParameters holobaseVisualisationParameters = holobaseEntityCreationParameters.AddVisualisation(holobase.mPreviewCube);
        holobaseVisualisationParameters.Color = Color.red;
        return holobase.CreateHolobaseEntity(holobaseEntityCreationParameters);
    }

    public void ResetSmeltingParameters()
    {
        this.mrTempGainRate = 25f;
        this.mrBurnRate = 1f;
        this.mrPowerRate = 1f;
        this.mrMaxPower = 128f;
        this.mnCollectionRate = 1;
        this.mbAllowT4 = false;
    }

    public bool SupportsForcedInduction()
    {
        return this.mValue != 1;
    }

    public void SetSmelterTemperatureGainRate(float value)
    {
        this.mrTempGainRate = value;
    }

    public void SetSmelterBurnRate(float value)
    {
        this.mrBurnRate = value;
    }

    public void SetSmelterPowerRate(float value)
    {
        this.mrPowerRate = value;
    }

    public void SetSmelterMaxPower(float value)
    {
        this.mrMaxPower = value;
    }

    public void SetSmelterCollectionRate(int value)
    {
        this.mnCollectionRate = value;
    }

    public void SetSmelterSupportsTier4(bool value)
    {
        this.mbAllowT4 = value;
    }
    public override string GetPopupText()
    {
        string text = "test" + this.meState;
        return text;
    }

}
