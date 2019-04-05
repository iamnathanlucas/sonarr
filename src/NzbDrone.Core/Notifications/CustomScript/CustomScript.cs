using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Processes;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Notifications.CustomScript
{
    public class CustomScript : NotificationBase<CustomScriptSettings>
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IProcessProvider _processProvider;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public CustomScript(IDiskProvider diskProvider, IAppFolderInfo appFolderInfo, IProcessProvider processProvider, IConfigService configService, Logger logger)
        {
            _diskProvider = diskProvider;
            _appFolderInfo = appFolderInfo;
            _processProvider = processProvider;
            _configService = configService;
            _logger = logger;
        }

        public override string Name => "Custom Script";

        public override string Link => "https://github.com/Sonarr/Sonarr/wiki/Custom-Post-Processing-Scripts";

        public override ProviderMessage Message => new ProviderMessage("Testing will execute the script with the EventType set to Test, ensure your script handles this correctly", ProviderMessageType.Warning);

        public override void OnGrab(GrabMessage message)
        {
            var series = message.Series;
            var remoteEpisode = message.Episode;
            var releaseGroup = remoteEpisode.ParsedEpisodeInfo.ReleaseGroup;
            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Sonarr_EventType", "Grab");
            environmentVariables.Add("Sonarr_Series_Id", series.Id.ToString());
            environmentVariables.Add("Sonarr_Series_Title", series.Title);
            environmentVariables.Add("Sonarr_Series_TvdbId", series.TvdbId.ToString());
            environmentVariables.Add("Sonarr_Series_TvMazeId", series.TvMazeId.ToString());
            environmentVariables.Add("Sonarr_Series_ImdbId", series.ImdbId ?? string.Empty);
            environmentVariables.Add("Sonarr_Series_Type", series.SeriesType.ToString());
            environmentVariables.Add("Sonarr_Release_EpisodeCount", remoteEpisode.Episodes.Count.ToString());
            environmentVariables.Add("Sonarr_Release_SeasonNumber", remoteEpisode.Episodes.First().SeasonNumber.ToString());
            environmentVariables.Add("Sonarr_Release_EpisodeNumbers", string.Join(",", remoteEpisode.Episodes.Select(e => e.EpisodeNumber)));
            environmentVariables.Add("Sonarr_Release_AbsoluteEpisodeNumbers", string.Join(",", remoteEpisode.Episodes.Select(e => e.AbsoluteEpisodeNumber)));
            environmentVariables.Add("Sonarr_Release_EpisodeAirDates", string.Join(",", remoteEpisode.Episodes.Select(e => e.AirDate)));
            environmentVariables.Add("Sonarr_Release_EpisodeAirDatesUtc", string.Join(",", remoteEpisode.Episodes.Select(e => e.AirDateUtc)));
            environmentVariables.Add("Sonarr_Release_EpisodeTitles", string.Join("|", remoteEpisode.Episodes.Select(e => e.Title)));
            environmentVariables.Add("Sonarr_Release_Title", remoteEpisode.Release.Title);
            environmentVariables.Add("Sonarr_Release_Indexer", remoteEpisode.Release.Indexer ?? string.Empty);
            environmentVariables.Add("Sonarr_Release_Size", remoteEpisode.Release.Size.ToString());
            environmentVariables.Add("Sonarr_Release_Quality", remoteEpisode.ParsedEpisodeInfo.Quality.Quality.Name);
            environmentVariables.Add("Sonarr_Release_QualityVersion", remoteEpisode.ParsedEpisodeInfo.Quality.Revision.Version.ToString());
            environmentVariables.Add("Sonarr_Release_ReleaseGroup", releaseGroup ?? string.Empty);
            environmentVariables.Add("Sonarr_Download_Client", message.DownloadClient ?? string.Empty);
            environmentVariables.Add("Sonarr_Download_Id", message.DownloadId ?? string.Empty);

            ExecuteScript(environmentVariables);
        }

        public override void OnDownload(DownloadMessage message)
        {
            var series = message.Series;
            var episodeFile = message.EpisodeFile;
            var sourcePath = message.SourcePath;
            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Sonarr_EventType", "Download");
            environmentVariables.Add("Sonarr_IsUpgrade", message.OldFiles.Any().ToString());
            environmentVariables.Add("Sonarr_Series_Id", series.Id.ToString());
            environmentVariables.Add("Sonarr_Series_Title", series.Title);
            environmentVariables.Add("Sonarr_Series_Path", series.Path);
            environmentVariables.Add("Sonarr_Series_TvdbId", series.TvdbId.ToString());
            environmentVariables.Add("Sonarr_Series_TvMazeId", series.TvMazeId.ToString());
            environmentVariables.Add("Sonarr_Series_ImdbId", series.ImdbId ?? string.Empty);
            environmentVariables.Add("Sonarr_Series_Type", series.SeriesType.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_Id", episodeFile.Id.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeCount", episodeFile.Episodes.Value.Count.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_RelativePath", episodeFile.RelativePath);
            environmentVariables.Add("Sonarr_EpisodeFile_Path", Path.Combine(series.Path, episodeFile.RelativePath));
            environmentVariables.Add("Sonarr_EpisodeFile_SeasonNumber", episodeFile.SeasonNumber.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeNumbers", string.Join(",", episodeFile.Episodes.Value.Select(e => e.EpisodeNumber)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeAirDates", string.Join(",", episodeFile.Episodes.Value.Select(e => e.AirDate)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeAirDatesUtc", string.Join(",", episodeFile.Episodes.Value.Select(e => e.AirDateUtc)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeTitles", string.Join("|", episodeFile.Episodes.Value.Select(e => e.Title)));
            environmentVariables.Add("Sonarr_EpisodeFile_Quality", episodeFile.Quality.Quality.Name);
            environmentVariables.Add("Sonarr_EpisodeFile_QualityVersion", episodeFile.Quality.Revision.Version.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_ReleaseGroup", episodeFile.ReleaseGroup ?? string.Empty);
            environmentVariables.Add("Sonarr_EpisodeFile_SceneName", episodeFile.SceneName ?? string.Empty);
            environmentVariables.Add("Sonarr_EpisodeFile_SourcePath", sourcePath);
            environmentVariables.Add("Sonarr_EpisodeFile_SourceFolder", Path.GetDirectoryName(sourcePath));
            environmentVariables.Add("Sonarr_Download_Client", message.DownloadClient ?? string.Empty);
            environmentVariables.Add("Sonarr_Download_Id", message.DownloadId ?? string.Empty);

            if (message.OldFiles.Any())
            {
                environmentVariables.Add("Sonarr_DeletedRelativePaths", string.Join("|", message.OldFiles.Select(e => e.RelativePath)));
                environmentVariables.Add("Sonarr_DeletedPaths", string.Join("|", message.OldFiles.Select(e => Path.Combine(series.Path, e.RelativePath))));
            }

            ExecuteScript(environmentVariables);
        }

        public override void OnRename(Series series)
        {
            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Sonarr_EventType", "Rename");
            environmentVariables.Add("Sonarr_Series_Id", series.Id.ToString());
            environmentVariables.Add("Sonarr_Series_Title", series.Title);
            environmentVariables.Add("Sonarr_Series_Path", series.Path);
            environmentVariables.Add("Sonarr_Series_TvdbId", series.TvdbId.ToString());
            environmentVariables.Add("Sonarr_Series_TvMazeId", series.TvMazeId.ToString());
            environmentVariables.Add("Sonarr_Series_ImdbId", series.ImdbId ?? string.Empty);
            environmentVariables.Add("Sonarr_Series_Type", series.SeriesType.ToString());

            ExecuteScript(environmentVariables);
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();
            var scriptsPath = GetScriptsFolder();

            if (!scriptsPath.IsParentPath(Settings.Path))
            {
                failures.Add(new NzbDroneValidationFailure("Path", $"Must be in '{scriptsPath}'"));
            }

            if (!_diskProvider.FileExists(Settings.Path))
            {
                failures.Add(new NzbDroneValidationFailure("Path", "File does not exist"));
            }

            try
            {
                var environmentVariables = new StringDictionary();
                environmentVariables.Add("Sonarr_EventType", "Test");

                var processOutput = ExecuteScript(environmentVariables);

                if (processOutput.ExitCode != 0)
                {
                    failures.Add(new NzbDroneValidationFailure(string.Empty, $"Script exited with code: {processOutput.ExitCode}"));
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                failures.Add(new NzbDroneValidationFailure(string.Empty, ex.Message));
            }

            return new ValidationResult(failures);
        }


        public override object RequestAction(string action, IDictionary<string, string> query)
        {
            if (action == "getFiles")
            {
                var scriptsPath = GetScriptsFolder();

                if (!_diskProvider.FolderExists(scriptsPath))
                {
                    return new { };
                }

                var files = _diskProvider.GetFiles(scriptsPath, SearchOption.TopDirectoryOnly);

                return new
                       {
                           options = files.Select(f => new
                                                       {
                                                           key = f,
                                                           value = Path.GetFileName(f)
                                                       })
                       };
            }

            return new { };
        }

        private string GetScriptsFolder()
        {
            var scriptFolder = _configService.ScriptFolder;

            if (Path.IsPathRooted(scriptFolder))
            {
                return scriptFolder;
            }

            return Path.Combine(_appFolderInfo.GetAppDataPath(), scriptFolder);
        }

        private ProcessOutput ExecuteScript(StringDictionary environmentVariables)
        {
            var scriptsPath = GetScriptsFolder();

            if (!scriptsPath.IsParentPath(Settings.Path))
            {
                _logger.Error("External script must be run from the specified scripts folder: {0}", scriptsPath);
            }

            _logger.Debug("Executing external script: {0}", Settings.Path);

            var processOutput = _processProvider.StartAndCapture(Settings.Path, Settings.Arguments, environmentVariables);

            _logger.Debug("Executed external script: {0} - Status: {1}", Settings.Path, processOutput.ExitCode);
            _logger.Debug("Script Output: \r\n{0}", string.Join("\r\n", processOutput.Lines));

            return processOutput;
        }
    }
}
