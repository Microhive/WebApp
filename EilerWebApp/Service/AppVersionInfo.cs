using System.Reflection;

namespace EilerWebApp.Service
{
    public class AppVersionInfo
    {
        public AppVersionInfo()
        {
            var version = "1.Branch.dev.Sha.4a99b73ae9502b35af8c266dc398b7f12a95ca83"; // Dummy version for local dev

            var appAssembly = typeof(AppVersionInfo).Assembly;
            var infoVerAttr = (AssemblyInformationalVersionAttribute)appAssembly
                .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute)).FirstOrDefault();

            if (infoVerAttr != null && infoVerAttr.InformationalVersion.Length > 6)
            {
                // Hash is embedded in the version after a '+' symbol, e.g. 1.0.0+a34a913742f8845d3da5309b7b17242222d41a21
                version = infoVerAttr.InformationalVersion;
            }

            var versionParts = version.Split('.');

            this.commitGitNumber = versionParts[0];
            this.gitBranchName = versionParts[2];
            this.longGitHash = versionParts[4];
            this.shortGitHash = versionParts[4].Substring(versionParts[4].Length - 6, 6);
        }

        public string longGitHash { get; }

        public string shortGitHash  { get; }

        public string gitBranchName  { get; }

        public string commitGitNumber { get; }
    }
}
