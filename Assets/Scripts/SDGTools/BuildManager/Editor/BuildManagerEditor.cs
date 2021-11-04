﻿using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Build.Reporting;

namespace build_manager
{
    public class BuildManagementEditor : EditorWindow
    {
        #region Constants

        private const int MIN_WINDOW_SIZE_X = 540;
        private const int MIN_WINDOW_SIZE_Y = 360;
        private const string KEY_USE_BUILD_VERSION = "BuildManager_UseBuildVersion";
        private const string KEY_BUILD_PATH = "BuildManager_BuildPath";
        private const string KEY_BUILD_TARGET = "BuildManager_BuildTarget";
        private const string KEY_BUILD_HEADLESS = "BuildManager_BuildHeadless";
        private const string KEY_INCLUDE_ZIP = "BuildManager_IncludeZIP";

        #endregion

        #region Members

        private static string m_productName = string.Empty;
        private static string m_buildPath = string.Empty;
        private static string m_buildDirectory = string.Empty;
        private static bool m_useBuildVersion = true;
        private static string m_buildVersion = string.Empty;
        private static string m_buildName = string.Empty;
        public static bool m_buildHeadless = false;
        private static string [] m_buildScenes = null;
        private static int [] m_buildTargets = null;
        private static string [] m_buildTargetOptions = null;
        private static int m_buildTarget = 0;
        private static bool m_includeZip = true;

        #endregion

        #region Initialization

        [MenuItem ( "Tools/Silver Dog Games/Build Manager" )]
        public static void ShowWindow ()
        {
            Initialize ();

            EditorWindow window = GetWindow ( typeof ( BuildManagementEditor ), true, "Build Manager" );
            window.minSize = new Vector2 ( MIN_WINDOW_SIZE_X, MIN_WINDOW_SIZE_Y );
            window.Repaint ();
        }

        private static void Initialize ()
        {
            m_buildTargets = new int []
            {
            (int)BuildTarget.StandaloneWindows64,
            (int)BuildTarget.StandaloneWindows,
            (int)BuildTarget.StandaloneOSX,
            (int)BuildTarget.StandaloneLinux64,
            };
            m_buildTargetOptions = new string []
            {
            "StandaloneWindows64",
            "StandaloneWindows",
            "StandaloneOSX",
            "StandaloneLinux64",
            };

            // Load previous build settings
            LoadBuildSettings ();

            // Get buildable scenes
            EditorBuildSettingsScene [] scenes = EditorBuildSettings.scenes;
            m_buildScenes = EditorBuildSettingsScene.GetActiveSceneList ( scenes );
        }

        #endregion

        #region GUI

        private void OnGUI ()
        {
            // Editor window
            GUILayout.Label ( "General Settings", EditorStyles.boldLabel );
            EditorGUILayout.Separator ();

            // Product name
            EditorGUILayout.LabelField ( "Product Name:", m_productName );

            // Build version
            EditorGUILayout.LabelField ( "Build Version:", m_buildVersion );

            // Use build version in filename
            m_useBuildVersion = EditorGUILayout.Toggle ( "Use Build Version:", m_useBuildVersion );

            // Build path
            EditorGUILayout.LabelField ( "Build Path: ", m_buildPath );

            if ( GUILayout.Button ( "Browse...", GUILayout.Height ( 20 ) ) )
            {
                m_buildPath = EditorUtility.OpenFolderPanel ( "Select build location...", "", "" );
            }
            EditorGUILayout.Separator ();

            m_buildTarget = EditorGUILayout.IntPopup ( "Build Target:", m_buildTarget, m_buildTargetOptions, m_buildTargets );

            EditorGUILayout.Separator ();

            // Buildable scenes
            string sceneList = string.Empty;
            foreach ( string scenePath in m_buildScenes )
            {
                sceneList += '\t' + scenePath + '\n';
            }
            GUILayout.Label ( "Scenes in Build:" );
            GUILayout.Label ( sceneList );

            EditorGUILayout.Separator ();

            // Build headless option
            m_buildHeadless = EditorGUILayout.Toggle ( "Headless build:", m_buildHeadless );

            EditorGUILayout.Separator ();

            GUILayout.Label ( "Post-Build Settings", EditorStyles.boldLabel );
            EditorGUILayout.Separator ();

            // Include Zip setting
            m_includeZip = EditorGUILayout.Toggle ( "Include ZIP: ", m_includeZip );


            GUILayout.FlexibleSpace ();

            // Save build settings
            if ( GUILayout.Button ( "Save Settings", GUILayout.Height ( 30 ) ) )
            {
                if ( IsValid () )
                {
                    SaveBuildSettings ();
                }
            }

            // Build name preview
            m_buildName = m_useBuildVersion ? $"{m_productName}_{m_buildVersion}" : m_productName;

            // Build button
            if ( GUILayout.Button ( $"Save and Build\n[{m_buildName}]", GUILayout.Height ( 40 ) ) )
            {
                if ( IsValid () )
                {
                    // Save build settings
                    SaveBuildSettings ();

                    // Apply settings and build
                    BuildPlayer ();
                }
                else
                {
                    // Build conditions invalid
                    Debug.LogError ( "Cannot build player. Check that all build conditions are valid first." );
                }
            }
        }

        #endregion

        #region Saving/Loading

        private static void LoadBuildSettings ()
        {
            m_productName = Application.productName;
            m_buildVersion = Application.version;

            m_useBuildVersion = PlayerPrefs.GetInt ( KEY_USE_BUILD_VERSION ) == 1;
            m_buildPath = PlayerPrefs.GetString ( KEY_BUILD_PATH );
            if ( string.IsNullOrEmpty ( m_buildPath ) )
            {
                m_buildPath = Directory.GetParent ( Application.dataPath ).FullName; // Default
            }
            m_buildTarget = PlayerPrefs.GetInt ( KEY_BUILD_TARGET );
            if ( m_buildTarget == 0 )
            {
                m_buildTarget = ( int ) BuildTarget.StandaloneWindows64; // Default
            }
            m_buildHeadless = PlayerPrefs.GetInt ( KEY_BUILD_HEADLESS ) == 1;
            m_includeZip = PlayerPrefs.GetInt ( KEY_INCLUDE_ZIP ) == 1;
        }

        private void SaveBuildSettings ()
        {
            PlayerPrefs.SetInt ( KEY_USE_BUILD_VERSION, m_useBuildVersion ? 1 : 0 );
            PlayerPrefs.SetString ( KEY_BUILD_PATH, m_buildPath );
            PlayerPrefs.SetInt ( KEY_BUILD_TARGET, m_buildTarget );
            PlayerPrefs.SetInt ( KEY_BUILD_HEADLESS, m_buildHeadless ? 1 : 0 );
            PlayerPrefs.SetInt ( KEY_INCLUDE_ZIP, m_includeZip ? 1 : 0 );

            Debug.Log ( "Successfully saved build settings." );
        }

        #endregion

        #region Builds

        private void BuildPlayer ()
        {
            string applicationName = $"{m_productName}.exe";
            string buildLocation = Path.Combine ( m_buildPath, m_buildName, applicationName );
            m_buildDirectory = Directory.GetParent ( buildLocation ).FullName;
            BuildOptions headlessOption = m_buildHeadless ? BuildOptions.EnableHeadlessMode : BuildOptions.None;

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = m_buildScenes,
                locationPathName = buildLocation,
                target = ( BuildTarget ) m_buildTarget,
                options = headlessOption,
                targetGroup = BuildTargetGroup.Standalone
            };
            BuildReport buildReport = BuildPipeline.BuildPlayer ( buildOptions );
            BuildSummary buildSummary = buildReport.summary;

            if ( buildSummary.result == BuildResult.Succeeded )
            {
                string totalSize = FileSizeFormatter.SizeSuffix ( ( long ) buildSummary.totalSize );
                Debug.Log ( $"Build succeeded - Name [{m_buildName}] - Size [{totalSize}] - Time [{buildSummary.totalTime}]" );
            }
            else if ( buildSummary.result == BuildResult.Failed )
            {
                Debug.Log ( "Build failed." );
            }
        }

        [PostProcessBuild]
        public static void OnPostProcessBuild ( BuildTarget target, string pathToBuiltProject )
        {
            if ( m_includeZip )
            {
                string zipPath = new DirectoryInfo ( pathToBuiltProject ).Parent.FullName + ".zip";
                Debug.Log ( $"Creating ZIP [{zipPath}]" );
                if ( File.Exists ( zipPath ) ) // Remove existing .zip
                {
                    File.Delete ( zipPath );
                }
                ZipFile.CreateFromDirectory ( m_buildDirectory, zipPath );

                // Open zip directory
                ShowExplorer ( zipPath );
            }
            else
            {
                // Open build directory
                ShowExplorer ( pathToBuiltProject );
            }
        }

        #endregion

        #region Util

        private bool IsValid ()
        {
            return !string.IsNullOrEmpty ( m_productName ) &&
                !string.IsNullOrEmpty ( m_buildPath ) &&
                Directory.Exists ( m_buildPath );
        }

        private static void ShowExplorer ( string path )
        {
            path = path.Replace ( @"/", @"\" );
            System.Diagnostics.Process.Start ( "explorer.exe", "/select," + path );
        }

        private void OnValidate ()
        {
            Initialize ();
        }

        #endregion
    }
}
