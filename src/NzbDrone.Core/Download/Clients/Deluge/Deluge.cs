using System;
using System.Linq;
using System.Collections.Generic;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Http;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles.TorrentInfo;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Validation;
using NLog;
using FluentValidation.Results;
using System.Net;
using NzbDrone.Core.RemotePathMappings;

namespace NzbDrone.Core.Download.Clients.Deluge
{
    public class Deluge : TorrentClientBase<DelugeSettings>
    {
        private readonly IDelugeProxy _proxy;

        public Deluge(IDelugeProxy proxy,
                      ITorrentFileInfoReader torrentFileInfoReader,
                      IHttpClient httpClient,
                      IConfigService configService,
                      IDiskProvider diskProvider,
                      IRemotePathMappingService remotePathMappingService,
                      Logger logger)
            : base(torrentFileInfoReader, httpClient, configService, diskProvider, remotePathMappingService, logger)
        {
            _proxy = proxy;
        }

        protected override string AddFromMagnetLink(RemoteEpisode remoteEpisode, string hash, string magnetLink)
        {
            var actualHash = _proxy.AddTorrentFromMagnet(magnetLink, Settings);

            if (actualHash.IsNullOrWhiteSpace())
            {
                throw new DownloadClientException("Deluge failed to add magnet " + magnetLink);
            }

            if (!Settings.TvCategory.IsNullOrWhiteSpace())
            {
                _proxy.SetLabel(actualHash, Settings.TvCategory, Settings);
            }

            _proxy.SetTorrentSeedingConfiguration(actualHash, remoteEpisode.SeedConfiguration, Settings);

            var isRecentEpisode = remoteEpisode.IsRecentEpisode();

            if (isRecentEpisode && Settings.RecentTvPriority == (int)DelugePriority.First ||
                !isRecentEpisode && Settings.OlderTvPriority == (int)DelugePriority.First)
            {
                _proxy.MoveTorrentToTopInQueue(actualHash, Settings);
            }

            return actualHash.ToUpper();
        }

        protected override string AddFromTorrentFile(RemoteEpisode remoteEpisode, string hash, string filename, byte[] fileContent)
        {
            var actualHash = _proxy.AddTorrentFromFile(filename, fileContent, Settings);

            if (actualHash.IsNullOrWhiteSpace())
            {
                throw new DownloadClientException("Deluge failed to add torrent " + filename);
            }

            _proxy.SetTorrentSeedingConfiguration(actualHash, remoteEpisode.SeedConfiguration, Settings);

            if (!Settings.TvCategory.IsNullOrWhiteSpace())
            {
                _proxy.SetLabel(actualHash, Settings.TvCategory, Settings);
            }

            var isRecentEpisode = remoteEpisode.IsRecentEpisode();

            if (isRecentEpisode && Settings.RecentTvPriority == (int)DelugePriority.First ||
                !isRecentEpisode && Settings.OlderTvPriority == (int)DelugePriority.First)
            {
                _proxy.MoveTorrentToTopInQueue(actualHash, Settings);
            }

            return actualHash.ToUpper();
        }

        public override string Name => "Deluge";

        public override IEnumerable<DownloadClientItem> GetItems()
        {
            IEnumerable<DelugeTorrent> torrents;

            if (!Settings.TvCategory.IsNullOrWhiteSpace())
            {
                torrents = _proxy.GetTorrentsByLabel(Settings.TvCategory, Settings);
            }
            else
            {
                torrents = _proxy.GetTorrents(Settings);
            }

            var items = new List<DownloadClientItem>();

            foreach (var torrent in torrents)
            {
                if (torrent.Hash == null) continue;
                var item = new DownloadClientItem();
                item.DownloadId = torrent.Hash.ToUpper();
                item.Title = torrent.Name;
                item.Category = Settings.TvCategory;

                item.DownloadClient = Definition.Name;

                var outputPath = _remotePathMappingService.RemapRemoteToLocal(Settings.Host, new OsPath(torrent.DownloadPath));
                item.OutputPath = outputPath + torrent.Name;
                item.RemainingSize = torrent.Size - torrent.BytesDownloaded;
                item.SeedRatio = torrent.Ratio;

                try
                {
                    item.RemainingTime = TimeSpan.FromSeconds(torrent.Eta);
                }
                catch (OverflowException ex)
                {
                    _logger.Debug(ex, "ETA for {0} is too long: {1}", torrent.Name, torrent.Eta);
                    item.RemainingTime = TimeSpan.MaxValue;
                }

                item.TotalSize = torrent.Size;

                if (torrent.State == DelugeTorrentStatus.Error)
                {
                    item.Status = DownloadItemStatus.Warning;
                    item.Message = "Deluge is reporting an error";
                }
                else if (torrent.IsFinished && torrent.State != DelugeTorrentStatus.Checking)
                {
                    item.Status = DownloadItemStatus.Completed;
                }
                else if (torrent.State == DelugeTorrentStatus.Queued)
                {
                    item.Status = DownloadItemStatus.Queued;
                }
                else if (torrent.State == DelugeTorrentStatus.Paused)
                {
                    item.Status = DownloadItemStatus.Paused;
                }
                else
                {
                    item.Status = DownloadItemStatus.Downloading;
                }

                // Here we detect if Deluge is managing the torrent and whether the seed criteria has been met.
                // This allows drone to delete the torrent as appropriate.
                item.CanMoveFiles = item.CanBeRemoved =
                    torrent.IsAutoManaged &&
                    torrent.StopAtRatio &&
                    torrent.Ratio >= torrent.StopRatio &&
                    torrent.State == DelugeTorrentStatus.Paused;

                items.Add(item);
            }

            return items;
        }

        public override void RemoveItem(string downloadId, bool deleteData)
        {
            _proxy.RemoveTorrent(downloadId.ToLower(), deleteData, Settings);
        }

        public override DownloadClientInfo GetStatus()
        {
            var config = _proxy.GetConfig(Settings);

            var destDir = new OsPath(config.GetValueOrDefault("download_location") as string);

            if (config.GetValueOrDefault("move_completed", false).ToString() == "True")
            {
                destDir = new OsPath(config.GetValueOrDefault("move_completed_path") as string);
            }

            var status = new DownloadClientInfo
            {
                IsLocalhost = Settings.Host == "127.0.0.1" || Settings.Host == "localhost"
            };

            if (!destDir.IsEmpty)
            {
                status.OutputRootFolders = new List<OsPath> { _remotePathMappingService.RemapRemoteToLocal(Settings.Host, destDir) };
            }

            return status;
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            failures.AddIfNotNull(TestConnection());
            if (failures.Any()) return;
            failures.AddIfNotNull(TestCategory());
            failures.AddIfNotNull(TestGetTorrents());
        }

        private ValidationFailure TestConnection()
        {
            try
            {
                _proxy.GetVersion(Settings);
            }
            catch (DownloadClientAuthenticationException ex)
            {
                _logger.Error(ex, ex.Message);

                return new NzbDroneValidationFailure("Password", "Authentication failed");
            }
            catch (WebException ex)
            {
                _logger.Error(ex, "Unble to test connection");
                switch (ex.Status)
                {
                    case WebExceptionStatus.ConnectFailure:
                        return new NzbDroneValidationFailure("Host", "Unable to connect")
                        {
                            DetailedDescription = "Please verify the hostname and port."
                        };
                    case WebExceptionStatus.ConnectionClosed:
                        return new NzbDroneValidationFailure("UseSsl", "Verify SSL settings")
                        {
                            DetailedDescription = "Please verify your SSL configuration on both Deluge and NzbDrone."
                        };
                    case WebExceptionStatus.SecureChannelFailure:
                        return new NzbDroneValidationFailure("UseSsl", "Unable to connect through SSL")
                        {
                            DetailedDescription = "Drone is unable to connect to Deluge using SSL. This problem could be computer related. Please try to configure both drone and Deluge to not use SSL."
                        };
                    default:
                        return new NzbDroneValidationFailure(string.Empty, "Unknown exception: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to test connection");
                return new NzbDroneValidationFailure(string.Empty, "Unknown exception: " + ex.Message);
            }

            return null;
        }

        private ValidationFailure TestCategory()
        {
            if (Settings.TvCategory.IsNullOrWhiteSpace())
            {
                return null;
            }

            var enabledPlugins = _proxy.GetEnabledPlugins(Settings);

            if (!enabledPlugins.Contains("Label"))
            {
                return new NzbDroneValidationFailure("TvCategory", "Label plugin not activated")
                {
                    DetailedDescription = "You must have the Label plugin enabled in Deluge to use categories."
                };
            }

            var labels = _proxy.GetAvailableLabels(Settings);

            if (!labels.Contains(Settings.TvCategory))
            {
                _proxy.AddLabel(Settings.TvCategory, Settings);
                labels = _proxy.GetAvailableLabels(Settings);

                if (!labels.Contains(Settings.TvCategory))
                {
                    return new NzbDroneValidationFailure("TvCategory", "Configuration of label failed")
                    {
                        DetailedDescription = "Sonarr as unable to add the label to Deluge."
                    };
                }
            }

            return null;
        }

        private ValidationFailure TestGetTorrents()
        {
            try
            {
                _proxy.GetTorrents(Settings);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to get torrents");
                return new NzbDroneValidationFailure(string.Empty, "Failed to get the list of torrents: " + ex.Message);
            }

            return null;
        }
    }
}
