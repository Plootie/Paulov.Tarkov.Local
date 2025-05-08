using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using EFT;
using HarmonyLib;
using Paulov.Bepinex.Framework.Patches;

namespace Paulov.Tarkov.Local.Patches.Performance;

public class BotUpdateMultiThreadPatch : NullPaulovHarmonyPatch
{
    private static readonly Lazy<Type> BotManagerType = new(GetBotManagerType);
    private static readonly Lazy<Type> BotOwnerType = new(GetBotOwnerType);
    private static readonly Lazy<MethodInfo> UpdateManualMethodInfo = new(() => AccessTools.Method(BotOwnerType.Value, "UpdateManual"));
    private static readonly Lazy<MethodInfo> AddFromListMethodInfo = new(() => AccessTools.Method(BotManagerType.Value, "AddFromList"));
    private static readonly Lazy<PropertyInfo> IDPropertyInfo = new(() => BotOwnerType.Value.GetProperty("Id"));
    
    public override MethodBase GetMethodToPatch()
    {
        Plugin.Logger.LogDebug($"{nameof(BotUpdateMultiThreadPatch)}.GetMethodToPatch: Getting manager type");
        
        return BotManagerType.Value.GetMethods().Single(x => x.Name == "UpdateByUnity");
    }

    public override HarmonyMethod GetPrefixMethod()
    {
        return new HarmonyMethod(GetType().GetMethod(nameof(PrefixMethod), BindingFlags.NonPublic | BindingFlags.Static));
    }

    private static Type GetBotManagerType()
    {
        Type retClass = Plugin.EftTypes.Where(x => x.GetMethods().Any(y => y.Name == "UpdateByUnity"))
            .Single(x => x.GetProperty("BotOwners", BindingFlags.Public | BindingFlags.Instance) != null);
        Plugin.Logger.LogDebug($"{nameof(BotUpdateMultiThreadPatch)}.{nameof(GetBotManagerType)}: Got botManager type {retClass.FullName}");
        return retClass;
    }

    private static Type GetBotOwnerType()
    {
        return Plugin.EftTypes.Where(x => x.GetInterfaces().Any(y => y.Name == "IPlayer"))
            .Single(x => x.Name == "BotOwner");
    }

    private static bool PrefixMethod(ref HashSet<object> ___hashSet_0, HashSet<Int32> ___hashSet_1, object __instance)
    {
        Plugin.Logger.LogDebug($"{nameof(BotUpdateMultiThreadPatch)}.{nameof(PrefixMethod)}: Starting to update {___hashSet_0.Count()} botOwners");
        Parallel.ForEach(___hashSet_0, botOwner =>
        {
            try
            {
                UpdateManualMethodInfo.Value.Invoke(botOwner, null);
                Plugin.Logger.LogDebug($"{nameof(BotUpdateMultiThreadPatch)}.{nameof(PrefixMethod)}: botOwner successfully updated");
            }
            catch (Exception e)
            {
                LogError($"Could not update botOwner\n{e}", nameof(PrefixMethod));
                Int32 botOwnerId = (Int32)IDPropertyInfo.Value.GetValue(botOwner);
                _ = ___hashSet_1.Add(botOwnerId);
            }
        });

        AddFromListMethodInfo.Value.Invoke(__instance, null);
        return false;
    }

    private static void LogError(string message, string source = nameof(LogError))
    {
        Plugin.Logger.LogError($"{nameof(BotUpdateMultiThreadPatch)}.{source}: {message}");   
    }
}