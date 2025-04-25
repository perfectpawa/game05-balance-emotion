using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

using static System.Environment;
using static System.IO.Path;
using static UnityEditor.AssetDatabase;

public static class ProjectSetup {
    [MenuItem("Tools/Setup/Import Essential Assets")]
    public static void ImportEssentials() {        
        // Assets.ImportAsset("Odin Inspector and Serializer.unitypackage", "Sirenix/Editor ExtensionsSystem");
        // Assets.ImportAsset("Odin Validator.unitypackage", "Sirenix/Editor ExtensionsUtilities");
        // Assets.ImportAsset("Editor Console Pro.unitypackage", "FlyingWorm/Editor ExtensionsSystem");
        Assets.ImportAsset("DOTween HOTween v2.unitypackage", "Demigiant/Editor ExtensionsAnimation");
        Assets.ImportAsset("Serialize Reference Editor.unitypackage", "Makar Osokin/Editor ExtensionsUtilities");
    }

    [MenuItem("Tools/Setup/Install Essential Packages")]
    public static void InstallPackages() {
        Debug.Log("Installing packages...");
        Packages.InstallPackages(new[] {
            "com.unity.2d.animation",
            "git+https://github.com/adammyhre/Unity-Utils.git",
            "git+https://github.com/adammyhre/Unity-Improved-Timers.git",
            "git+https://github.com/KyleBanks/scene-ref-attribute.git"
        });
    }

    [MenuItem("Tools/Setup/Create Folders")]
    public static void CreateFolders() {
        Folders.Create("_Project", "Animation", "Art", "Materials", "Prefabs", "_Scripts/Scriptable", "_Scripts/Tests", "_Scripts/Tests/Editor", "_Scripts/Tests/Runtime");
        Refresh();
        Folders.Move("_Project", "Scenes");
        Folders.Move("_Project", "Settings");
        Folders.Delete("TutorialInfo");
        Refresh();

        MoveAsset("Assets/InputSystem_Actions.inputactions", "Assets/_Project/Settings/InputSystem_Actions.inputactions");
        DeleteAsset("Assets/Readme.asset");
        Refresh();
        
        // Optional: Disable Domain Reload
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
    }

    static class Assets {
        public static void ImportAsset(string asset, string folder) {
            string basePath;
            if (OSVersion.Platform is PlatformID.MacOSX or PlatformID.Unix) {
                string homeDirectory = GetFolderPath(SpecialFolder.Personal);
                basePath = Combine(homeDirectory, "Library/Unity/Asset Store-5.x");
            } else {
                string defaultPath = Combine(GetFolderPath(SpecialFolder.ApplicationData), "Unity");
                basePath = Combine(EditorPrefs.GetString("AssetStoreCacheRootPath", defaultPath), "Asset Store-5.x");
            }

            asset = asset.EndsWith(".unitypackage") ? asset : asset + ".unitypackage";

            string fullPath = Combine(basePath, folder, asset);

            if (!File.Exists(fullPath)) {
                throw new FileNotFoundException($"The asset package was not found at the path: {fullPath}");
            }

            ImportPackage(fullPath, false);
        }
    }

    static class Packages {
        static AddRequest request;
        static Queue<string> packagesToInstall = new Queue<string>();

        public static void InstallPackages(string[] packages) {
            foreach (var package in packages) {
                packagesToInstall.Enqueue(package);
            }

            if (packagesToInstall.Count > 0) {
                StartNextPackageInstallation();
            }
        }

        static async void StartNextPackageInstallation() {
            request = Client.Add(packagesToInstall.Dequeue());
            
            while (!request.IsCompleted) await Task.Delay(10);
            
            if (request.Status == StatusCode.Success) Debug.Log("Installed: " + request.Result.packageId);
            else if (request.Status >= StatusCode.Failure) Debug.LogError(request.Error.message);

            if (packagesToInstall.Count > 0) {
                await Task.Delay(1000);
                StartNextPackageInstallation();
            }
        }
    }

    static class Folders {
        public static void Create(string root, params string[] folders) {
            var fullpath = Combine(Application.dataPath, root);
            if (!Directory.Exists(fullpath)) {
                Directory.CreateDirectory(fullpath);
            }

            foreach (var folder in folders) {
                CreateSubFolders(fullpath, folder);
            }
        }
        
        static void CreateSubFolders(string rootPath, string folderHierarchy) {
            var folders = folderHierarchy.Split('/');
            var currentPath = rootPath;

            foreach (var folder in folders) {
                currentPath = Combine(currentPath, folder);
                if (!Directory.Exists(currentPath)) {
                    Directory.CreateDirectory(currentPath);
                }
            }
        }
        
        public static void Move(string newParent, string folderName) {
            var sourcePath = $"Assets/{folderName}";
            if (IsValidFolder(sourcePath)) {
                var destinationPath = $"Assets/{newParent}/{folderName}";
                var error = MoveAsset(sourcePath, destinationPath);

                if (!string.IsNullOrEmpty(error)) {
                    Debug.LogError($"Failed to move {folderName}: {error}");
                }
            }
        }
        
        public static void Delete(string folderName) {
            var pathToDelete = $"Assets/{folderName}";

            if (IsValidFolder(pathToDelete)) {
                DeleteAsset(pathToDelete);
            }
        }
    }
}