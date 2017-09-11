using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
public class OrePurifierMk1 : MachineEntity, PowerConsumerInterface
{
    #region Startup
    //debug crap
    public string lastDebug;
    public int DebugValue;
    public string debugString;
    /*What states the purifier can be in in*/
    public enum eState
    {
        eLookingForResources,
        eCrafting,
        eLookingToDropOffOre,
        eOutOfPower,
        eOutOfCatalyst,
        eNumStates
    }
    //Object's name in a attempt to increase code reusability
    public string Name = "OrePurifierMk1";

    public const eSegmentEntity SEGMENT_ENTITY = eSegmentEntity.Mod;
    //Refined Liquid Resins Key
    public static int REFINEDRESIN = ItemEntry.GetIDFromKey("RefinedLiquidResin",true); 
    //The purifier's current state
    public OrePurifierMk1.eState meState;
    //Whether the block is linked to a game object
    private bool mbLinkedToGO;
    //Animations
    private Animation mAnimation;
    //Willl this be used, targeted item to create
    public ItemBase mTargetCreation;
    public ItemCubeStack mTargetCreationCube;
    public string mTarget;
    //PPS the device requires
    private float mrPowerPerSecond = 1f;
    //How long until crafting is complete
    public float mrCraftingTimer;
    //?????
   // private OrePurifierMk1.eState mUnityState;
    //????
   // private MaterialPropertyBlock mMPB;

   // private float mrGlow;

   // private Renderer mRend;

    private ParticleSystem CompletionParticles;

    private ParticleSystem HeatParticles;

   // private float mrTimeSinceSmelt;

    private GameObject TextQuad;

    private GameObject GlowObject;

    private GameObject SmelterObj;

    private Light GlowLight;

    private TextMesh mReadout;

    private int mnUnityUpdates;
    //What hoppers are attached to the machine
    private StorageMachineInterface[] maAttachedHoppers;
    //How many hoppers are attached
    private int mnNumAttachedHoppers;
    //How much power the device has
    public int mResinStorage;
    public float mrCurrentPower;
    //Maximum amount of power the machine can have
    public float mrMaxPower = 500f;
    //How quickly the machine can accept Power
    public float mrMaxTransferRate = 750f;
    //Constructor

    public OrePurifierMk1(ModCreateSegmentEntityParameters parameters) : base(parameters)
    {
       
        this.meState = OrePurifierMk1.eState.eLookingForResources;
        this.mbNeedsLowFrequencyUpdate = true;
        this.mbNeedsUnityUpdate = true;
        this.mTargetCreation = null;
        this.maAttachedHoppers = new StorageMachineInterface[6];
        this.mResinStorage = 0;
        this.mnUnityUpdates = 0;
    }

    public override void DropGameObject()
    {
        base.DropGameObject();
        this.mbLinkedToGO = false;
    }
    #endregion
    #region Unknown Code
    public override void UnityUpdate()
    {
        if (!this.mbLinkedToGO)
        {
            if (this.mWrapper == null || !this.mWrapper.mbHasGameObject)
            {
                return;
            }
            if (this.mWrapper.mGameObjectList == null)
            {
                Debug.LogError("Ore Purifier missing game object #0?");
            }
            if (this.mWrapper.mGameObjectList[0].gameObject == null)
            {
                Debug.LogError("Ore Purifier missing game object #0 (GO)?");
            }
            this.mReadout = this.mWrapper.mGameObjectList[0].transform.Search("ReadoutText").GetComponent<TextMesh>();
            this.mbLinkedToGO = true;
            this.CompletionParticles = this.mWrapper.mGameObjectList[0].transform.Search("SmeltCompleteParticles").GetComponent<ParticleSystem>();
            this.HeatParticles = this.mWrapper.mGameObjectList[0].transform.Search("HeatParticles").GetComponent<ParticleSystem>();
            this.GlowObject = this.mWrapper.mGameObjectList[0].transform.Search("SmelterGlow").gameObject;
            this.GlowLight = this.mWrapper.mGameObjectList[0].transform.Search("Smelting Light").gameObject.GetComponent<Light>();
            this.SmelterObj = this.mWrapper.mGameObjectList[0].transform.Search("Smelter").gameObject;
            this.TextQuad = this.mWrapper.mGameObjectList[0].transform.Search("Quad").gameObject;
        }
        if (this.mnUnityUpdates % 60 == 0)
        {
            //this.ConfigLOD();

            // if (this.mDotWithPlayerForwards > 0f)
            // {
            if (this.meState == eState.eLookingForResources)
            {
                this.mReadout.text = "Insert Ore to start";
            }
            else if (this.meState == eState.eCrafting)
            {
                this.mReadout.text = "Smelting...";
            }
            else
            {
                this.mReadout.text = "N/A";
            }
                /*
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
                                    }*/
          //  }


            /*
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
            */
            // else
            //{
            this.GlowLight.enabled = false;
            this.GlowObject.SetActive(false);
            this.HeatParticles.emissionRate = 0f;
            this.GlowObject.GetComponent<Renderer>().enabled = false;
            //  }
        }
            this.mnUnityUpdates++;
    
            /*
            this.mAnimation = this.mWrapper.mGameObjectList[0].GetComponentInChildren<Animation>();
            this.mAnimation.wrapMode = WrapMode.Loop;
            this.mUnityState = OrePurifierMk1.eState.eNumStates;
            this.mMPB = new MaterialPropertyBlock();
            this.mRend = this.mWrapper.mGameObjectList[0].transform.Search("Laboratory Base").GetComponent<Renderer>();
        }
        if (this.mDistanceToPlayer < 32f)
        {
            float num = 0f;
            if (this.meState == OrePurifierMk1.eState.eCrafting)
            {
                if (this.mrGlow < 9f)
                {
                    this.mrGlow += Time.deltaTime * 9f;
                }
                num = Mathf.Sin(Time.time) * 2f;
            }
            else
            {
                this.mrGlow *= 0.95f;
            }
            this.mMPB.SetFloat("_GlowMult", num + (this.mrGlow - 1f));
            this.mRend.SetPropertyBlock(this.mMPB);
        }
        if (this.mUnityState != this.meState)
        {
            if (this.meState == OrePurifierMk1.eState.eCrafting)
            {
                this.mAnimation.CrossFade("Research Work Temp", 1f);
            }
            if (this.meState == OrePurifierMk1.eState.eOutOfPower)
            {
                this.mAnimation.CrossFade("Research Idle Temp", 1f);
            }
            if (this.meState == OrePurifierMk1.eState.eLookingForResources)
            {
                this.mAnimation.CrossFade("Research Idle Temp", 1f);
            }
            if (this.meState == OrePurifierMk1.eState.eLookingToDropOffOre)
            {
                this.mAnimation.CrossFade("Research Idle Temp", 1f);
            }
            this.mUnityState = this.meState;
        }
        */
    }

    private void SetNewState(OrePurifierMk1.eState leNewState)
    {
        this.meState = leNewState;
        if (this.meState == OrePurifierMk1.eState.eCrafting && this.mrCraftingTimer <= 0f)
        {
            this.mrCraftingTimer = 15f;
            //if (DifficultySettings.mbCasualResource)
            // {
            //    this.mrCraftingTimer = 5f;
            // }
        }
    }

    public override void LowFrequencyUpdate()
    {
        this.UpdatePlayerDistanceInfo();
        //If state is looking for resources try to find some

        if (this.meState == OrePurifierMk1.eState.eLookingForResources)
        {
            //if we are a server and the target creation is set something is wrong!
            if (WorldScript.mbIsServer && this.mTargetCreation != null)
            {
                Debug.LogError("Error, looking for resources to start new Experiment Pod, but we're already setup to make one!");
            }
            this.UpdateLookingForResources();
        }
        //If our state is  out of power but we have enough power lets go crafting!
        if (this.meState == OrePurifierMk1.eState.eOutOfPower && this.mrCurrentPower > this.mrMaxPower * 0.5f)
        {
            this.SetNewState(OrePurifierMk1.eState.eCrafting);
        }
        //If we are able to craft then proceed with the crafting
        if (this.meState == OrePurifierMk1.eState.eCrafting)
        {
            //How much power will this require during the time elapsed since last update
            float num = this.mrPowerPerSecond * LowFrequencyThread.mrPreviousUpdateTimeStep;
            //if we have the power let's proceed remove the power and decrement the crafting timer
            if (this.mrCurrentPower > num)
            {
                this.mrCurrentPower -= num;
                this.mrCraftingTimer -= LowFrequencyThread.mrPreviousUpdateTimeStep;
                //if crafting is complete let's try to drop off the Ore 
                if (this.mrCraftingTimer <= 0f)
                {
                    this.RequestImmediateNetworkUpdate();
                    this.SetNewState(OrePurifierMk1.eState.eLookingToDropOffOre);
                    PlayerStats.instance.SurvivalItemCrafted++;
                    PlayerStats.instance.MarkStatsDirty();
                }
            }
            else
            {
                this.SetNewState(OrePurifierMk1.eState.eOutOfPower);
            }
        }
        //Try to drop off the ore
       
        if (this.meState == OrePurifierMk1.eState.eLookingToDropOffOre)
        {

            this.UpdateAttachedHoppers(true);
            
          
            if (this.mnNumAttachedHoppers > 0)
            {
                //For every attached valid input hopper try to insert the ore into the hopper, nullify the target created and reset to start new crafting operation
            
                for (int i = 0; i < this.mnNumAttachedHoppers; i++)
                {
                    if (this.mTargetCreation != null)
                    {
                        if (this.maAttachedHoppers[i].TryInsert(this, this.mTargetCreation))
                        {
                            this.mTargetCreation = null;
                            this.SetNewState(OrePurifierMk1.eState.eLookingForResources);
                            return;
                        }
                    }
                    else if (this.mTargetCreationCube != null)
                        if (this.maAttachedHoppers[i].TryInsert(this, this.mTargetCreationCube.mCubeType, this.mTargetCreationCube.mCubeValue,this.mTargetCreationCube.mnAmount))
                        {
                            this.mTargetCreationCube = null;
                            this.SetNewState(OrePurifierMk1.eState.eLookingForResources);
                        }
                }
            }
        }
    }
    #endregion
    #region Resource Code
    private void UpdateLookingForResources()
    {     
        this.UpdateAttachedHoppers(false);
        List<CraftData> RecipesForSet = CraftData.GetRecipesForSet(this.Name);
        //Go through all recipes until one succeeds
        for (int i = 0; i < RecipesForSet.Count; i ++)
        {
            if (this.mResinStorage < 50)
            {
                this.GetResin();
            }
            if (this.mResinStorage > 0)
            {
                if (this.GetItemsForRecipe(RecipesForSet[i]))
                {
                    this.mResinStorage -= 1;
                    break;
                }
            }
        }
    }
    //finds a hopper with the correct items, removes it and sets to item to be crafted and the machines state
    //if succecssful, returns true else returns false
    private bool GetItemsForRecipe(CraftData recipe)
    {
        int itemCount;
        int itemId;
        ushort cubeType;
        ushort cubeValue;
        //get recipe requirement item
        MaterialData.GetItemIdOrCubeValues(recipe.Costs[0].Key, out itemId, out cubeType, out cubeValue);
        //for every attached hopper find see if it has the required item
        for (int i = 0; i < this.mnNumAttachedHoppers; i++)
        {
            if (cubeType > 0)
            {
                itemCount = this.maAttachedHoppers[i].CountCubes(cubeType, cubeValue);
            }
            else
            {
                itemCount = this.maAttachedHoppers[i].CountItems(itemId);
            }
            ItemBase item;
            this.maAttachedHoppers[i].TryExtractAny(this, 1, out item);
            GameManager.DoLocalChat(item.GetDisplayString() +":ID " + item.mnItemID +":Type " +item.mType);
            this.maAttachedHoppers[i].TryInsert(this,item);
            //if it does extract the items, 
            if (itemCount >= recipe.Costs[0].Amount)
            {

                if (this.maAttachedHoppers[i].TryExtractItemsOrCubes(this,itemId,cubeType,cubeValue,(int)recipe.Costs[0].Amount))
                {
                    MaterialData.GetItemIdOrCubeValues(recipe.CraftedKey, out itemId, out cubeType, out cubeValue);
                    mTarget = recipe.CraftedKey;
                  //  if (cubeType > 0)
                   // {
                    //    this.mTargetCreationCube = ItemManager.SpawnCubeStack(cubeType, cubeValue, (int)recipe.CraftedAmount);
                   // }
                   // else
                   // {
                        this.mTargetCreation = ItemManager.SpawnItem(recipe);
                   // }
                    this.SetNewState(OrePurifierMk1.eState.eCrafting);
                    return true;
                }
            }
        }
        return false;
    }
    private void GetResin()
    {
        for (int i = 0; i < this.mnNumAttachedHoppers; i++)
        {
            if(maAttachedHoppers[i].TryExtractItems(this,REFINEDRESIN,1))
            {
                this.mResinStorage += 100;
                return;
            }
        }
    }
    //finds directly attached hoppers that are valid for input or output
    //if lbInput is true, searches for input else output
    private void UpdateAttachedHoppers(bool lbInput)
    {
        int num = 0;
        for (int i = 0; i < 6; i++)
        {
            long num2 = this.mnX;
            long num3 = this.mnY;
            long num4 = this.mnZ;
            if (i == 0)
            {
                num2 -= 1L;
            }
            if (i == 1)
            {
                num2 += 1L;
            }
            if (i == 2)
            {
                num3 -= 1L;
            }
            if (i == 3)
            {
                num3 += 1L;
            }
            if (i == 4)
            {
                num4 -= 1L;
            }
            if (i == 5)
            {
                num4 += 1L;
            }
            Segment segment = base.AttemptGetSegment(num2, num3, num4);
            if (segment != null)
            {
                StorageMachineInterface storageMachineInterface = segment.SearchEntity(num2, num3, num4) as StorageMachineInterface;
                if (storageMachineInterface != null)
                {
                    eHopperPermissions permissions = storageMachineInterface.GetPermissions();
                    if (permissions != eHopperPermissions.Locked)
                    {
                        if (lbInput || permissions != eHopperPermissions.AddOnly)
                        {
                            if (!lbInput || permissions != eHopperPermissions.RemoveOnly)
                            {
                                if (!lbInput || !storageMachineInterface.IsFull())
                                {
                                    if (lbInput || !storageMachineInterface.IsEmpty())
                                    {
                                        this.maAttachedHoppers[num] = storageMachineInterface;
                                        num++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        this.mnNumAttachedHoppers = num;
    }
    #endregion
    #region Power Code
    public float GetRemainingPowerCapacity()
    {
        return this.mrMaxPower - this.mrCurrentPower;
    }

    public float GetMaximumDeliveryRate()
    {
        return this.mrMaxTransferRate;
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
        this.MarkDirtyDelayed();
        return true;
    }

    public bool WantsPowerFromEntity(SegmentEntity entity)
    {
        return true;
    }
    #endregion
    #region Cleanup Code
    public override bool ShouldSave()
    {
        return true;
    }

    public override void Read(BinaryReader reader, int entityVersion)
    {
        this.meState = (OrePurifierMk1.eState)reader.ReadInt32();
        this.mrCurrentPower = reader.ReadSingle();
        this.mrCraftingTimer = reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        this.mTargetCreation = ItemFile.DeserialiseItem(reader);
        this.mResinStorage = reader.ReadInt32();
       // this.mTarget = reader.ReadString();
        /*if (mTarget != null)
        {
            int itemId;
            ushort cubeType;
            ushort cubeValue;
            MaterialData.GetItemIdOrCubeValues(mTarget, out itemId, out cubeType, out cubeValue);
            if (cubeType > 0)
            {
                this.mTargetCreationCube = ItemManager.SpawnCubeStack(cubeType, cubeValue, reader.ReadInt32());
            }
        }*/
    }

    public override void Write(BinaryWriter writer)
    {
        float value = 0f;
        writer.Write((int )this.meState);
        writer.Write(this.mrCurrentPower);
        writer.Write(this.mrCraftingTimer);
        writer.Write(value);
        writer.Write(value);
        writer.Write(value);
        writer.Write(value);
        writer.Write(value);
        ItemFile.SerialiseItem(this.mTargetCreation, writer);
        writer.Write(this.mResinStorage);
        // mTargetCreationCube.Write(writer);
        //writer.Write(mTarget);
    }

    public override bool ShouldNetworkUpdate()
    {
        return true;
    }
    #endregion
    public override string GetPopupText()
    {
        string text = "Resin Storage: " + this.mResinStorage;
        text += "\nPower: " + this.mrCurrentPower + "/" + this.mrMaxPower; 
        if(this.meState != eState.eCrafting)
            text += "\n" + this.meState;
        if(this.meState == eState.eCrafting)
        {
            text += "\nCrafting:" + this.mTargetCreation.GetDisplayString() + "\n" + mrCraftingTimer.ToString("N1") + "s remaining";
        }


        return text;
    }
}
