using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

using GCore.Extensions.StringShEx;
using GCore.Extensions.ArrayEx;

class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.Compile);

    [Parameter("ApiKey for the specified source.")] readonly string ApiKey;
    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;

    string Source => "https://api.nuget.org/v3/index.json";


    String GitVersion = "1.0.0";

    string GitVersionSuffix = "0";

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";


    public Build() {

        var version = "git tag".Sh().Split('\n').Get(-2).ExtractVersion();

        var suffix = "git rev-list --count HEAD".Sh().Replace("\n", "").Trim();



        GitVersion = version.ToString(3);

        GitVersionSuffix = GitVersion + "." + suffix;

    }

    Target Clean => _ => _
        .Executes(() =>
        {
            EnsureCleanDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(SolutionFile));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(SolutionFile)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var version = GitVersionSuffix;

            DotNetPack(s => s
                .SetProject(SolutionFile)
                .SetVersion(version)
                .SetOutputDirectory(ArtifactsDirectory)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableIncludeSymbols());
        });

    Target Publish => _ => _
        .DependsOn(Pack)
        .Requires(() => ApiKey)
        .Executes(() =>
        {
            GlobFiles(ArtifactsDirectory, "GCore.*.nupkg").NotEmpty()
                .Where(x => !x.EndsWith(".symbols.nupkg"))
                .ToList()
                .ForEach(x => DotNetNuGetPush(s => s
                    .SetTargetPath(x)
                    .SetSource(Source)
                    .SetApiKey(ApiKey)));
        });
}
