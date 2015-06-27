﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Common.TPL;
using System.Collections;
using System;
namespace NzbDrone.Core.Indexers
{
    public interface IFetchAndParseRss
    {
        List<ReleaseInfo> Fetch();
    }

    public class FetchAndParseRssService : IFetchAndParseRss
    {
        private readonly IIndexerFactory _indexerFactory;
        private readonly IIndexerStatusService _indexerStatusService;
        private readonly Logger _logger;

        public FetchAndParseRssService(IIndexerFactory indexerFactory, IIndexerStatusService indexerStatusService, Logger logger)
        {
            _indexerFactory = indexerFactory;
            _indexerStatusService = indexerStatusService;
            _logger = logger;
        }

        public List<ReleaseInfo> Fetch()
        {
            var result = new List<ReleaseInfo>();

            var indexers = _indexerFactory.RssEnabled().ToList();

            if (!indexers.Any())
            {
                _logger.Warn("No available indexers. check your configuration.");
                return result;
            }

            _logger.Debug("Available indexers {0}", indexers.Count);

            var taskList = new List<Task>();
            var taskFactory = new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.None);

            foreach (var indexer in indexers)
            {
                var backOff = _indexerStatusService.GetBackOffDate(indexer.Definition.Id);
                if (backOff > DateTime.UtcNow)
                {
                    _logger.Debug("Temporarily backing off on {0} till {1}.", indexer.Definition.Name, backOff);
                    continue;
                }

                var indexerLocal = indexer;

                var task = taskFactory.StartNew(() =>
                     {
                         var indexerFeed = indexerLocal.FetchRecent();

                         lock (result)
                         {
                             result.AddRange(indexerFeed);
                         }
                     }).LogExceptions();

                taskList.Add(task);
            }

            Task.WaitAll(taskList.ToArray());

            _logger.Debug("Found {0} reports", result.Count);

            return result;
        }
    }
}