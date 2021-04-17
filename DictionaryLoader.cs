//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection;
//using System.Runtime.CompilerServices;
//using System.Threading.Tasks;

//namespace Codex_API
//{
//    public static class DictionaryLoader
//    {
//        // It is important to mark this method as NoInlining, otherwise the JIT could decide
//        // to inline it into the Main method. That could then prevent successful unloading
//        // of the plugin because some of the MethodInfo / Type / Plugin.Interface / HostAssemblyLoadContext
//        // instances may get lifetime extended beyond the point when the plugin is expected to be
//        // unloaded.
//        [MethodImpl(MethodImplOptions.NoInlining)]
//        public static void LoadDictionariesAndUnload(string assemblyPath)
//        {
//            var alc = new HostAssemblyLoadContext(assemblyPath);

//            // Create a weak reference to the AssemblyLoadContext that will allow us to detect
//            // when the unload completes.
//            WeakReference alcWeakRef = new WeakReference(alc);

//            // Load the plugin assembly into the HostAssemblyLoadContext.
//            // NOTE: the assemblyPath must be an absolute path.
//            Assembly a = alc .LoadFromAssemblyPath(assemblyPath);

//            // Get the plugin interface by calling the PluginClass.GetInterface method via reflection.
//            Type pluginType = a.GetType("Fockleyr.DictionaryModule");
//            MethodInfo loadEnglish = pluginType.GetMethod("loadEnglishDictionary", BindingFlags.Static | BindingFlags.Public);
//            object english = loadEnglish.Invoke(null, null);

//            Dictionary<string, IList<string>> dict = (Dictionary<string, IList<string>>) english.GetType().GetProperty("Translations").GetValue(english);
//            Startup.EnglishDictionary = dict;

//            MethodInfo loadManxMethod = pluginType.GetMethod("loadManxDictionary", BindingFlags.Static | BindingFlags.Public);
//            object manx = loadManxMethod.Invoke(null, null);

//            Startup.ManxDictionary = (Dictionary<string, IList<string>>)manx.GetType().GetProperty("Translations").GetValue(manx);

//            // This initiates the unload of the HostAssemblyLoadContext. The actual unloading doesn't happen
//            // right away, GC has to kick in later to collect all the stuff.
//            alc.Unload();
//            GC.Collect();

///*            // TODO: This is massive - 700+ MB for these lines? Bad structure or library overhead?
//            Startup.EnglishDictionary = DictionaryModule.loadEnglishDictionary();
//            Startup.ManxDictionary = DictionaryModule.loadManxDictionary();
//*/


//        }
//    }
//}
