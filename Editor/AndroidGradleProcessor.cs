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
            var i = (int)CustomTemplateType.AndroidManifest;
            for (; i <= (int)CustomTemplateType.GradleProperties; i++)
            {
                var haveCustomGradleTemplate = AndroidUtils.HaveCustomTemplateFile((CustomTemplateType)i);
                gardleTemplateToggleRecord.Add((CustomTemplateType)i, haveCustomGradleTemplate);
            }
            
            var providers = AndroidUtils.Load();
            if (providers == null) return;

            i = 0;
            for (; i < providers.Count; i++)
            {
                var provider = providers[i];
                if (provider.AndroidGradleContext == null) continue;
                foreach (var context in provider.AndroidGradleContext)
                {
                    try
                    {
                        AndroidUtils.ProcessCustomGradleContext(context);
                    }
                    catch (Exception e)
                    {
                        Debug.LogErrorFormat(
                            $"[Tap::AndroidGradleProcessor] Process Custom Gradle Context Error! Error Msg:\n{e.Message}\nError Stack:\n{e.StackTrace}");
                    }
                }
            }
        }
    }
    
}
#endif