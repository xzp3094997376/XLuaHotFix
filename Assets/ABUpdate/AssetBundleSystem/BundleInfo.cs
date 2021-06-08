using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;


[Serializable]
public class BundleInfoData
{
    public string BundleName;
    public string Hash;
    public double Size;


    public BundleInfoData()
    {
        
    }
    public BundleInfoData(string bundleName, string hash, double size)
    {
        BundleName = bundleName;
        Hash = hash;
        Size = size;
    }

}
public class BundleInfo : ScriptableObject
{
    public int Version;
    public string Url = "http://www.baidu.com";
    public List<BundleInfoData> dataList=  new List<BundleInfoData>();
   
    private Dictionary<string, BundleInfoData> dataDic = null;

    public BundleInfo()
    {

    }
    public BundleInfoData GetData(string bundleName)
    {
        if (dataDic == null)
        {
            dataDic = new Dictionary<string, BundleInfoData>();
            foreach (var bundleInfoData in dataList)
            {
                dataDic[bundleInfoData.BundleName] = bundleInfoData;
            }
        }

        if (!dataDic.ContainsKey(bundleName))
        {
            return null;
        }

        return dataDic[bundleName];
    }

}
