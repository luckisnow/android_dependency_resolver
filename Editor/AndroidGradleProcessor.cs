#if UNITY_EDITOR && UNITY_ANDROID
using System;
using System.Collections.Generic;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TapTap.AndroidDependencyResolver.Editor
{
    public class AndroidGradleProcessor : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
    {
        // 当前版本
        private const int VERSION = 1;
        
        private static Dictionary<CustomTemplateType, bool> gardleTemplateToggleRecord = new Dictionary<CustomTemplateType, bool>();

        public const int CALLBACK_ORDER = 1100;
        public int callbackOrder => CALLBACK_ORDER;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // for (int i = (int)CustomTemplateType.AndroidManifest; i <= (int)CustomTemplateType.GradleProperties; i++)
            // {
            //     AndroidUtils.ToggleCustomTemplateFile((CustomTemplateType)i,
            //         gardleTemplateToggleRecord[(CustomTemplateType)i]);
            // }
        }
        
        public void OnPreprocessBuild(BuildReport report)
        {
            gardleTemplateToggleRecord.Clear();
            for (int i = (int)CustomTemplateType.AndroidManifest; i <= (int)CustomTemplateType.GradleProperties; i++)
            {
                var haveCustomGradleTemplate = AndroidUtils.HaveCustomTemplateFile((CustomTemplateType)i);
                gardleTemplateToggleRecord.Add((CustomTemplateType)i, haveCustomGradleTemplate);
            }
            
            var providers = AndroidUtils.Load();
            
            Debug.LogFormat($"[TapTap.AGCP] Load Provider Count: {providers?.Count ?? -1}");
            if (providers == null) return;

            for (int i = 0; i < providers.Count; i++)
            {
                var provider = providers[i];
                if (provider.AndroidGradleContext == null)
                {
                    Debug.LogFormat($"[TapTap.AGCP] Provider: {provider.ModuleName} return! since : provider.AndroidGradleContext == null");
                    continue;
                }

                if (provider.Use == false)
                {
                    Debug.LogFormat($"[TapTap.AGCP] Provider: {provider.ModuleName} return! since : provider.Use == false");
                    continue;
                }

                if (provider.Version != VERSION)
                {
                    Debug.LogFormat($"[TapTap.AGCP] Provider: {provider.ModuleName} return! since : provider.Version != VERSION");
                    continue;
                }
                foreach (var context in provider.AndroidGradleContext)
                {
                    try
                    {
                        AndroidUtils.ProcessCustomGradleContext(context);
                    }
                    catch (Exception e)
                    {
                        Debug.LogErrorFormat(
                            $"[TapTap.AGCP] Process Custom Gradle Context Error! Error Msg:\n{e.Message}\nError Stack:\n{e.StackTrace}");
                    }
                }
            }
        }
    }
    
}
#endif