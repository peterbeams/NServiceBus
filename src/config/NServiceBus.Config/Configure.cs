﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using NServiceBus.Logging;
using NServiceBus.Config;
using NServiceBus.Config.ConfigurationSource;
using NServiceBus.ObjectBuilder;
using System.IO;
using System.Reflection;

namespace NServiceBus
{
    using Config.Conventions;

    /// <summary>
    /// Central configuration entry point for NServiceBus.
    /// </summary>
    public class Configure
    {
        static Configure()
        {
            ConfigurationSource = new DefaultConfigurationSource();
        }

        /// <summary>
        /// Provides static access to the configuration object.
        /// </summary>
        public static Configure Instance
        {
            get
            {
                //we can't check for null here since that would break the way we do extension methods (the must be on a instance)
                return instance;
            }
        }

        /// <summary>
        /// True if any of the Configure.With() has been called
        /// </summary>
        /// <returns></returns>
        public static bool WithHasBeenCalled()
        {
            return instance != null;
        }

        /// <summary>
        /// Event raised when configuration is complete
        /// </summary>
        public static event Action ConfigurationComplete;

        /// <summary>
        /// Gets/sets the builder.
        /// Setting the builder should only be done by NServiceBus framework code.
        /// </summary>
        public IBuilder Builder
        {
            get
            {
                if (builder == null)
                    throw new InvalidOperationException("You can't access Configure.Instance.Builder before calling specifying a builder. Please add a call to Configure.DefaultBuilder() or any of the other supported builders to set one up");

                return builder;

            }
            set { builder = value; }
        }

        /// <summary>
        /// True if a builder has been defined
        /// </summary>
        /// <returns></returns>
        public static bool BuilderIsConfigured()
        {
            if (!WithHasBeenCalled())
                return false;

            return Instance.HasBuilder();
        }

        bool HasBuilder()
        {
            return builder != null && configurer != null;
        }


        IBuilder builder;

        static bool initialized { get; set; }

        /// <summary>
        /// Gets/sets the configuration source to be used by NServiceBus.
        /// </summary>
        public static IConfigurationSource ConfigurationSource { get; set; }

        /// <summary>
        /// Sets the current configuration source
        /// </summary>
        /// <param name="configurationSource"></param>
        /// <returns></returns>
        public Configure CustomConfigurationSource(IConfigurationSource configurationSource)
        {
            ConfigurationSource = configurationSource;
            return this;
        }

        /// <summary>
        /// Gets/sets the object used to configure components.
        /// This object should eventually reference the same container as the Builder.
        /// </summary>
        public IConfigureComponents Configurer
        {
            get
            {
                if (configurer == null)
                    throw new InvalidOperationException("You can't access Configure.Instance.Configurer before calling specifying a builder. Please add a call to Configure.DefaultBuilder() or any of the other supported builders to set one up");

                return configurer;
            }
            set
            {
                bool invoke = configurer == null;
                configurer = value;
                WireUpConfigSectionOverrides();
                if (invoke)
                    InvokeBeforeConfigurationInitializers();
            }
        }

        private void InvokeBeforeConfigurationInitializers()
        {
            TypesToScan.Where(t => typeof(IWantToRunBeforeConfiguration).IsAssignableFrom(t) && !(t.IsAbstract || t.IsInterface))
                .ToList().ForEach(t =>
                {
                    var ini = (IWantToRunBeforeConfiguration)Activator.CreateInstance(t);
                    ini.Init();
                });
        }

        private IConfigureComponents configurer;

        void WireUpConfigSectionOverrides()
        {
            TypesToScan
                .Where(t => t.GetInterfaces().Any(IsGenericConfigSource))
                .ToList().ForEach(t => configurer.ConfigureComponent(t, DependencyLifecycle.InstancePerCall));
        }

        /// <summary>
        /// Protected constructor to enable creation only via the With method.
        /// </summary>
        protected Configure()
        {
        }

        /// <summary>
        /// Creates a new configuration object scanning assemblies
        /// in the regular runtime directory.
        /// </summary>
        /// <returns></returns>
        public static Configure With()
        {
            if (HttpRuntime.AppDomainAppId != null)
                return With(HttpRuntime.BinDirectory);

            return With(AppDomain.CurrentDomain.BaseDirectory);
        }

        /// <summary>
        /// Configures NServiceBus to scan for assemblies 
        /// in the relevant web directory instead of regular
        /// runtime directory.
        /// </summary>
        /// <returns></returns>
        [ObsoleteEx(Replacement = "With()", TreatAsErrorFromVersion = "4.0", RemoveInVersion = "5.0")]
        public static Configure WithWeb()
        {
            return With();
        }

        /// <summary>
        /// Configures NServiceBus to scan for assemblies
        /// in the given directory rather than the regular
        /// runtime directory.
        /// </summary>
        /// <param name="probeDirectory"></param>
        /// <returns></returns>
        public static Configure With(string probeDirectory)
        {
            lastProbeDirectory = probeDirectory;
            return With(GetAssembliesInDirectory(probeDirectory));
        }

        /// <summary>
        /// Configures NServiceBus to use the types found in the given assemblies.
        /// </summary>
        /// <param name="assemblies"></param>
        /// <returns></returns>
        public static Configure With(IEnumerable<Assembly> assemblies)
        {
            return With(assemblies.ToArray());
        }

        /// <summary>
        /// Configures nServiceBus to scan the given assemblies only.
        /// </summary>
        /// <param name="assemblies"></param>
        /// <returns></returns>
        public static Configure With(params Assembly[] assemblies)
        {
            var types = GetAllowedTypes(assemblies);

            return With(types);
        }

        /// <summary>
        /// Configures nServiceBus to scan the given types.
        /// </summary>
        /// <param name="typesToScan"></param>
        /// <returns></returns>
        public static Configure With(IEnumerable<Type> typesToScan)
        {
            if (instance == null)
                instance = new Configure();

            TypesToScan = typesToScan.Union(GetAllowedTypes(Assembly.GetExecutingAssembly()));

            if (HttpRuntime.AppDomainAppId == null)
            {
                var hostPath = Path.Combine(lastProbeDirectory ?? AppDomain.CurrentDomain.BaseDirectory, "NServiceBus.Host.exe");
                if (File.Exists(hostPath))
                {
                    TypesToScan = TypesToScan.Union(GetAllowedTypes(Assembly.LoadFrom(hostPath)));
                }
            }

            Logger.DebugFormat("Number of types to scan: {0}", TypesToScan.Count());

            return instance;
        }

        /// <summary>
        /// Run a custom action at configuration time - useful for performing additional configuration not exposed by the fluent interface.
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public Configure RunCustomAction(Action action)
        {
            action();

            return this;
        }

        /// <summary>
        /// Provides an instance to a startable bus.
        /// </summary>
        /// <returns></returns>
        public IStartableBus CreateBus()
        {
            Initialize();

            if (Configurer.HasComponent<IStartableBus>())
                return Builder.Build<IStartableBus>();

            return null;
        }


        /// <summary>
        /// Finalizes the configuration by invoking all initializers.
        /// </summary>
        public void Initialize()
        {
            if (initialized)
                return;

            ForAllTypes<IWantToRunWhenConfigurationIsComplete>(t => Configurer.ConfigureComponent(t, DependencyLifecycle.InstancePerCall));
            
            ForAllTypes<IWantToRunWhenBusStartsAndStops>(t => Configurer.ConfigureComponent(t, DependencyLifecycle.InstancePerCall));

            ForAllTypes<IWantToRunBeforeConfiguration>(t =>
            {
                var ini = (IWantToRunBeforeConfiguration)Activator.CreateInstance(t);
                ini.Init();
            });

            ForAllTypes<Config.INeedInitialization>(t =>
            {
                var ini = (Config.INeedInitialization)Activator.CreateInstance(t);
                ini.Init();
            });

            ForAllTypes<INeedInitialization>(t =>
            {
                var ini = (INeedInitialization)Activator.CreateInstance(t);
                ini.Init();
            });

            ForAllTypes<IWantToRunBeforeConfigurationIsFinalized>(t =>
            {
                var ini = (IWantToRunBeforeConfigurationIsFinalized)Activator.CreateInstance(t);
                ini.Run();
            });

            initialized = true;

            if (ConfigurationComplete != null)
                ConfigurationComplete();

            Builder.BuildAll<IWantToRunWhenConfigurationIsComplete>()
                .ToList().ForEach(o => o.Run());
        }

        /// <summary>
        /// Applies the given action to all the scanned types that can be assigned to T 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action"></param>
        public void ForAllTypes<T>(Action<Type> action) where T : class
        {
            TypesToScan.Where(t => typeof(T).IsAssignableFrom(t) && !(t.IsAbstract || t.IsInterface))
              .ToList().ForEach(action);
        }

        /// <summary>
        /// Returns types in assemblies found in the current directory.
        /// </summary>
        public static IEnumerable<Type> TypesToScan { get; private set; }

        /// <summary>
        /// Returns the requested config section using the current configuration source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetConfigSection<T>() where T : class,new()
        {
            if (instance != null)
                if (instance.configurer != null)
                    if (instance.configurer.HasComponent<IProvideConfiguration<T>>())
                    {
                        var configSource = instance.Builder.Build<IProvideConfiguration<T>>();
                        if (configSource != null)
                            return configSource.GetConfiguration();
                    }

            return ConfigurationSource.GetConfiguration<T>();
        }

        /// <summary>
        /// Load and return all assemblies in the given directory except the given ones to exclude
        /// </summary>
        /// <param name="path"></param>
        /// <param name="assembliesToSkip">The exclude must either be the full</param>
        /// <returns></returns>
        public static IEnumerable<Assembly> GetAssembliesInDirectory(string path, params string[] assembliesToSkip)
        {
            Predicate<string> exclude = 
                f => assembliesToSkip.Any(skip => destillLowerAssemblyName(skip) == f);

            return FindAssemblies(path, false, null, exclude);
        }

        static string destillLowerAssemblyName(string assemblyOrFileName)
        {
            var lowerAssemblyName = assemblyOrFileName.ToLowerInvariant();
            if (lowerAssemblyName.EndsWith(".dll"))
                lowerAssemblyName = lowerAssemblyName.Substring(0, lowerAssemblyName.Length - 4);
            return lowerAssemblyName;
        }


        /// <summary>
        /// Find and return all assemblies in the given directory and the current appdomain 
        /// filtered to <paramref name="includeAssemblyNames"/>, if given, 
        /// but except <paramref name="excludeAssemblyNames"/>
        /// </summary>
        /// <param name="path">Directory to search in.</param>
        /// <param name="includeAppDomainAssemblies">Shortcut Assembly.Load by instead using yet loaded assemblies.</param>
        /// <param name="includeAssemblyNames">All, if <c>null</c></param>
        /// <param name="excludeAssemblyNames">None, if <c>null</c></param>
        /// <returns></returns>
        public static IEnumerable<Assembly> FindAssemblies(string path, bool includeAppDomainAssemblies, Predicate<string> includeAssemblyNames, Predicate<string> excludeAssemblyNames)
        {
            var possiblyChangedExcludePredicate = excludeAssemblyNames;
            if (includeAppDomainAssemblies)
            {
                var yetLoadedMatchingAssemblies =
                    (from assembly in AppDomain.CurrentDomain.GetAssemblies()
                     where IsIncluded(assembly.GetName().Name, includeAssemblyNames, excludeAssemblyNames)
                    select assembly).ToArray();

                foreach (var a in yetLoadedMatchingAssemblies)
                {
                    yield return a;
                }

                Predicate<string> additionalExclude =
                    name => yetLoadedMatchingAssemblies.Any(
                        a => IsMatch(a.GetName().Name, name));

                if (possiblyChangedExcludePredicate != null)
                    possiblyChangedExcludePredicate = name => additionalExclude(name) || excludeAssemblyNames(name);
                else
                {
                    possiblyChangedExcludePredicate = additionalExclude;
                }
            }

            foreach (var a in GetAssembliesInDirectoryWithExtension(path, "*.exe", includeAssemblyNames, possiblyChangedExcludePredicate))
                yield return a;
            foreach (var a in GetAssembliesInDirectoryWithExtension(path, "*.dll", includeAssemblyNames, possiblyChangedExcludePredicate))
                yield return a;
        }

        /// <summary>
        /// Initialized the bus in send only mode
        /// </summary>
        /// <returns></returns>
        public IBus SendOnly()
        {
            SendOnlyMode = true;
            Initialize();

            return Builder.Build<IBus>();
        }

        /// <summary>
        /// True if this endpoint is operating in send only mode
        /// </summary>
        public static bool SendOnlyMode { get; private set; }

        /// <summary>
        /// The name of this endpoint
        /// </summary>
        public static string EndpointName
        {
            get { return GetEndpointNameAction(); }
        }

        /// <summary>
        /// The function used to get the name of this endpoint
        /// </summary>
        public static Func<string> GetEndpointNameAction = () => DefaultEndpointName.Get();

        private static IEnumerable<Type> GetAllowedTypes(params Assembly[] assemblies)
        {
            var types = new List<Type>();
            Array.ForEach(
                assemblies,
                a =>
                {
                    try
                    {
                        types.AddRange(a.GetTypes()
                                           .Where(t => !t.IsValueType &&
                                                       (t.FullName == null ||
                                                        !defaultTypeExclusions.Union(defaultAssemblyExclusions).Any(
                                                            exclusion => IsMatch(exclusion, t.FullName)))));
                    }
                    catch (ReflectionTypeLoadException e)
                    {
                        var sb = new StringBuilder();
                        sb.Append(string.Format("Could not scan assembly: {0}. Exception message {1}.", a.FullName, e));
                        if (e.LoaderExceptions.Any())
                        {
                            sb.Append(Environment.NewLine + "Scanned type errors: ");
                            foreach (var ex in e.LoaderExceptions)
                                sb.Append(Environment.NewLine + ex.Message);
                        }
                        LogManager.GetLogger(typeof(Configure)).Warn(sb.ToString());
                        //intentionally swallow exception
                    }
                });
            return types;
        }

        /// <summary>
        /// The function used to get the name of this endpoint
        /// </summary>
        public static Func<FileInfo, Assembly> LoadAssembly = s => Assembly.LoadFrom(s.FullName);

        private static IEnumerable<Assembly> GetAssembliesInDirectoryWithExtension(string path, string extension, Predicate<string> includeAssemblyNames, Predicate<string> excludeAssemblyNames)
        {
            var result = new List<Assembly>();

            foreach (FileInfo file in new DirectoryInfo(path).GetFiles(extension, SearchOption.AllDirectories))
            {
                try
                {
                    if (IsIncluded(file.Name, includeAssemblyNames, excludeAssemblyNames))
                    {
                        var loadAssembly = LoadAssembly(file);
                        if (loadAssembly != null)
                        {
                            result.Add(loadAssembly);
                        }
                    }
                }
                catch (BadImageFormatException bif)
                {
                    if (bif.FileName.ToLower().Contains("system.data.sqlite.dll"))
                        throw new BadImageFormatException(
                            "You've installed the wrong version of System.Data.SQLite.dll on this machine. If this machine is x86, this dll should be roughly 800KB. If this machine is x64, this dll should be roughly 1MB. You can find the x86 file under /binaries and the x64 version under /binaries/x64. *If you're running the samples, a quick fix would be to copy the file from /binaries/x64 over the file in /binaries - you should 'clean' your solution and rebuild after.",
                            bif.FileName, bif);

                    throw new InvalidOperationException(
                        "Could not load " + file.FullName +
                        ". Consider using 'Configure.With(AllAssemblies.Except(\"" + file.Name + "\"))' to tell NServiceBus not to load this file.",
                        bif);
                }
            }

            return result;
        }

        private static bool IsIncluded(string assemblyNameOrFileName, Predicate<string> includeAssemblyNames, Predicate<string> excludeAssemblyNames)
        {
            
            if (includeAssemblyNames != null 
                && !includeAssemblyNames(assemblyNameOrFileName)
                && !defaultAssemblyInclusionOverrides.Any(s => IsMatch(s, assemblyNameOrFileName)))
                return false;

            if (defaultAssemblyExclusions.Any(exclusion => IsMatch(exclusion, assemblyNameOrFileName)))
                return false;

            if (excludeAssemblyNames != null && excludeAssemblyNames(assemblyNameOrFileName))
                return false;

            return true;
        }

        /// <summary>
        /// Check, if an assembly name matches the given expression.
        /// </summary>
        /// <param name="expression">
        ///  <c>Wildcard.</c> matches 'Wildcard' and Assemblies starting with 'Wildcard.';
        ///  <c>Exact</c> matches only "Exact". Casing is generally ignored.
        /// </param>
        /// <param name="scopedNameOrFileName">The name or file name of the assembly, a full type name or namespace.</param>
        /// <returns></returns>
        public static bool IsMatch(string expression, string scopedNameOrFileName)
        {
            if (destillLowerAssemblyName(scopedNameOrFileName).StartsWith(expression.ToLower()))
                return true;
            if (destillLowerAssemblyName(expression).TrimEnd('.') == destillLowerAssemblyName(scopedNameOrFileName))
                return true;

            return false;
        }

        private static bool IsGenericConfigSource(Type t)
        {
            if (!t.IsGenericType)
                return false;

            var args = t.GetGenericArguments();
            if (args.Length != 1)
                return false;  

            return typeof(IProvideConfiguration<>).MakeGenericType(args).IsAssignableFrom(t);
        }

        static string lastProbeDirectory;
        static Configure instance;
        static ILog Logger = LogManager.GetLogger("NServiceBus.Config");
        
        static readonly IEnumerable<string> defaultAssemblyInclusionOverrides = new[] { "nservicebus." };

        // TODO: rename to defaultAssemblyAndNamespaceExclusions
        static readonly IEnumerable<string> defaultAssemblyExclusions 
            = new[]
              {

                  "system.", 
                  
                  // NSB Build-Dependencies
                  "nunit.", "pnunit.", "rhino.mocks.","XsdGenerator.",
                 
                  // NSB OSS Dependencies
                  "rhino.licensing.", "bouncycastle.crypto",
                  "magnum.", "interop.", "nlog.", "newtonsoft.json.",
                  "common.logging.", "topshelf.",
                  "Autofac.", "log4net.","nhibernate.", 

                  // Raven
                  "raven.server", "raven.client", "raven.munin.",
                  "raven.storage.", "raven.abstractions.", "raven.database",
                  "esent.interop", "asyncctplibrary.", "lucene.net.", 
                  "icsharpcode.nrefactory", "spatial4n.core"
              };

        // TODO: rename to additionalTypeExclusions 
        private static readonly IEnumerable<string> defaultTypeExclusions
            = new string[]
              {
                  // defaultAssemblyExclusions will merged inn; specify additional ones here 
              };
    }
}
