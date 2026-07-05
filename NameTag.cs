using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AceNameTags
{
    [BepInPlugin("com.ace.acenametags.tags", "AceNameTags", "1.0.0")]
    public sealed class NameTagFpsMod : BaseUnityPlugin
    {
        private static readonly FieldInfo RigFpsField =
            typeof(VRRig).GetField("fps", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly Dictionary<VRRig, TextMeshPro> activeTags = new();
        private readonly HashSet<VRRig> seenRigs = new();
        private readonly List<VRRig> staleRigs = new();

        private TMP_FontAsset sansFont;
        private float smoothedDeltaTime = 0.016f;
        private float nextStatusLogTime;

        private void Start()
        {
            sansFont = ResolveSansFont();
            Logger.LogInfo("AceNameTags loaded.");
        }

        private void Update()
        {
            if (GorillaTagger.Instance == null)
                return;

            smoothedDeltaTime += (Time.unscaledDeltaTime - smoothedDeltaTime) * 0.1f;
            int fps = smoothedDeltaTime > 0.0001f ? Mathf.RoundToInt(1f / smoothedDeltaTime) : 0;

            VRRig[] rigs = FindObjectsOfType<VRRig>();
            if (rigs == null || rigs.Length == 0)
            {
                CleanupAllTags();
                return;
            }

            seenRigs.Clear();

            foreach (var rig in rigs)
            {
                if (rig == null || rig.isLocal)
                    continue;

                seenRigs.Add(rig);

                if (!activeTags.TryGetValue(rig, out TextMeshPro tag) || tag == null)
                {
                    tag = CreateTagForRig(rig);
                    if (tag == null) continue;

                    activeTags[rig] = tag;
                }

                UpdateTag(tag, rig, fps);
            }

            staleRigs.Clear();

            foreach (var kvp in activeTags)
            {
                if (kvp.Key == null || kvp.Key.isLocal || !seenRigs.Contains(kvp.Key) || kvp.Value == null)
                {
                    staleRigs.Add(kvp.Key);
                }
            }

            foreach (var rig in staleRigs)
            {
                if (activeTags.TryGetValue(rig, out TextMeshPro tag) && tag != null)
                    Object.Destroy(tag.gameObject);

                activeTags.Remove(rig);
            }

            if (Time.unscaledTime >= nextStatusLogTime)
            {
                Logger.LogInfo($"NameTags rigs: {seenRigs.Count}, tags: {activeTags.Count}");
                nextStatusLogTime = Time.unscaledTime + 15f;
            }
        }

        private void OnDestroy()
        {
            CleanupAllTags();
        }

        private TextMeshPro CreateTagForRig(VRRig rig)
        {
            if (rig == null || rig.isLocal) return null;

            TextMeshPro sourceTag = rig.playerText1 != null
                ? rig.playerText1
                : rig.GetComponentInChildren<TextMeshPro>(true);

            TextMeshPro newTag;

            if (sourceTag != null)
            {
                newTag = Object.Instantiate(sourceTag, rig.transform);
                newTag.transform.localScale = sourceTag.transform.localScale * 10f;
            }
            else
            {
                GameObject obj = new("AceNameTags");
                obj.transform.SetParent(rig.transform, false);
                obj.transform.localScale = Vector3.one * 10f;
                newTag = obj.AddComponent<TextMeshPro>();
            }

            ConfigureTag(newTag);
            return newTag;
        }

        private void ConfigureTag(TextMeshPro tag)
        {
            if (tag == null) return;

            if (sansFont == null)
                sansFont = ResolveSansFont();

            if (sansFont != null)
                tag.font = sansFont;

            tag.color = Color.white;
            tag.alignment = TextAlignmentOptions.Center;
            tag.fontSize = 20f;
            tag.richText = true;
            tag.enableAutoSizing = false;
        }

        private void UpdateTag(TextMeshPro tag, VRRig rig, int fps)
        {
            if (tag == null || rig == null || rig.isLocal) return;

            int rigFps = GetRigFps(rig, fps);

            Color fpsColor =
                rigFps >= 87 ? Color.green :
                rigFps >= 50 ? new Color(1f, 0.5f, 0f) :
                Color.red;

            string name = string.IsNullOrWhiteSpace(rig.playerNameVisible)
                ? "PLAYER"
                : rig.playerNameVisible;

            tag.text =
                $"{name}\n<color=#{ColorUtility.ToHtmlStringRGB(fpsColor)}>{rigFps}</color> FPS";

            tag.transform.position = rig.transform.position + Vector3.up * 0.5f;

            if (Camera.main != null)
            {
                tag.transform.LookAt(Camera.main.transform);
                tag.transform.Rotate(0f, 180f, 0f);
            }
        }

        private static int GetRigFps(VRRig rig, int fallback)
        {
            if (rig == null) return 0;

            if (RigFpsField == null)
                return rig.isLocal ? fallback : 0;

            object value = RigFpsField.GetValue(rig);

            if (value is int fps && fps > 0)
                return fps;

            return rig.isLocal ? fallback : 0;
        }

        private void CleanupAllTags()
        {
            foreach (var tag in activeTags.Values)
            {
                if (tag != null)
                    Object.Destroy(tag.gameObject);
            }

            activeTags.Clear();
            seenRigs.Clear();
            staleRigs.Clear();
        }

        private static TMP_FontAsset ResolveSansFont()
        {
            return FindObjectsOfType<TMP_FontAsset>().FirstOrDefault();
        }
    }
}
