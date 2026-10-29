#if UNITY_EDITOR
using UnityEditor;
public static class MemoryMapLocalBuild
{
    [MenuItem("MakubeX/Memory Map/Build Local Review")]
    public static void Run()
    {
        WebEntryBuild.BuildAt("Builds/WebGL_MemoryMapLocal_20261026","../Verification/MemoryMapLocal_20261026");
    }
    public static void RunReview()
    {
        WebEntryBuild.BuildAt("Builds/WebGL_MemoryMapLocal_20261026_r2","../Verification/MemoryMapLocal_20261026");
    }
}
#endif
