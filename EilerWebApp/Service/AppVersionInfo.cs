using System.Reflection;

namespace EilerWebApp.Service
{
    public class AppVersionInfo
    {
        public AppVersionInfo()
        {
            var version = "0.1.0+31.Branch.dev.Sha.58801555e6ca2d683f1137c481b1bfd3c9f2b98a"; // Dummy version for local dev

            var appAssembly = typeof(AppVersionInfo).Assembly;
            var infoVerAttr = (AssemblyInformationalVersionAttribute)appAssembly
                .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute)).FirstOrDefault();

            if (infoVerAttr != null && infoVerAttr.InformationalVersion.Length > 6)
            {
                // Hash is embedded in the version after a '+' symbol, e.g. 1.0.0+a34a913742f8845d3da5309b7b17242222d41a21
                version = infoVerAttr.InformationalVersion;
            }

            var buildVersion = version.Split('+');
            var gitVersion = buildVersion[1].Split('.');

            this.commitGitNumber = gitVersion[0];
            this.gitBranchName = gitVersion[2];
            this.longGitHash = gitVersion[4];
            this.shortGitHash = gitVersion[4].Substring(gitVersion[4].Length - 6, 6);
        }

        public string longGitHash { get; }

        public string shortGitHash  { get; }

        public string gitBranchName  { get; }

        public string commitGitNumber { get; }
    }
}
