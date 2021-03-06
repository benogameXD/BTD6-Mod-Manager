﻿using BTD6_Mod_Manager.Lib.Web;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows;

namespace BTD6_Mod_Manager.Lib.Updaters
{
    public class ModManager_Updater
    {
        internal static readonly string defaultGitURL = "https://api.github.com/repos/TDToolbox/BTD6-Mod-Manager/releases";

        public string GitReleasesURL { get; set; } = "";
        public GithubReleaseConfig LatestReleaseInfo { get; set; }


        public ModManager_Updater()
        {
            GitReleasesURL = defaultGitURL;
        }

        public ModManager_Updater(string gitReleaseURL)
        {
            GitReleasesURL = (String.IsNullOrEmpty(gitReleaseURL)) ? defaultGitURL : gitReleaseURL;
        }


        public void HandleUpdates()
        {
            var unparsedGitText = GetGitText();
            if (string.IsNullOrEmpty(unparsedGitText))
                return;

            var releaseConfig = CreateReleaseConfigFromText(unparsedGitText);
            if (releaseConfig is null)
                return;

            if (!IsUpdate(releaseConfig))
            {
                Logger.Log("BTD6 Mod Manager is up to date!");
                return;
            }

            Logger.Log("An update is available for BTD6 Mod Manager!");
            bool installUpdates = AskInstallUpdates();
            if (!installUpdates)
            {
                Logger.Log("You chose not to install updates.");
                return;
            }

            DownloadUpdates();
            ExtractUpdater();

            Logger.Log("Closing BTD6 Mod Manager to finish update", OutputType.Both);
            LaunchUpdater();
            Environment.Exit(0);
        }


        private string GetGitText()
        {
            WebReader reader = new WebReader();
            var gitText = reader.ReadText_FromURL(GitReleasesURL, maxTries: 50);
            return gitText;
        }

        private List<GithubReleaseConfig> CreateReleaseConfigFromText(string unparsedGitText)
        {
            var releaseConfig = GithubReleaseConfig.FromJson(unparsedGitText);
            return releaseConfig;
        }

        private bool IsUpdate(List<GithubReleaseConfig> githubReleaseConfigs)
        {
            GetCurrentAndLatestVersion(githubReleaseConfigs, out string latestGitVersion, out string currentVersion);
            var latest = VersionToInt(latestGitVersion);
            var current = VersionToInt(currentVersion);
            return latest > current;
        }

        private void GetCurrentAndLatestVersion(List<GithubReleaseConfig> githubReleaseConfigs, out string latestGitVersion, out string currentVersion)
        {
            LatestReleaseInfo = githubReleaseConfigs[0];
            latestGitVersion = LatestReleaseInfo.TagName;
            currentVersion = GetCurrentVersion();

            CleanVersionTexts(latestGitVersion, currentVersion, out latestGitVersion, out currentVersion);
        }

        private string GetCurrentVersion()
        {
            var assembly = Assembly.GetEntryAssembly();
            FileVersionInfo currentVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            return currentVersionInfo.FileVersion;
        }

        private void CleanVersionTexts(string latestGitVersion, string currentVersion, out string processedLatestVersion, out string processedCurrentVersion)
        {
            const string delimiter = ".";
            processedLatestVersion = latestGitVersion.Replace(delimiter, "");
            processedCurrentVersion = currentVersion.Replace(delimiter, "");

            bool areSameLength = (processedLatestVersion.Length == processedCurrentVersion.Length);
            if (areSameLength)
                return;

            const string fillerChar = "0";
            while (!areSameLength)
            {
                int lLength = processedLatestVersion.Length;
                int cLength = currentVersion.Length;

                processedLatestVersion = (lLength < cLength) ? processedLatestVersion + fillerChar : processedLatestVersion;
                processedCurrentVersion = (cLength < lLength) ? processedCurrentVersion + fillerChar : processedCurrentVersion;
                areSameLength = (processedLatestVersion.Length == processedCurrentVersion.Length);
            }
        }

        private int VersionToInt(string versionText)
        {
            Int32.TryParse(versionText, out int version);
            return version;
        }


        private bool AskInstallUpdates()
        {
            var result = MessageBox.Show("An update is available for BTD6 Mod Manager. Would you like to download the update?", "Download update?", MessageBoxButton.YesNo);
            return (result == MessageBoxResult.Yes);
        }

        private void DownloadUpdates()
        {
            List<string> downloads = GetDownloadURLs();
            foreach (string file in downloads)
            {
                FileDownloader downloader = new FileDownloader();
                downloader.DownloadFile(file, Environment.CurrentDirectory);
            }
        }

        private List<string> GetDownloadURLs()
        {
            int i = -1;
            List<string> downloads = new List<string>();
            foreach (var a in LatestReleaseInfo.Assets)
            {
                Logger.Log("Downloading " + a.BrowserDownloadUrl.ToString());
                downloads.Add(a.BrowserDownloadUrl.ToString());
            }

            return downloads;
        }

        private void ExtractUpdater()
        {
            var files = new DirectoryInfo(Environment.CurrentDirectory).GetFiles();
            foreach (var file in files)
            {
                string filename = file.Name;
                if (!filename.EndsWith(".zip") && !filename.EndsWith(".rar") && !filename.EndsWith(".7z"))
                    continue;

                using (ZipArchive archive = ZipFile.OpenRead(file.FullName))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string destinationPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, entry.FullName));
                        if (destinationPath.StartsWith(Environment.CurrentDirectory, StringComparison.Ordinal))
                        {
                            if (new FileInfo(destinationPath).Name != "Updater.exe")
                                continue;

                            if (File.Exists(destinationPath))
                                File.Delete(destinationPath);

                            entry.ExtractToFile(destinationPath);
                            Logger.Log($"Extracting file: {destinationPath}");
                        }
                    }
                }
            }
        }


        private void LaunchUpdater()
        {
            var updaterPath = Environment.CurrentDirectory + "\\Updater.exe";
            if (!File.Exists(updaterPath))
            {
                Logger.Log("ERROR! Unable to find updater. You will need to close BTD6 Mod Manager" +
                    $" and manually extract the updater from:  {updaterPath}");
                return;
            }

            Process.Start(updaterPath, "launched_from_BTD6 Mod Manager");
        }
    }
}
