#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using MakubeX;

public static class WebEntryBuild
{
    public static void Run()
    {
        BuildAt("Builds/WebGL_StartPage_20261026","../Verification/WebEntry_20261026");
    }
    public static void BuildAt(string outputFolder,string evidenceFolder,string entryScene=null,string chapterScene=null)
    {
        var output=Path.GetFullPath(outputFolder);
        if(Directory.Exists(output)) throw new InvalidOperationException("Preserve existing build: "+output);
        var catalog=Resources.Load<MakubeXAudioCatalog>("Audio/MakubeXAudioCatalog");
        if(!catalog) throw new InvalidOperationException("Missing audio catalog");
        var previous=typeof(MakubeXAudioCatalog).GetFields().Where(f=>f.FieldType==typeof(AudioClip))
            .Select(f=>(AudioClip)f.GetValue(catalog)).Where(c=>c)
            .Select(c=>(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(c)))
            .Select(i=>new Saved{path=i.assetPath,settings=i.defaultSampleSettings}).ToArray();
        try {
            foreach(var item in previous.Where(s=>s.settings.loadType==AudioClipLoadType.Streaming)) {
                var importer=(AudioImporter)AssetImporter.GetAtPath(item.path);
                var settings=item.settings;settings.loadType=AudioClipLoadType.CompressedInMemory;
                importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            }
            MakubeX.Editor.WebBuild.BuildAt(output,entryScene,chapterScene);
        } finally {
            foreach(var item in previous.Where(s=>s.settings.loadType==AudioClipLoadType.Streaming)) {
                var importer=(AudioImporter)AssetImporter.GetAtPath(item.path);
                importer.defaultSampleSettings=item.settings;importer.SaveAndReimport();
            }
            var report=BuildReport.GetLatestReport();
            var evidence=Path.GetFullPath(evidenceFolder);
            Directory.CreateDirectory(evidence);
            if(report) {
                var s=report.summary;
                File.WriteAllText(Path.Combine(evidence,"build.json"),JsonUtility.ToJson(new Result{result=s.result.ToString(),output=s.outputPath,errors=s.totalErrors,warnings=s.totalWarnings,bytes=s.totalSize},true));
                File.WriteAllText(Path.Combine(evidence,"build-messages.txt"),string.Join("\n",report.steps.SelectMany(step=>step.messages.Select(m=>m.type+": "+m.content))));
            }
        }
    }
    sealed class Saved { public string path; public AudioImporterSampleSettings settings; }
    [Serializable] sealed class Result { public string result,output;public int errors,warnings;public ulong bytes; }
}
#endif
