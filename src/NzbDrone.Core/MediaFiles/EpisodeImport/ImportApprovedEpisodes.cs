using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Extras;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles.EpisodeImport
{
    public interface IImportApprovedEpisodes
    {
        List<ImportResult> Import(List<ImportDecision> decisions, bool newDownload, DownloadClientItem downloadClientItem = null, ImportMode importMode = ImportMode.Auto);
    }

    public class ImportApprovedEpisodes : IImportApprovedEpisodes
    {
        private readonly IUpgradeMediaFiles _episodeFileUpgrader;
        private readonly IMediaFileService _mediaFileService;
        private readonly IExtraService _extraService;
        private readonly IUpdateMediaInfo _updateMediaInfo;
        private readonly IRenameEpisodeFileService _renameEpisodeFileService;
        private readonly IDiskProvider _diskProvider;
        private readonly IEventAggregator _eventAggregator;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public ImportApprovedEpisodes(IUpgradeMediaFiles episodeFileUpgrader,
                                      IMediaFileService mediaFileService,
                                      IExtraService extraService,
                                      IUpdateMediaInfo updateMediaInfo,
                                      IRenameEpisodeFileService renameEpisodeFileService,
                                      IDiskProvider diskProvider,
                                      IEventAggregator eventAggregator,
                                      IManageCommandQueue commandQueueManager,
                                      Logger logger)
        {
            _episodeFileUpgrader = episodeFileUpgrader;
            _mediaFileService = mediaFileService;
            _extraService = extraService;
            _updateMediaInfo = updateMediaInfo;
            _renameEpisodeFileService = renameEpisodeFileService;
            _diskProvider = diskProvider;
            _eventAggregator = eventAggregator;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        public List<ImportResult> Import(List<ImportDecision> decisions, bool newDownload, DownloadClientItem downloadClientItem = null, ImportMode importMode = ImportMode.Auto)
        {
            var qualifiedImports = decisions
                .Where(decision => decision.Approved)
                .GroupBy(decision => decision.LocalEpisode.Series.Id)
                .SelectMany(group => group
                    .OrderByDescending(decision => decision.LocalEpisode.Quality, new QualityModelComparer(group.First().LocalEpisode.Series.QualityProfile))
                    .ThenByDescending(decision => decision.LocalEpisode.Size))
                .ToList();

            var importResults = new List<ImportResult>();

            foreach (var importDecision in qualifiedImports.OrderBy(e => e.LocalEpisode.Episodes.Select(episode => episode.EpisodeNumber).MinOrDefault())
                                                           .ThenByDescending(e => e.LocalEpisode.Size))
            {
                var localEpisode = importDecision.LocalEpisode;
                var oldFiles = new List<EpisodeFile>();

                try
                {
                    // check if already imported
                    if (importResults.SelectMany(r => r.ImportDecision.LocalEpisode.Episodes)
                                         .Select(e => e.Id)
                                         .Intersect(localEpisode.Episodes.Select(e => e.Id))
                                         .Any())
                    {
                        importResults.Add(new ImportResult(importDecision, "Episode has already been imported"));
                        continue;
                    }

                    var episodeFile = new EpisodeFile();
                    episodeFile.DateAdded = DateTime.UtcNow;
                    episodeFile.SeriesId = localEpisode.Series.Id;
                    episodeFile.Path = localEpisode.Path.CleanFilePath();
                    episodeFile.Size = _diskProvider.GetFileSize(localEpisode.Path);
                    episodeFile.Quality = localEpisode.Quality;
                    episodeFile.MediaInfo = localEpisode.MediaInfo;
                    episodeFile.SeasonNumber = localEpisode.SeasonNumber;
                    episodeFile.Episodes = localEpisode.Episodes;
                    episodeFile.ReleaseGroup = localEpisode.ReleaseGroup;
                    episodeFile.Languages = localEpisode.Languages;

                    bool copyOnly;
                    switch (importMode)
                    {
                        default:
                        case ImportMode.Auto:
                            copyOnly = downloadClientItem != null && !downloadClientItem.CanMoveFiles;
                            break;
                        case ImportMode.Move:
                            copyOnly = false;
                            break;
                        case ImportMode.Copy:
                            copyOnly = true;
                            break;
                    }

                    var scriptImportDecisionInfo = new ScriptImportDecisionInfo
                    {
                        downloadClientItemInfo = downloadClientItem?.DownloadClientInfo,
                        downloadId = downloadClientItem?.DownloadId,
                        episodeFile = episodeFile,
                        localEpisode = localEpisode,
                        isEpisodeFile = true,
                    };

                    bool rename = false;

                    if (newDownload)
                    {
                        episodeFile.SceneName = localEpisode.SceneName;
                        episodeFile.OriginalFilePath = GetOriginalFilePath(downloadClientItem, localEpisode);

                        var (moveResult, needsRename) = _episodeFileUpgrader.UpgradeEpisodeFile(episodeFile, localEpisode, scriptImportDecisionInfo, copyOnly);
                        rename = needsRename;
                        oldFiles = moveResult.OldFiles;
                    }
                    else
                    {
                        episodeFile.RelativePath = localEpisode.Series.Path.GetRelativePath(episodeFile.Path);

                        // Delete existing files from the DB mapped to this path
                        var previousFiles = _mediaFileService.GetFilesWithRelativePath(localEpisode.Series.Id, episodeFile.RelativePath);

                        foreach (var previousFile in previousFiles)
                        {
                            _mediaFileService.Delete(previousFile, DeleteMediaFileReason.ManualOverride);
                        }
                    }

                    episodeFile = _mediaFileService.Add(episodeFile);
                    importResults.Add(new ImportResult(importDecision));

                    if (rename)
                    {
                        _logger.Debug("Rename requested, renaming...");
                        _updateMediaInfo.Update(episodeFile, localEpisode.Series);

                        episodeFile.Path = null;
                        _renameEpisodeFileService.RenameFile(episodeFile, localEpisode.Series);
                    }

                    if (newDownload)
                    {
                        scriptImportDecisionInfo.isEpisodeFile = false;
                        _extraService.ImportEpisode(localEpisode, episodeFile, scriptImportDecisionInfo, copyOnly);
                    }

                    _eventAggregator.PublishEvent(new EpisodeImportedEvent(localEpisode, episodeFile, oldFiles, newDownload, downloadClientItem));
                }
                catch (RootFolderNotFoundException e)
                {
                    _logger.Warn(e, "Couldn't import episode " + localEpisode);
                    _eventAggregator.PublishEvent(new EpisodeImportFailedEvent(e, localEpisode, newDownload, downloadClientItem));

                    importResults.Add(new ImportResult(importDecision, "Failed to import episode, Root folder missing."));
                }
                catch (DestinationAlreadyExistsException e)
                {
                    _logger.Warn(e, "Couldn't import episode " + localEpisode);
                    importResults.Add(new ImportResult(importDecision, "Failed to import episode, Destination already exists."));

                    _commandQueueManager.Push(new RescanSeriesCommand(localEpisode.Series.Id));
                }
                catch (Exception e)
                {
                    _logger.Warn(e, "Couldn't import episode " + localEpisode);
                    importResults.Add(new ImportResult(importDecision, "Failed to import episode"));
                }
            }

            // Adding all the rejected decisions
            importResults.AddRange(decisions.Where(c => !c.Approved)
                                            .Select(d => new ImportResult(d, d.Rejections.Select(r => r.Reason).ToArray())));

            return importResults;
        }

        private string GetOriginalFilePath(DownloadClientItem downloadClientItem, LocalEpisode localEpisode)
        {
            var path = localEpisode.Path;

            if (downloadClientItem != null && !downloadClientItem.OutputPath.IsEmpty)
            {
                var outputDirectory = downloadClientItem.OutputPath.Directory.ToString();

                if (outputDirectory.IsParentPath(path))
                {
                    return outputDirectory.GetRelativePath(path);
                }
            }

            var folderEpisodeInfo = localEpisode.FolderEpisodeInfo;

            if (folderEpisodeInfo != null)
            {
                var folderPath = path.GetAncestorPath(folderEpisodeInfo.ReleaseTitle);

                if (folderPath != null)
                {
                    return folderPath.GetParentPath().GetRelativePath(path);
                }
            }

            var parentPath = path.GetParentPath();
            var grandparentPath = parentPath.GetParentPath();

            if (grandparentPath != null)
            {
                return grandparentPath.GetRelativePath(path);
            }

            return Path.GetFileName(path);
        }
    }
}
