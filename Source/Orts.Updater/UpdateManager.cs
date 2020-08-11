﻿// COPYRIGHT 2014, 2015 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Orts.Common;
using Orts.Common.Info;
using Orts.Settings;

namespace Orts.Updater
{
    public class UpdateManager
    {
        private const string versionFile = "version.json";

        // The date on this is fairly arbitrary - it's only used in a calculation to round the DateTime up to the next TimeSpan period.
        readonly DateTime BaseDateTimeMidnightLocal = new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Local);

        public const string ChannelCommandLine = "/CHANNEL=";
        public const string WaitProcessIdCommandLine = "/WAITPID=";
        public const string RelaunchCommandLine = "/RELAUNCH=";
        public const string ElevationCommandLine = "/ELEVATE=";

        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        private readonly string basePath;
        private readonly UpdateSettings updateSettings;
        private readonly UpdateState updateState;
        private UpdateSettings channel;
        private bool forceUpdate;

        private static string PathUpdateTest => Path.Combine(RuntimeInfo.ApplicationFolder, "UpdateTest");

        private string PathUpdateDirty => Path.Combine(basePath, "UpdateDirty");
        private string PathUpdateStage => Path.Combine(basePath, "UpdateStage");
        private string PathDocumentation => Path.Combine(basePath, "Documentation");
        private string PathUpdateDocumentation => Path.Combine(PathUpdateStage, "Documentation"); 
        private string FileUpdateStage => Path.Combine(PathUpdateStage, "Update.zip");
        private string FileSettings => Path.Combine(basePath, "OpenRails.ini");
        private string FileUpdater => Path.Combine(basePath, "Updater.exe");

        public string ChannelName { get; private set; }
        public string ChangeLogLink => channel?.ChangeLogLink;
        public Update LastUpdate { get; private set; }
        public Exception LastCheckError { get; private set; }
        public Exception LastUpdateError { get; private set; }
        public bool UpdaterNeedsElevation { get; private set; }

        public const string LauncherExecutable = "openrails.exe";

        private UpdateChannels channels;
        private string sourceDownload;

        public UpdateManager(string basePath, string productName, string productVersion)
        {
            if (!Directory.Exists(basePath))
                throw new ArgumentException("The specified path must be valid and exist as a directory.", nameof(basePath));
            this.basePath = basePath;
            try
            {
                updateSettings = new UpdateSettings();
                updateState = new UpdateState(updateSettings);
                channel = new UpdateSettings(ChannelName = updateSettings.Channel);
            }
            catch (ArgumentException)
            {
                // Updater.ini doesn't exist. That's cool, we'll just disable updating.
            }

            // Check for elevation to update; elevation is needed if the update writes failed and the user is NOT an
            // Administrator. Weird cases (like no permissions on the directory for anyone) are not handled.
            if (!CheckUpdateWrites())
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                UpdaterNeedsElevation = !principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public UpdateManager(string currentChannel, string source)
        {
            ChannelName = currentChannel;
            sourceDownload = source;
            channels = ResolveUpdateChannels();
            // Check for elevation to update; elevation is needed if the update writes failed and the user is NOT an
            // Administrator. Weird cases (like no permissions on the directory for anyone) are not handled.
            if (!CheckUpdateWrites())
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                UpdaterNeedsElevation = !principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public IEnumerable<string> GetChannels()
        {
            return channels.Channels.Select(channel => channel.Name);
        }

        public ChannelInfo GetChannelByName(string channelName)
        {
            return channels.Channels.FirstOrDefault(channel => channel.Name.Equals(channelName, StringComparison.OrdinalIgnoreCase));
        }

        public void SetChannel(string channelName)
        {
            if (channel == null)
                throw new InvalidOperationException();

            // Switch channel and save the change.
            updateSettings.Channel = channelName;
            updateSettings.Save();
            channel = new UpdateSettings(ChannelName = updateSettings.Channel);

            // Do a forced update check because the cached update data is likely to only be valid for the old channel.
            forceUpdate = true;
        }

        public static string VersionFile { get; } = Path.Combine(RuntimeInfo.ConfigFolder, versionFile);

        public async Task<ChannelInfo> CheckForUpdatesAsync(UpdateCheckFrequency frequency, string channel)
        {
            if (!CheckUpdateNeeded(frequency))
                return null;

            LastCheckError = null;
            using (WebClient client = new WebClient()
            { CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.BypassCache) })
            {
                client.Headers[HttpRequestHeader.UserAgent] = GetUserAgent();
                UriBuilder uriBuilder = new UriBuilder(sourceDownload + versionFile);

                try
                {
                    string versions = await client.DownloadStringTaskAsync(uriBuilder.Uri).ConfigureAwait(false);
                    File.WriteAllText(VersionFile, versions);
                    channels = ResolveUpdateChannels();
                }
                catch (WebException webException)
                {
                    Trace.WriteLine(webException);
                    LastCheckError = webException;
                }
            }
            return CheckForApplicableUpdate(channel);
        }

        public static bool CheckUpdateNeeded(UpdateCheckFrequency target)
        {
            //we just check the update file's timestamp against the target
            switch (target)
            {
                case UpdateCheckFrequency.Never: return false;
                case UpdateCheckFrequency.Daily: return File.Exists(VersionFile) && File.GetLastWriteTime(VersionFile).AddDays(1) < DateTime.Now;
                case UpdateCheckFrequency.Weekly: return File.Exists(VersionFile) && File.GetLastWriteTime(VersionFile).AddDays(7) < DateTime.Now;
                case UpdateCheckFrequency.Biweekly: return File.Exists(VersionFile) && File.GetLastWriteTime(VersionFile).AddDays(14) < DateTime.Now;
                case UpdateCheckFrequency.Monthly: return File.Exists(VersionFile) && File.GetLastWriteTime(VersionFile).AddMonths(1) < DateTime.Now;
                default: return true; //Always, Manually
            }
        }

        private static UpdateChannels ResolveUpdateChannels()
        {
            if (File.Exists(VersionFile))
            {
                using (StreamReader reader = File.OpenText(VersionFile))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    return (UpdateChannels)serializer.Deserialize(reader, typeof(UpdateChannels));
                }
            }
            return UpdateChannels.Empty;
        }

        public ChannelInfo CheckForApplicableUpdate(string targetChannel)
        {
            if (string.IsNullOrEmpty(targetChannel))
                return null;
            ChannelInfo info = GetChannelByName(targetChannel);
            if (null == info)
                return null;

            if (targetChannel.Equals(VersionInfo.Channel, StringComparison.OrdinalIgnoreCase))
            {
                if (VersionInfo.Compare(info.Version) > 0)
                    return info;
            }
            else
            {
                return GetChannelByName(targetChannel);
            }
            return null;
        }

        public async Task CheckForUpdateAsync()
        {
            // If there's no updater file or the update channel is not correctly configured, exit without error.
            if (channel == null)
                return;

            try
            {
                // If we're not at the appropriate time for the next check (and we're not forced), we reconstruct the cached update/error and exit.
                if (DateTime.UtcNow < updateState.NextCheck && !forceUpdate)
                {
                    LastUpdate = updateState.Update.Length > 0 ? JsonConvert.DeserializeObject<Update>(updateState.Update) : null;
                    LastCheckError = updateState.Update.Length > 0 || string.IsNullOrEmpty(channel.URL) ? null : new InvalidDataException("Last update check failed.");

                    // Validate that the deserialized update is sane.
                    ValidateLastUpdate();

                    return;
                }

                // This updates the NextCheck time and clears the cached update/error.
                ResetCachedUpdate();

                if (string.IsNullOrEmpty(channel.URL))
                {
                    // If there's no update URL, reset cached update/error.
                    LastUpdate = null;
                    LastCheckError = null;
                    return;
                }

                // Fetch the update URL (adding ?force=true if forced) and cache the update/error.
                var client = new WebClient()
                {
                    CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.BypassCache),
                    Encoding = Encoding.UTF8,
                };
                client.Headers[HttpRequestHeader.UserAgent] = GetUserAgent();
                UriBuilder uriBuilder = new UriBuilder(channel.URL);
                if (channel.URL.ToLower().Contains("ultimate"))
                {
                    if (!uriBuilder.Uri.IsFile)
                    {
                        if (!uriBuilder.Path.EndsWith("/"))
                            uriBuilder.Path += "/";
                        uriBuilder.Path += "version.json";
                    }
                }
                if (forceUpdate)
                {
                    uriBuilder.Query = (uriBuilder.Query?.Length > 1 ? uriBuilder.Query.Substring(1) + "&" : string.Empty) + "force=true";
                }                
                string updateData = await client.DownloadStringTaskAsync(uriBuilder.Uri);
                LastUpdate = JsonConvert.DeserializeObject<Update>(updateData);
                LastCheckError = null;

                // Check it's all good.
                ValidateLastUpdate();

                CacheUpdate(updateData);
            }
            catch (Exception error)
            {
                // This could be a problem deserializing the LastUpdate or fetching/deserializing the new update. It doesn't really matter, we record an error.
                LastUpdate = null;
                LastCheckError = error;
                Trace.WriteLine(error);

                ResetCachedUpdate();
            }

        }

        private void ValidateLastUpdate()
        {
            if (LastUpdate != null)
            {
                Uri uri = new Uri(LastUpdate.Url, UriKind.RelativeOrAbsolute);
                if (uri.IsAbsoluteUri)
                {
                    LastUpdate = null;
                    LastCheckError = new InvalidDataException("Update URL must be relative to channel URL.");
                }
            }
        }

        public void Update()
        {
            if (LastUpdate == null)
                throw new InvalidOperationException("Cannot get update when no LatestUpdate exists.");
            try
            {
                Process.Start(FileUpdater, 
                    $"{ChannelCommandLine}{ChannelName} {WaitProcessIdCommandLine}{Process.GetCurrentProcess().Id} {RelaunchCommandLine}1 {ElevationCommandLine}{(UpdaterNeedsElevation ? "1" : "0")}")
                    .WaitForInputIdle();
                Environment.Exit(0);
            }
            catch (Exception error)
            {
                LastUpdateError = error;
            }
        }

        public Task RunUpdateProcess()
        {
            if (LastUpdate == null)
                throw new InvalidOperationException("Cannot get update when no LatestUpdate exists.");
            try
            {
                Task updateTask = RunProcess(new ProcessStartInfo(FileUpdater, $"{ChannelCommandLine}{ChannelName} " +
                    $"{WaitProcessIdCommandLine}{Process.GetCurrentProcess().Id} {RelaunchCommandLine}1 {ElevationCommandLine}{(UpdaterNeedsElevation ? "1" : "0")}"));
                Environment.Exit(0);
                return Task.CompletedTask;
            }
            catch (Exception error)
            {
                LastUpdateError = error;
                return Task.FromException(error);
            }
        }

        public async Task ApplyUpdateAsync()
        {
            if (LastUpdate == null) throw new InvalidOperationException("There is no update to apply.");

            TriggerApplyProgressChanged(0);
            try
            {
                CheckUpdateWrites();
                TriggerApplyProgressChanged(1);

                await CleanDirectoriesAsync().ConfigureAwait(false);
                TriggerApplyProgressChanged(2);

                await DownloadUpdateAsync(2, 65).ConfigureAwait(false);
                TriggerApplyProgressChanged(67);

                await ExtractUpdate(67, 30).ConfigureAwait(false);
                TriggerApplyProgressChanged(97);

                if (await UpdateIsReadyAync().ConfigureAwait(false))
                {
                    await VerifyUpdateAsync().ConfigureAwait(false);
                    TriggerApplyProgressChanged(98);

                    await CopyUpdateFileAsync().ConfigureAwait(false);
                    TriggerApplyProgressChanged(99);

                    await CleanDirectoriesAsync().ConfigureAwait(false);
                    TriggerApplyProgressChanged(100);
                }

                LastUpdateError = null;
            }
            catch (Exception error)
            {
                LastUpdateError = error;
            }
        }

        private void TriggerApplyProgressChanged(int progressPercentage)
        {
            ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(progressPercentage, null));
        }

        private static string GetUserAgent()
        {
            return $"{RuntimeInfo.ProductName}/{VersionInfo.Version}";
        }

        void ResetCachedUpdate()
        {
            updateState.LastCheck = DateTime.UtcNow;
            // So what we're doing here is rounding up the DateTime (LastCheck) to the next TTL period. For
            // example, if the TTL was 1 hour, we'd round up to the start of the next hour. Similarly, if the TTL was
            // 1 day, we'd round up to midnight (the start of the next day). The purpose of this is to avoid 2 * TTL 
            // checking which might well occur if you always launch Open Rails around the same time of day each day -
            // if they launch it at 6:00PM on Monday, then 5:30PM on Tuesday, they won't get an update check on
            // Tuesday. With the time rounding, they should get one check/day if the TTL is 1 day and they open it
            // every day. (This is why BaseDateTimeMidnightLocal uses the local midnight!)
            updateState.NextCheck = channel.TTL.TotalDays > 1 ? 
                BaseDateTimeMidnightLocal.AddSeconds(Math.Ceiling((updateState.LastCheck - BaseDateTimeMidnightLocal).TotalSeconds / channel.TTL.TotalSeconds) * channel.TTL.TotalSeconds) : 
                updateState.LastCheck + channel.TTL;
            updateState.Update = string.Empty;
            updateState.Save();
        }

        private void CacheUpdate(string updateData)
        {
            forceUpdate = false;
            updateState.Update = updateData;
            updateState.Save();
        }

        private static bool CheckUpdateWrites()
        {
            try
            {
                Directory.CreateDirectory(PathUpdateTest)?.Delete(true);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private async Task CleanDirectoriesAsync()
        {
            List<Task> cleanupTasks = new List<Task>();

            if (Directory.Exists(PathUpdateDirty))
                cleanupTasks.Add(CleanDirectory(PathUpdateDirty));

            if (Directory.Exists(PathUpdateStage))
                cleanupTasks.Add(CleanDirectory(PathUpdateStage));

            await Task.WhenAll(cleanupTasks).ConfigureAwait(false);
        }

        private Task CleanDirectory(string path)
        {
            //// Clean up as much as we can here, but any in-use files will fail. Don't worry about them. This is
            //// called before the update begins so we'll always start from a clean slate.
            //// Scan the files in any order.
            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try { File.Delete(file); }
                catch (Exception ex) { Trace.TraceWarning($"{path} :: {ex.Message}"); };
            }

            foreach (string directory in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
            {
                try { Directory.Delete(directory, true); }
                catch (Exception ex) { Trace.TraceWarning($"{path} :: {ex.Message}"); };
            }
            try { Directory.Delete(path); }
            catch (Exception ex) { Trace.TraceWarning($"{path} :: {ex.Message}"); };
            return Task.CompletedTask;
        }

        private async Task DownloadUpdateAsync(int progressMin, int progressLength)
        {
            Directory.CreateDirectory(PathUpdateStage);

            Uri updateUri = new Uri(channel.URL);
            updateUri = new Uri(updateUri, LastUpdate.Url);
            WebClient client = new WebClient();

            client.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
            {
                TriggerApplyProgressChanged(progressMin + progressLength * e.ProgressPercentage / 100);
            };

            client.Headers[HttpRequestHeader.UserAgent] = GetUserAgent();

            await client.DownloadFileTaskAsync(updateUri, FileUpdateStage);

            TriggerApplyProgressChanged(progressMin + progressLength);
        }

        private Task ExtractUpdate(int progressMin, int progressLength)
        {
            using (FileStream fileStream = new FileStream(FileUpdateStage, FileMode.Open, FileAccess.Read))
            {
                using (ZipArchive zipFile = new ZipArchive(fileStream))
                {
                    if (string.IsNullOrEmpty(PathUpdateStage))
                        throw new ArgumentNullException(nameof(PathUpdateStage));

                    // Note that this will give us a good DirectoryInfo even if destinationDirectoryName exists:
                    DirectoryInfo directoryInfo = Directory.CreateDirectory(PathUpdateStage);
                    string destinationDirectoryFullPath = directoryInfo.FullName;
                    int count = 0;

                    foreach (ZipArchiveEntry entry in zipFile.Entries)
                    {
                        count++;
                        string fileDestinationPath = Path.GetFullPath(Path.Combine(destinationDirectoryFullPath, entry.FullName));

                        if (!fileDestinationPath.StartsWith(destinationDirectoryFullPath, StringComparison.OrdinalIgnoreCase))
                            throw new IOException("File is extracting to outside of the folder specified.");

                        TriggerApplyProgressChanged(progressMin + progressLength * count / zipFile.Entries.Count);

                        if (Path.GetFileName(fileDestinationPath).Length == 0)
                        {
                            // Directory
                            if (entry.Length != 0)
                                throw new IOException("Directory entry with data.");
                            Directory.CreateDirectory(fileDestinationPath);
                        }
                        else
                        {
                            // File
                            // Create containing directory
                            Directory.CreateDirectory(Path.GetDirectoryName(fileDestinationPath));
                            entry.ExtractToFile(fileDestinationPath);
                        }
                    }
                }
            }
            File.Delete(FileUpdateStage);
            TriggerApplyProgressChanged(progressMin + progressLength);
            return Task.CompletedTask;
        }

        private async Task<bool> UpdateIsReadyAync()
        {
            // The staging directory must exist, contain OpenRails.exe (be ready) and NOT contain the update zip.
            if (Directory.Exists(Path.Combine(PathUpdateStage, "Program")) && !File.Exists(Path.Combine(PathUpdateStage, LauncherExecutable)))
            {
                //looks like the archive contains the root folder as well, so we move everything one level up
                await MoveDirectoryFiles(Path.Combine(PathUpdateStage, "Program"), PathUpdateStage, true);
            }

            return await Task.FromResult(Directory.Exists(PathUpdateStage)
                && File.Exists(Path.Combine(PathUpdateStage, LauncherExecutable))
                && !File.Exists(FileUpdateStage));
        }

        private Task VerifyUpdateAsync()
        {
            IEnumerable<string> files = Directory.GetFiles(PathUpdateStage, "*", SearchOption.AllDirectories).Where(s =>
                    s.ToUpper().EndsWith(".EXE") ||
                    s.ToUpper().EndsWith(".CPL") ||
                    s.ToUpper().EndsWith(".DLL") ||
                    s.ToUpper().EndsWith(".OCX") ||
                    s.ToUpper().EndsWith(".SYS"));

            HashSet<string> expectedSubjects = new HashSet<string>();
            try
            {
                foreach (X509Certificate2 cert in GetCertificatesFromFile(FileUpdater))
                    expectedSubjects.Add(cert.Subject);
            }
            catch (Exception ex) when (ex is CryptographicException || ex is Win32Exception)
            {
                // No signature on the updater, so we can't verify the update. :(
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                // No signature on the updater, so we can't verify the update. :(
                return Task.FromException(ex);
            }

            foreach(string file in files)
            {
                List<X509Certificate2> certificates = GetCertificatesFromFile(file);
                if (!certificates.Any(c => expectedSubjects.Contains(c.Subject)))
                    throw new InvalidDataException("Cryptographic signatures don't match. Expected a common subject in old subjects:\n\n"
                        + FormatCertificateSubjectList(expectedSubjects) + "\n\nAnd new subjects:\n\n"
                        + FormatCertificateSubjectList(certificates) + "\n");
            }
            return Task.CompletedTask;
        }

        private async Task CopyUpdateFileAsync()
        {
            string[] excludeDirs = Directory.Exists(PathUpdateDocumentation) ? new string [] { PathUpdateDirty, PathUpdateStage } : new string[] { PathUpdateDirty, PathUpdateStage, PathDocumentation};
            await MoveDirectoryFiles(basePath, PathUpdateDirty, true, excludeDirs, new string[] { FileSettings }).ConfigureAwait(false);

            await MoveDirectoryFiles(PathUpdateStage, basePath, true).ConfigureAwait(false);
        }

        private static Task MoveDirectoryFiles(string sourceDirName, string destDirName, bool recursive, 
            string[] excludedFolders = null, string[] excludedFiles = null)
        {
            if (null != excludedFolders && excludedFolders.Contains(sourceDirName))
            {
                return Task.CompletedTask;
            }

            // Get the subdirectories for the specified directory.
            DirectoryInfo source = new DirectoryInfo(sourceDirName);
            if (!source.Exists)
            {
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);
            }
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            Parallel.ForEach(source.GetFiles(),
                 (file) =>
                 {
                     if (null == excludedFiles || !excludedFiles.Contains(file.FullName))
                         file.MoveTo(Path.Combine(destDirName, file.Name));
                 });

            // If copying subdirectories, copy them and their contents to new location.
            if (recursive)
            {
                Parallel.ForEach(source.GetDirectories(),
                     (directory) =>
                     {
                         MoveDirectoryFiles(directory.FullName, Path.Combine(destDirName, directory.Name), recursive, excludedFolders, excludedFiles);
                     });
            }

            try { source.Delete(); }
            catch (Exception ex) { Trace.TraceWarning($"{sourceDirName} :: {ex.Message}"); };
            return Task.CompletedTask;
        }

        public static Task RunProcess(ProcessStartInfo processStartInfo)
        {
            var tcs = new TaskCompletionSource<object>();
            processStartInfo.RedirectStandardError = true;
            processStartInfo.UseShellExecute = false;

            Process process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = processStartInfo
            };

            process.Exited += (sender, args) =>
            {
                if (process.ExitCode != 0)
                {
                    var errorMessage = process.StandardError.ReadToEnd();
                    tcs.SetException(new InvalidOperationException("The process did not exit correctly. " +
                        "The corresponding error message was: " + errorMessage));
                }
                else
                {
                    tcs.SetResult(null);
                }
                process.Dispose();
            };
            process.Start();
            return tcs.Task;
        }


        static string FormatCertificateSubjectList(IEnumerable<string> subjects)
        {
            return string.Join("\n", subjects.Select(s => "- " + s).ToArray());
        }

        static string FormatCertificateSubjectList(IEnumerable<X509Certificate2> certificates)
        {
            return FormatCertificateSubjectList(certificates.Select(c => c.Subject));
        }

        static List<X509Certificate2> GetCertificatesFromFile(string filename)
        {
            IntPtr cryptMsg = IntPtr.Zero;
            if (!NativeMethods.CryptQueryObject(NativeMethods.CERT_QUERY_OBJECT_FILE, filename, NativeMethods.CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED_EMBED, NativeMethods.CERT_QUERY_FORMAT_FLAG_ALL, 0, 0, 0, 0, 0, ref cryptMsg, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            // Get size of the encoded message.
            int dataSize = 0;
            if (!NativeMethods.CryptMsgGetParam(cryptMsg, NativeMethods.CMSG_ENCODED_MESSAGE, 0, IntPtr.Zero, ref dataSize))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            // Get the encoded message.
            var data = new byte[dataSize];
            if (!NativeMethods.CryptMsgGetParam(cryptMsg, NativeMethods.CMSG_ENCODED_MESSAGE, 0, data, ref dataSize))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            return GetCertificatesFromEncodedData(data);
        }

        static List<X509Certificate2> GetCertificatesFromEncodedData(byte[] data)
        {
            var certs = new List<X509Certificate2>();

            var signedCms = new SignedCms();
            signedCms.Decode(data);

            foreach (var signerInfo in signedCms.SignerInfos)
            {
                // Record this signer info's certificate if it has one.
                if (signerInfo.Certificate != null)
                    certs.Add(signerInfo.Certificate);

                foreach (var unsignedAttribute in signerInfo.UnsignedAttributes)
                {
                    // This attribute Oid is for "code signatures" and is used to attach multiple signatures to a single item.
                    if (unsignedAttribute.Oid.Value == "1.3.6.1.4.1.311.2.4.1")
                    {
                        foreach (var value in unsignedAttribute.Values)
                            certs.AddRange(GetCertificatesFromEncodedData(value.RawData));
                    }
                }
            }

            return certs;
        }
    }

    public class Update
    {
        [JsonProperty]
        public DateTime Date { get; private set; }

        [JsonProperty]
        public string Url { get; private set; }

        [JsonProperty]
        public string Version { get; private set; }
    }
}
