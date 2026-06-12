using System;
using Raphael.Resources;
using Raphael.Utils;
using Il2CppInterop.Runtime;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace Raphael.Services.Faust;

// READ-ONLY diagnostic for the "player markers on the native map" feature (proxy-entity approach). It does
// NOT create or modify anything — it just logs, to LogOutput.log, what the game's map-icon system looks
// like on this client so the proxy-entity spawner can be built from real data:
//   • whether the local character carries MapIconData / PlayerMapIcon / AttachMapIconsToEntity / etc.
//   • how many entities currently have each of those components,
//   • for a sample of MapIcon / PlayerMapIcon entities, their own PrefabGUID + resolved name (to find a
//     usable marker-icon prefab) and a few MapIconData field values (the template to replicate).
//
// Crash discipline: client-null gate, world.IsCreated, lazy queries, em.Exists, everything in try/catch,
// dispose temp arrays. Triggered on demand from a diagnostic button — never runs on a timer.
internal static class FaustMapProbe
{
    private const int SAMPLE = 30;

    public static void Probe()
    {
        try
        {
            if (Plugin.IsClientNull()) { LogUtils.LogInfo("[Faust][mapprobe] client world not ready."); return; }
            var em = Plugin.EntityManager;
            var world = em.World;
            if (world == null || !world.IsCreated) { LogUtils.LogInfo("[Faust][mapprobe] world not created."); return; }

            LogUtils.LogInfo("[Faust][mapprobe] ===== map-icon probe begin =====");

            // 1) What does the local character carry?
            var ch = Plugin.LocalCharacter;
            if (ch != Entity.Null && em.Exists(ch))
            {
                LogUtils.LogInfo($"[Faust][mapprobe] local char: MapIconData={Has<MapIconData>(ch)} " +
                    $"PlayerMapIcon={Has<PlayerMapIcon>(ch)} AttachMapIconsToEntity={Has<AttachMapIconsToEntity>(ch)} " +
                    $"MapIconTargetEntity={Has<MapIconTargetEntity>(ch)}");
            }
            else LogUtils.LogInfo("[Faust][mapprobe] local char not available.");

            // 2) Population counts + samples for each map-icon component.
            ProbeComponent<MapIconData>("MapIconData", em, logData: true);
            var samplePlayerIcon = ProbeComponent<PlayerMapIcon>("PlayerMapIcon", em, logData: false);
            ProbeComponent<AttachMapIconsToEntity>("AttachMapIconsToEntity", em, logData: false);

            // 3) FULL component dump (archetype + values) of a real player-marker icon entity + the local
            //    character — this is the template the proxy-marker spawner must replicate. Best-effort.
            DumpEntityInfo(em, samplePlayerIcon, "PlayerMapIcon[0] (the MapIcon_Player template)");
            DumpEntityInfo(em, ch, "local character");

            LogUtils.LogInfo("[Faust][mapprobe] ===== map-icon probe end (paste the [Faust][mapprobe] lines back) =====");
        }
        catch (Exception ex)
        {
            LogUtils.LogWarning($"[Faust][mapprobe] probe failed: {ex}");
        }
    }

    private static bool Has<T>(Entity e) where T : unmanaged
    {
        try { return e.Has<T>(); } catch { return false; }
    }

    private static Entity ProbeComponent<T>(string label, EntityManager em, bool logData) where T : unmanaged
    {
        Entity first = Entity.Null;
        EntityQuery q = default;
        NativeArray<Entity> arr = default;
        try
        {
            q = em.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<T>()));
            arr = q.ToEntityArray(Allocator.Temp);
            LogUtils.LogInfo($"[Faust][mapprobe] {label}: {arr.Length} entities");
            int n = Math.Min(arr.Length, SAMPLE);
            for (int i = 0; i < n; i++)
            {
                var e = arr[i];
                if (!em.Exists(e)) continue;
                if (first == Entity.Null) first = e;
                string guidTxt = "";
                if (e.Has<PrefabGUID>())
                {
                    int g = e.Read<PrefabGUID>().GuidHash;
                    guidTxt = $" prefab={g} ({SafeName(g)})";
                }
                string extra = logData ? MapIconDataSummary(e) : "";
                LogUtils.LogInfo($"[Faust][mapprobe]   {label}[{i}] entity={e.Index}:{e.Version}{guidTxt}{extra}");
            }
        }
        catch (Exception ex) { LogUtils.LogWarning($"[Faust][mapprobe] {label} query failed: {ex.Message}"); }
        finally { if (arr.IsCreated) arr.Dispose(); }
        return first;
    }

    // Dump the full component archetype (+ values where the framework prints them) of one entity, so the
    // proxy-marker spawner can replicate the exact template. Tries EntityManager.Debug.GetEntityInfo first
    // (one rich string), then falls back to listing component type names from GetComponentTypes.
    private static void DumpEntityInfo(EntityManager em, Entity e, string label)
    {
        if (e == Entity.Null || !em.Exists(e)) { LogUtils.LogInfo($"[Faust][mapprobe] entity-info ({label}): n/a"); return; }
        LogUtils.LogInfo($"[Faust][mapprobe] --- entity-info: {label} (entity={e.Index}:{e.Version}) ---");
        bool printed = false;
        try
        {
            var info = em.Debug.GetEntityInfo(e);
            if (!string.IsNullOrEmpty(info))
            {
                foreach (var line in info.Split('\n'))
                    LogUtils.LogInfo($"[Faust][mapprobe]   {line.TrimEnd()}");
                printed = true;
            }
        }
        catch (Exception ex) { LogUtils.LogInfo($"[Faust][mapprobe]   (GetEntityInfo unavailable: {ex.Message})"); }

        if (!printed)
        {
            try
            {
                var types = em.GetComponentTypes(e, Allocator.Temp);
                try
                {
                    foreach (var ct in types)
                    {
                        string tn;
                        try { tn = ct.GetManagedType()?.FullName ?? ct.ToString(); } catch { tn = ct.ToString(); }
                        LogUtils.LogInfo($"[Faust][mapprobe]   component: {tn}");
                    }
                }
                finally { types.Dispose(); }
            }
            catch (Exception ex) { LogUtils.LogInfo($"[Faust][mapprobe]   (component list unavailable: {ex.Message})"); }
        }
    }

    // Best-effort dump of a few MapIconData fields. Wrapped so an unexpected field type can't throw out of
    // the whole probe — if a line is missing from the log, that field name/type guess was wrong and I'll
    // adjust from the assembly metadata.
    private static string MapIconDataSummary(Entity e)
    {
        try
        {
            var d = e.Read<MapIconData>();
            return $" [MapIconData ShowOnMinimap={d.ShowOnMinimap} ClampOnMinimap={d.ClampOnMinimap} RequiresReveal={d.RequiresReveal}]";
        }
        catch (Exception ex) { return $" [MapIconData read failed: {ex.Message}]"; }
    }

    private static string SafeName(int guid)
    {
        try { return PrefabNameResolver.TryGet(guid, out var n) && !string.IsNullOrEmpty(n) ? n : "?"; }
        catch { return "?"; }
    }
}
