#if UNITY_EDITOR
using UnityEditor;
public static class PlayComfortLocalBuild
{
    [MenuItem("MakubeX/Playtest/Build Local Controls And Settings")]
    public static void Run()
    {
        WebEntryBuild.BuildAt("Builds/WebGL_PlayComfortLocal_20261026_r2","../Verification/PlayComfortLocal_20261026");
    }
}
#endif
