using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class IosAttPostBuildProcessor
{
    [PostProcessBuild(1000)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        string pbxProjectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        var pbxProject = new PBXProject();
        pbxProject.ReadFromFile(pbxProjectPath);

        string mainTargetGuid = pbxProject.GetUnityMainTargetGuid();
        string frameworkTargetGuid = pbxProject.GetUnityFrameworkTargetGuid();

        // iOS 14+ ATT API references must be linked at build time.
        pbxProject.AddFrameworkToProject(mainTargetGuid, "AppTrackingTransparency.framework", true);
        pbxProject.AddFrameworkToProject(frameworkTargetGuid, "AppTrackingTransparency.framework", true);

        // Commonly required together with ATT-based ad attribution flows.
        pbxProject.AddFrameworkToProject(mainTargetGuid, "AdSupport.framework", true);
        pbxProject.AddFrameworkToProject(frameworkTargetGuid, "AdSupport.framework", true);

        pbxProject.WriteToFile(pbxProjectPath);
    }
}
