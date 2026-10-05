using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Varkaria.DiscordPresence
{
    internal static class PresenceSettings
    {
        internal const string DefaultApplicationId = "1283700247440134174";
        private static readonly string Prefix = MakePrefix();
        private static string MakePrefix()
        {
            using (var sha = SHA256.Create()) return "VRMDiscordRPC." + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Application.dataPath))).Replace("-", "") + ".";
        }
        public static bool Enabled { get => EditorPrefs.GetBool(Prefix + "Enabled", true); set => EditorPrefs.SetBool(Prefix + "Enabled", value); }
        public static bool ShowProject { get => EditorPrefs.GetBool(Prefix + "ShowProject", true); set => EditorPrefs.SetBool(Prefix + "ShowProject", value); }
        public static bool ShowScene { get => EditorPrefs.GetBool(Prefix + "ShowScene", true); set => EditorPrefs.SetBool(Prefix + "ShowScene", value); }
        public static string ApplicationId { get => EditorPrefs.GetString(Prefix + "ApplicationId", DefaultApplicationId); set => EditorPrefs.SetString(Prefix + "ApplicationId", value); }
        public static bool ValidApplicationId => ulong.TryParse(ApplicationId, out var value) && value > 0;
        [SettingsProvider]
        private static SettingsProvider Provider() => new SettingsProvider("Preferences/VRM Discord RPC", SettingsScope.User)
        {
            label = "VRM Discord RPC", keywords = new HashSet<string> { "Discord", "Rich Presence", "privacy", "scene" },
            guiHandler = _ =>
            {
                EditorGUILayout.HelpBox("Show your Unity activity on your Discord profile. These preferences apply to this project on this computer.", MessageType.Info);
                EditorGUI.BeginChangeCheck();
                var enabled = EditorGUILayout.Toggle("Enable Rich Presence", Enabled);
                var project = EditorGUILayout.Toggle("Share project name", ShowProject);
                var scene = EditorGUILayout.Toggle("Share scene name", ShowScene);
                var appId = EditorGUILayout.TextField("Application ID", ApplicationId).Trim();
                if (EditorGUI.EndChangeCheck())
                {
                    Enabled = enabled; ShowProject = project; ShowScene = scene; ApplicationId = appId;
                    VRMDiscordRPC.ApplySettings();
                }
                if (!ValidApplicationId) EditorGUILayout.HelpBox("Enter a numeric Discord application ID.", MessageType.Warning);
                EditorGUILayout.LabelField("Status", VRMDiscordRPC.Status);
                if (GUILayout.Button("Restore default application")) { ApplicationId = DefaultApplicationId; VRMDiscordRPC.ApplySettings(); }
            }
        };
    }
}
