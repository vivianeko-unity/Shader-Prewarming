using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public class ShaderVariantData
{
    public Shader shader;
    public PassType passType;
    public string[] keywords;
    public float uploadTime;
    public int uploadCount;
}


[Serializable]
public class ShaderKeywordsData
{
    public Shader shader;
    public string[] keywords;
}