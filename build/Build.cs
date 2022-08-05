using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[GitHubActions("ci",
    GitHubActionsImage.UbuntuLatest,
    AutoGenerate = true,
    OnPushBranches = new[] { "main" },
    OnPullRequestBranches = new[] { "dev" },
    InvokedTargets = new[] { nameof(GitHubActions) },
    ImportSecrets = new[] { nameof(APP_SERVICE_NAME), nameof(WEB_DEPLOY_USERNAME), nameof(WEB_DEPLOY_PASSWORD) })]
[DotNetVerbosityMapping]
[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.GitHubActions);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution(GenerateProjects = true)] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion(NoFetch = true)] readonly GitVersion GitVersion;

    [Parameter][Secret] string WEB_DEPLOY_USERNAME;
    [Parameter][Secret] string WEB_DEPLOY_PASSWORD;
    [Parameter][Secret] string APP_SERVICE_NAME;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath OutputDirectory => RootDirectory / "output";
    AbsolutePath DeploymentDirectory => RootDirectory / "deployment";

    [Parameter]
    readonly string publishOutput = IsServerBuild
        ? Configuration.Release
        : Configuration.Debug;

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(OutputDirectory);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(_ => _
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .SetOutputDirectory(OutputDirectory));
        });

    Target Publish => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetPublish(_ => _
                .SetProject(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .SetOutput(OutputDirectory)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
            );
        });

    Target Deploy => _ => _
        .DependsOn(Publish)
        .Requires(() => WEB_DEPLOY_USERNAME)
        .Requires(() => WEB_DEPLOY_PASSWORD)
        .Requires(() => APP_SERVICE_NAME)
        .Executes(async () =>
        {
            var base64Auth = Convert.ToBase64String(Encoding.Default.GetBytes($"{WEB_DEPLOY_USERNAME}:{WEB_DEPLOY_PASSWORD}"));

            var zipFile = DeploymentDirectory / "deployment.zip";

            if (File.Exists(zipFile))
            {
                File.Delete(zipFile);
            }

            if (!Directory.Exists(DeploymentDirectory))
            {
                Directory.CreateDirectory(DeploymentDirectory);
            }

            ZipFile.CreateFromDirectory(OutputDirectory, zipFile);
            byte[] fileContents = File.ReadAllBytes(zipFile);

            using (var memStream = new MemoryStream(fileContents))
            {
                memStream.Position = 0;
                var content = new StreamContent(memStream);
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", base64Auth);
                var requestUrl = $"https://{APP_SERVICE_NAME}.scm.azurewebsites.net/api/zipdeploy";
                var response = await httpClient.PostAsync(requestUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();
                Serilog.Log.Debug(responseString);
                Serilog.Log.Debug("Deployment finished");
                if (!response.IsSuccessStatusCode)
                {
                    Assert.Fail("Deployment returned status code: " + response.StatusCode);
                }
                else
                {
                    Serilog.Log.Debug(response.StatusCode.ToString());
                }
            }
        });

    Target GitHubActions => _ => _
        .DependsOn(Deploy)
        .Executes();
}
