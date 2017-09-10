using System;
using System.IO;
using System.Reflection;
using UnityEngine;


public class OrePurifierMod : FortressCraftMod
{
    public ushort OrePurifierMk1Cube = ModManager.mModMappings.CubesByKey["Nedrith.OrePurifierMk1"].CubeType;

    private string XMLModID = "Nedrith.OrePurifier";
    private int XMLModVersion = 1;

    public override ModRegistrationData Register()
    {
       
        ModRegistrationData modRegistrationData = new ModRegistrationData();
        modRegistrationData.RegisterEntityHandler("Nedrith.OrePurifierMk1");
        return modRegistrationData;
    }



    public override void CreateSegmentEntity(ModCreateSegmentEntityParameters parameters, ModCreateSegmentEntityResults results)
    {
        if (parameters.Cube == OrePurifierMk1Cube)
        {
            parameters.ObjectType = SpawnableObjectEnum.ExperimentalAssembler;
            results.Entity = new OrePurifierMk1(parameters.Segment, parameters.X, parameters.Y, parameters.Z, parameters.Cube, parameters.Flags, parameters.Value, parameters.LoadFromDisk);
        }
    }
        
        
}
