#if UNITY_EDITOR && UNITY_ANDROID
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using Regex = System.Text.RegularExpressions.Regex;
using LC.Newtonsoft.Json;

namespace TapTap.AndroidDependencyResolver.Editor
{
    public static class AndroidUtils
    {
        private static string _PluginPath;
        private static string _AndroidEditorModulePath;
        
        private static string _CustomMainManifest;
        private static string _InternalMainManifest;
        private static string _InternalLauncherManifest;
        private static string _CustomLauncherManifest;
        
        private static string _InternalMainGradleTemplate;
        private static string _CustomMainGradleTemplate;
        
        private static string _InternalLauncherGradleTemplate;
        private static string _CustomLauncherGradleTemplate;
        private static string _InternalBaseGradleTemplate;
        private static string _CustomBaseGradleTemplate;
        private static string _InternalGradlePropertiesTemplate;
        private static string _CustomGradlePropertiesTemplate;

        public static void SaveProvider(string path, AndroidGradleContextProvider provider, bool assetDatabaseRefresh = true)
        {
            var serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Include;
            serializer.DefaultValueHandling = DefaultValueHandling.Include;
            using (var sw = new StreamWriter(path))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.Formatting = Formatting.Indented;
                serializer.Serialize(writer, provider);
            }
            if (assetDatabaseRefresh) AssetDatabase.Refresh();
        }
        
        public static List<AndroidGradleContextProvider> Load()
        {
            var guids = AssetDatabase.FindAssets("TapAndroidProvider");
            if (guids == null) return null;
            var providers = new List<AndroidGradleContextProvider>();
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                using (var file = File.OpenText(assetPath))
                {
                    try
                    {
                        var serializer = new JsonSerializer();
                        var gradleContextProvider = (AndroidGradleContextProvider)serializer.Deserialize(file, typeof(AndroidGradleContextProvider));
                        if (gradleContextProvider?.AndroidGradleContext == null) continue;
                        for (var index = 0; index < gradleContextProvider.AndroidGradleContext.Count; index++)
                        {
                            var gradleContext = gradleContextProvider.AndroidGradleContext[index];
                            if (gradleContext.processContent != null) gradleContext.processContent.Reverse();
                        }

                        gradleContextProvider.AndroidGradleContext.Reverse();
                        providers.Add(gradleContextProvider);
                    }
                    catch (Exception e)
                    {
                        Debug.LogErrorFormat(string.Format("[Tap::AndroidGradleProcessor] Deserialize AndroidGradleContextProvider Error! Error Msg:\n{0}\nError Stack:\n{1}", e.Message, e.StackTrace));
                    }
                }
            }
            providers.Sort((a,b)=> a.Priority.CompareTo(b.Priority));
            return providers;
        }
        
        public static void ProcessCustomGradleContext(AndroidGradleContext gradleContext)
        {
            if (gradleContext == null) return;
            // 打开 Gradle 模板
            var fileInfo = ToggleCustomTemplateFile(gradleContext.templateType, true);
            if (fileInfo == null) return;
            // 逐行解决依赖
            for (var i = 0; i < gradleContext.processContent.Count; i++)
            {
                var content = gradleContext.processContent[i];
                var appendNewline = gradleContext.processType == AndroidGradleProcessType.Insert && i == 0;
                ProcessEachContext(gradleContext, content, fileInfo, appendNewline);
            }
        }
        
        private static void ProcessEachContext(AndroidGradleContext gradleContext, string eachContext, FileInfo gradleTemplateFileInfo, bool apeendNewline = false)
        {
            // 检查 Unity 版本
            if (UnityVersionValidate(gradleContext) == false) return;
            
            var contents = File.ReadAllText(gradleTemplateFileInfo.FullName);
                
            Match match = null;
            // 寻找修改位置
            if (gradleContext.locationType == AndroidGradleLocationType.Builtin)
            {
                match = Regex.Match(contents, $"\\*\\*{gradleContext.locationParam}\\*\\*");
            }
            else if (gradleContext.locationType == AndroidGradleLocationType.Custom)
            {
                match = Regex.Match(contents, gradleContext.locationParam);
            }
            else if (gradleContext.locationType == AndroidGradleLocationType.End)
            {
            }

            var index = 0;
            if (gradleContext.locationType != AndroidGradleLocationType.End)
            {
                if (match == null || match.Success == false)
                {
                    var msg = string.Format("Couldn't find Custom Gradle Template Location! Gradle Type: {0} Location Type: {1} Location Param: {2}", gradleContext.templateType, gradleContext.locationType, gradleContext.locationParam);
                    Debug.LogWarning(msg);
                    return;
                }
                
                if (gradleContext.processType == AndroidGradleProcessType.Insert)
                    index = match.Index + match.Length;
                else if (gradleContext.processType == AndroidGradleProcessType.Replace)
                    index = match.Index;
            }
            else
            {
                index = contents.Length;
            }
            
            // 已经替换过的情况
            if (HadWrote(gradleContext, eachContext, contents, index)) return;
            // 检查是否需要引入,不能引入的原因是 gradle 已经存在 >= package 的版本
            var needImport = CheckNeedImport(gradleContext, eachContext, contents, gradleTemplateFileInfo, ref index, out string fixedContents);
            if (needImport == false) return;
            if (false == string.IsNullOrEmpty(fixedContents)) contents = fixedContents;
            
            // 替换新的修改内容
            string newContents = null;
            if (gradleContext.processType == AndroidGradleProcessType.Insert)
            {
                newContents = contents.Insert(index, string.Format("\n{0}{1}", eachContext, (apeendNewline?"\n":"")));
            }
            else if (gradleContext.processType == AndroidGradleProcessType.Replace)
            {
                var replaceContent = contents.Replace(contents.Substring(index, match.Length),
                    string.Format("{0}{1}", eachContext, apeendNewline ? "\n" : ""));
                newContents = replaceContent;
            }
                
            File.WriteAllText(gradleTemplateFileInfo.FullName, newContents);
        }
        
        private static bool UnityVersionValidate(AndroidGradleContext gradleContext)
        {
            // 版本检查
            var unityVersionCompatibleType = gradleContext.unityVersionCompatibleType;
            if (unityVersionCompatibleType == UnityVersionCompatibleType.Unity_2019_3_Above)
            {
#if !UNITY_2019_3_OR_NEWER
                return false;
#endif
            }
            if (unityVersionCompatibleType == UnityVersionCompatibleType.Unity_2019_3_Beblow)
            {
#if UNITY_2019_3_OR_NEWER
                return false;
#endif
            }

            return true;
        }

        // 是否已经写过
        private static bool HadWrote(AndroidGradleContext gradleContext, string eachContext, string contents, int insertIndex)
        {
            var hadWrote = false;
            
            if (gradleContext.locationType == AndroidGradleLocationType.End)
            {
                var text = contents.TrimEnd();
                text = text.Substring(text.Length - eachContext.Length, eachContext.Length);
                hadWrote = text == eachContext;
            }
            else
            {
                if (gradleContext.processType == AndroidGradleProcessType.Insert)
                {
                    var temp = Regex.Match(contents, string.Format("^{0}", eachContext), RegexOptions.Multiline, TimeSpan.FromSeconds(2));
                    hadWrote = temp.Success;
                }
                else if (gradleContext.processType == AndroidGradleProcessType.Replace)
                {
                    hadWrote = contents.Substring(insertIndex, eachContext.Length) == eachContext;
                }
            }

            return hadWrote;
        }
        /// <summary>
        /// 检查是否有引入包重复情况
        /// </summary>
        /// <param name="gradleContext"></param>
        /// <param name="eachContext"></param>
        /// <param name="contents"></param>
        /// <param name="gradleTemplateFileInfo"></param>
        /// <param name="insertIndex"></param>
        /// <param name="newContents"></param>
        /// <returns>是否需要继续引入包</returns>
        private static bool CheckNeedImport(AndroidGradleContext gradleContext, string eachContext, string contents, FileInfo gradleTemplateFileInfo, ref int insertIndex, out string newContents)
        {
            newContents = null;
            var headerPattern = "^.*";
            var packageNamePattern =
                "\\s*([-\\w]{0,62}\\.\\s*)+[-\\w]{0,62}\\s*:\\s*[-\\w]{0,62}\\s*";
            var versionNumberPattern =
                "\\s*(\\d{1,3}\\.\\s*){1,3}\\d{1,3}\\s*";
            var packageAllPattern = headerPattern + "['\"]" + packageNamePattern + ":" + versionNumberPattern + "['\"]";

            var importMatch = Regex.Match(eachContext, packageAllPattern);
            if (string.IsNullOrEmpty(importMatch.Value)) return true;

            var importPkgNameMatch = Regex.Match(importMatch.Value, packageNamePattern);
            if (string.IsNullOrEmpty(importPkgNameMatch.Value)) return true;
            
            try
            {
                var pattern = headerPattern + "['\"]" + importPkgNameMatch.Value + ":" + versionNumberPattern + "['\"]";
                // 已经存在的 gradle 文件有没有对应的 package 内容
                var builtinMatches = Regex.Matches(contents, pattern, RegexOptions.Multiline, TimeSpan.FromSeconds(2));
                if (builtinMatches.Count == 0) return true;

                var importVersionMatch = Regex.Match(importMatch.Value, versionNumberPattern);
                var importVersion = new Version(importVersionMatch.Value);

                foreach (Match builtinMatch in builtinMatches)
                {
                    // 检查前面是否已经有了注释, -2 是防止末尾的\n
                    var matchEndIdx = builtinMatch.Index + builtinMatch.Length - 2;
                    var headerIdx = contents.LastIndexOf('\n', matchEndIdx);
                    var commentIdx = contents.LastIndexOf("//", matchEndIdx, matchEndIdx - headerIdx, StringComparison.Ordinal);
                    if (commentIdx >= 0) continue;
                    
                    // 版本相同不添加
                    var builtinVersionMatch = Regex.Match(builtinMatch.Value, versionNumberPattern);
                    if (builtinVersionMatch.Success == false) continue;
                    var builtinVersion = new Version(builtinVersionMatch.Value);
                    if (importVersion == builtinVersion) return false;
                    // 如果是替换的话,不需要添加注释
                    if (gradleContext.processType == AndroidGradleProcessType.Replace) return true;
                    Debug.LogWarningFormat(string.Format("[TapTap:AndroidGradlePostProcessor] Detect Package Collision! Gradle File: {0} Process Content: {1} Collision Content: {2}", gradleContext.templateType, eachContext, builtinMatch.Value));
                    // 引入版本更低,那就不用引入了
                    if (importVersion < builtinVersion) return false;
                    // 引入版本更高,把已经存在的版本注释掉
                    newContents = contents.Insert(headerIdx + 1, "//");
                    File.WriteAllText(gradleTemplateFileInfo.FullName, newContents);
                    if (gradleContext.locationType == AndroidGradleLocationType.End) insertIndex = newContents.Length;
                    return true;
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

            return true;
        }
        
        public static bool HaveCustomTemplateFile(CustomTemplateType templateType)
        {
            if (string.IsNullOrEmpty(_AndroidEditorModulePath))
            {
                Init();
            }

            GetTemplatePath(templateType, out _, out string customPath);

            return File.Exists(customPath);
        }
        
        private static FileInfo ToggleCustomTemplateFile(CustomTemplateType templateType, bool create)
        {
            if (string.IsNullOrEmpty(_AndroidEditorModulePath))
            {
                Init();
            }

            GetTemplatePath(templateType, out string internalPath, out string customPath);

            return ToggleAsset(internalPath, customPath, create);
        }
        
        private static FileInfo ToggleAsset(string originalFile, string assetFile, bool create)
        {
            if (string.IsNullOrEmpty(originalFile) || string.IsNullOrEmpty(assetFile)) return null;
            var str = assetFile + ".DISABLED";
            var directoryName = Path.GetDirectoryName(assetFile);
            if (create)
            {
                if (File.Exists(str))
                {
                    AssetDatabase.MoveAsset(str, assetFile);
                    return new FileInfo(assetFile);
                }

                if (File.Exists(assetFile)) return new FileInfo(assetFile);
                if (!Directory.Exists(directoryName))
                    Directory.CreateDirectory(directoryName);
                if (File.Exists(originalFile))
                    File.Copy(originalFile, assetFile);
                else
                    File.Create(assetFile).Dispose();
                AssetDatabase.Refresh();
                return new FileInfo(assetFile);
            }
            else
            {
                AssetDatabase.MoveAsset(assetFile, str);
                return new FileInfo(str);
            }
        }

        private static void GetTemplatePath(CustomTemplateType templateType, out string internalPath,
            out string customPath)
        {
            internalPath = null;
            customPath = null;
            switch (templateType)
            {
                case CustomTemplateType.AndroidManifest:
                    internalPath = _InternalMainManifest;
                    customPath = _CustomMainManifest;
                    break;
                case CustomTemplateType.BaseGradle:
                    internalPath = _InternalBaseGradleTemplate;
                    customPath = _CustomBaseGradleTemplate;
                    break;
                case CustomTemplateType.GradleProperties:
                    internalPath = _InternalGradlePropertiesTemplate;
                    customPath = _CustomGradlePropertiesTemplate;
                    break;
                case CustomTemplateType.LauncherGradle:
                    internalPath = _InternalLauncherGradleTemplate;
                    customPath = _CustomLauncherGradleTemplate;
                    break;
                case CustomTemplateType.LauncherManifest:
                    internalPath = _InternalLauncherManifest;
                    customPath = _CustomLauncherManifest;
                    break;
                case CustomTemplateType.UnityMainGradle:
                    internalPath = _InternalMainGradleTemplate;
                    customPath = _CustomMainGradleTemplate;
                    break;
            }
        }
        
        private static void Init()
        {
#if UNITY_2019_3_OR_NEWER
             _AndroidEditorModulePath =
                    BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None);
#else
            var buildPipelineType = typeof(BuildPipeline);
            var methodInfo = buildPipelineType.GetMethod("GetBuildToolsDirectory", BindingFlags.Static | BindingFlags.NonPublic);
            var temp = methodInfo.Invoke(null, new object[] { BuildTarget.Android }) as string;
            _AndroidEditorModulePath = temp.Substring(0, temp.LastIndexOf('/'));
#endif
            _PluginPath = Path.Combine("Assets", "Plugins", "Android");
            
#if UNITY_2019_3_OR_NEWER
            var builtinAPK = Path.Combine(BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None), "Apk");
#else
            var builtinAPK = Path.Combine(_AndroidEditorModulePath, "Apk");
#endif
            _InternalMainManifest = Path.Combine(builtinAPK, "UnityManifest.xml");
            _CustomMainManifest = Path.Combine(_PluginPath, "AndroidManifest.xml");
            
#if UNITY_2019_3_OR_NEWER
            _InternalLauncherManifest = Path.Combine(builtinAPK, "LauncherManifest.xml");
            _CustomLauncherManifest = Path.Combine(_PluginPath, "LauncherManifest.xml");
#else
            _InternalLauncherManifest = Path.Combine(builtinAPK, "UnityManifest.xml");
            _CustomLauncherManifest = Path.Combine(_PluginPath, "AndroidManifest.xml");
#endif
            
            var builtinGradleTemplates = Path.Combine(_AndroidEditorModulePath, "Tools", "GradleTemplates");
            _InternalMainGradleTemplate = Path.Combine(builtinGradleTemplates, "mainTemplate.gradle");
            _CustomMainGradleTemplate = Path.Combine(_PluginPath, "mainTemplate.gradle");
            
#if UNITY_2019_3_OR_NEWER
            _InternalLauncherGradleTemplate = Path.Combine(builtinGradleTemplates, "launcherTemplate.gradle");
            _CustomLauncherGradleTemplate = Path.Combine(_PluginPath, "launcherTemplate.gradle");
#else
            _InternalLauncherGradleTemplate = Path.Combine(builtinGradleTemplates, "mainTemplate.gradle");
            _CustomLauncherGradleTemplate = Path.Combine(_PluginPath, "mainTemplate.gradle");
#endif
            
#if UNITY_2019_3_OR_NEWER
            _InternalBaseGradleTemplate = Path.Combine(builtinGradleTemplates, "baseProjectTemplate.gradle");
            _CustomBaseGradleTemplate = Path.Combine(_PluginPath, "baseProjectTemplate.gradle");
#else
            _InternalBaseGradleTemplate = Path.Combine(builtinGradleTemplates, "mainTemplate.gradle");
            _CustomBaseGradleTemplate = Path.Combine(_PluginPath, "mainTemplate.gradle");
#endif
            
#if UNITY_2019_3_OR_NEWER
            _InternalGradlePropertiesTemplate = Path.Combine(builtinGradleTemplates, "gradleTemplate.properties");
            _CustomGradlePropertiesTemplate = Path.Combine(_PluginPath, "gradleTemplate.properties");
#endif
            
        }
        
        #region test code
        // [MenuItem("Android/Test")]
        // private static void Test()
        // {
        //     var providers = Load();
        //     if (providers == null) return;
        //     
        //     foreach (var provider in providers)
        //     {
        //         if (provider.AndroidGradleContext == null) continue;
        //         foreach (var context in provider.AndroidGradleContext)
        //         {
        //             try
        //             {
        //                 ProcessCustomGradleContext(context);
        //             }
        //             catch (Exception e)
        //             {
        //                 Debug.LogErrorFormat($"[Tap::AndroidGradleProcessor] Process Custom Gradle Context Error! Error Msg:\n{e.Message}\nError Stack:\n{e.StackTrace}");
        //             }
        //         }
        //     }
        // }
                
        // [MenuItem("Android/Regex")]
        // private static void Regex()
        // {
        //     string packageNamePatter =
        //         "\\s*([-\\w]{0,62}\\.\\s*)+[-\\w]{0,62}\\s*:\\s*[-\\w]{0,62}\\s*";
        //     string versionNumberPatter =
        //         "\\s*(\\d{1,3}\\.\\s*){1,3}\\d{1,3}\\s*";
        //     string headerPatter = "^\\s*(\\/){0}\\s*\\w+\\s";
        //     string packageAllPattern = headerPatter + "['\"]" + packageNamePatter + ":" + versionNumberPatter + "['\"]";
        //         // "^\\s*(\\/){0}\\s*\\w+\\s['\"]\\s*([-\\w]{0,62}\\.\\s*)+[-\\w]{0,62}\\s*:\\s*[-\\w]{0,62}\\s*:\\s*(\\d{1,3}\\.\\s*){1,3}\\d{1,3}\\s*['\"]";
        //     
        //     var importMatch =
        //         Regex.Match(
        //             $"    implementation 'com.google.firebase:firebase-core:18.0.0'", packageAllPattern);
        //     Debug.LogFormat($"importmatch result: {importMatch.Success}");
        //     var pkgNameMatch =
        //         Regex.Match(
        //             $"//    implementation 'com.google.firebase:firebase-core:18.0.0'", packageNamePatter);
        //     Debug.LogFormat($"importmatch result: {pkgNameMatch.Success}");
        //     var verNumberMatch =
        //         Regex.Match(
        //             $"//    implementation 'com.google.firebase:firebase-core:18.0.0'", versionNumberPatter);
        //     Debug.LogFormat($"importmatch result: {verNumberMatch.Success}");
        //
        //     var providers = Load();
        //     if (providers == null) return;
        //     providers.Sort((a,b)=> a.Priority.CompareTo(b.Priority));
        //     foreach (var provider in providers)
        //     {
        //         if (provider.AndroidGradleContext == null) continue;
        //         foreach (var context in provider.AndroidGradleContext)
        //         {
        //             var fileInfo = ToggleCustomTemplateFile(context.templateType, true);
        //             if (fileInfo == null) return;
        //             var contents = File.ReadAllText(fileInfo.FullName);
        //             foreach (var processContent in context.processContent)
        //             {
        //                 var debug = processContent.Contains("    implementation 'com.google.firebase:firebase-core:18.0.0'");
        //                 if (debug == false) continue;
        //                 // 已经替换过的情况
        //                 var importPkgNameMatch = "com.google.firebase:firebase-core";
        //                 var pattern = "^\\s*(\\/){0}\\s*\\w+\\s['\"]" + importPkgNameMatch + ":" + versionNumberPatter +
        //                               "['\"]";
        //                 var builtinMatches = System.Text.RegularExpressions.
        //                     Regex.Matches(contents, pattern, RegexOptions.Multiline);
        //                 Debug.LogFormat($"FileInfo builtinMatches Result Count: {builtinMatches.Count}");
        //                 var normalMatches = System.Text.RegularExpressions.
        //                     Regex.Matches(contents, pattern, RegexOptions.Multiline);
        //                 Debug.LogFormat($"FileInfo normalMatches Result Count: {normalMatches.Count}");
        //                 
        //             }
        //         }
        //     }
        //
        // }
        #endregion
    }
}
#endif