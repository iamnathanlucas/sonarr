﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Common.TPL;

namespace NzbDrone.Core.Indexers
{
    public interface IFetchAndParseRss
    {
        List<ReleaseInfo> Fetch();
    }

    public class FetchAndParseRssService : IFetchAndParseRss
    {
        private readonly IIndexerFactory _indexerFactory;
        private readonly IFetchFeedFromIndexers _feedFetcher;
        private readonly Logger _logger;

        public FetchAndParseRssService(IIndexerFactory indexerFactory, IFetchFeedFromIndexers feedFetcher, Logger logger)
        {
            _indexerFactory = indexerFactory;
            _feedFetcher = feedFetcher;
            _logger = logger;
        }

        public List<ReleaseInfo> Fetch()
        {
            var result = new List<ReleaseInfo>();

            var indexers = _indexerFactory.GetAvailableProviders().ToList();

            if (!indexers.Any())
            {
                _logger.Warn("No available indexers. check your configuration.");
                return result;
            }

            _logger.Debug("Available indexers {0}", indexers.Count);

            Parallel.ForEach(indexers, (indexer) =>
                    {
                        var indexerFeed = _feedFetcher.FetchRss(indexer);

                        lock (result)
                        {
                            result.AddRange(indexerFeed);
                        }
                    });

            _logger.Debug("Found {0} reports", result.Count);

            return result;
        }
    }
}