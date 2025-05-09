﻿using Comfort.Common;
using EFT;
using HarmonyLib;
using Paulov.Bepinex.Framework;
using Paulov.Bepinex.Framework.Patches;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Paulov.Tarkov.Local.Patches;
//public class BotLoadingPatch : NullHarmonyPatch
public class BotLoadingPatch : NullPaulovHarmonyPatch
//public class BotLoadingPatch : NullHarmonyPatch, Paulov.Bepinex.Framework.IPaulovHarmonyPatch
{
    private static MethodInfo methodPrepareToLoadBackend;
    private static MethodInfo methodGetNewProfile;
    public BotLoadingPatch()
    {
    }

    public override MethodBase GetMethodToPatch()
    {
        Plugin.Logger.LogDebug($"{nameof(BotLoadingPatch)}.GetMethodToPatch");

        var method =
            GetExpectedMethod(
            Plugin
            .EftTypes
            .Where(x => !x.IsAbstract)
            .Where(x => !x.IsInterface)
            .Where(x => x.GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(y => y.Name == "CreateProfile"))
            .First(x => GetExpectedMethod(x) != null)
            );

        Plugin.Logger.LogDebug($"{nameof(BotLoadingPatch)}.GetMethodToPatch:{method.DeclaringType}.{method}");

        return method;

    }

    private MethodInfo GetExpectedMethod(Type t)
    {
        return !t.GetMembers().Any() ? null :
            t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(x => x.Name == "CreateProfile");
    }

    public override HarmonyMethod GetPrefixMethod()
    {
        return new HarmonyMethod(this.GetType().GetMethod(nameof(PrefixMethod), BindingFlags.Public | BindingFlags.Static));
    }
    public static bool PrefixMethod(ref Task<Profile> __result, BotsPresets __instance, List<Profile> ___list_0, object data, ref bool withDelete)
    {
        //var taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        //var continuation = new BundleLoader(taskScheduler);

        //DebugBotData.Instance.useThisData = false;
        //DebugBotData.Instance.localProfiles = false;

        //if (methodGetNewProfile == null)
        //    methodGetNewProfile = ReflectionHelpers
        //        .GetAllMethodsForType(__instance.GetType(), false, true)
        //        .Single(x => x.Name == "GetNewProfile" && x.GetParameters().Length == 2);


        //if (methodPrepareToLoadBackend == null)
        //    methodPrepareToLoadBackend = ReflectionHelpers.GetAllMethodsForType(data.GetType()).Single(x => x.Name == "PrepareToLoadBackend" && x.GetParameters().Length > 0);

        //var sourceWaves = methodPrepareToLoadBackend.Invoke(data, new object[1] { 1 }) as WaveInfo[];
        //var numberOfWaves = sourceWaves.Length;
        //var source = sourceWaves.Take(new Random().Next(1, numberOfWaves)).ToList();

        ////var taskAwaiter = (Task<Profile>)null;
        ////var resultOfLoadBots = Plugin.BackEndSession2.LoadBots(source);
        ////taskAwaiter = resultOfLoadBots.ContinueWith(GetRandomResult, taskScheduler);

        ////__result = taskAwaiter.ContinueWith(continuation.LoadBundles, taskScheduler).Unwrap();
        ////return false;
        //return true;



        try
        {
            __instance.GetNewProfile(data as BotCreationDataClass, true);
        }
        catch (Exception)
        {
            Plugin.Logger.LogError("Error in GetNewProfile");
            return true;
        }

        try
        {

            if (methodPrepareToLoadBackend == null)
                methodPrepareToLoadBackend = ReflectionHelpers.GetAllMethodsForType(data.GetType()).Single(x => x.Name == "PrepareToLoadBackend" && x.GetParameters().Length > 0);

            var sourceWaves = methodPrepareToLoadBackend.Invoke(data, new object[1] { 1 }) as WaveInfo[];

            var taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            var taskAwaiter = (Task<Profile>)null;
            taskAwaiter = Plugin.BackEndSession2.LoadBots(sourceWaves.ToList()).ContinueWith(GetRandomResult, taskScheduler);

            var continuation = new BundleLoader(taskScheduler);
            __result = taskAwaiter.ContinueWith(continuation.LoadBundles, taskScheduler).Unwrap();

            return false;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError(ex);
            return true;
        }

    }
    private static Profile GetRandomResult(Task<Profile[]> task)
    {
        var result = task.Result;
        var length = task.Result.Length;
        var v = result[0];//.PickRandom();
        Plugin.Logger.LogDebug($"Loading {v.Info.Nickname} profile. Role: {v.Info.Settings.Role} Side: {v.Side}");
        return v;
    }

    public struct BundleLoader
    {
        private Profile _profile;
        TaskScheduler TaskScheduler { get; }

        public BundleLoader(TaskScheduler taskScheduler)
        {
            _profile = null;
            TaskScheduler = taskScheduler;
        }

        public Task<Profile> LoadBundles(Task<Profile> task)
        {
            _profile = task.Result;

            var loadTask = Singleton<PoolManager>.Instance.LoadBundlesAndCreatePools(
                PoolManager.PoolsCategory.Raid,
                PoolManager.AssemblyType.Local,
                _profile.GetAllPrefabPaths(false).Where(x => !x.IsNullOrEmpty()).ToArray(),
                JobPriority.General,
                null,
                default(CancellationToken));

            return loadTask.ContinueWith(GetProfile, TaskScheduler);
        }

        private Profile GetProfile(Task task)
        {
            return _profile;
        }
    }
}
