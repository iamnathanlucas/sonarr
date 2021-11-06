using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation.Aggregators.Augmenters.Language;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation.Aggregators
{
    public class AggregateLanguage : IAggregateLocalEpisode
    {
        private readonly List<IAugmentLanguage> _augmentLanguages;
        private readonly Logger _logger;

        public AggregateLanguage(IEnumerable<IAugmentLanguage> augmentLanguages, Logger logger)
        {
            _augmentLanguages = augmentLanguages.OrderBy(a => a.Order).ToList();
            _logger = logger;
        }

        public LocalEpisode Aggregate(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            var languages = new List<Language> { };
            var languagesConfidence = Confidence.Default;

            foreach (var augmentLanguage in _augmentLanguages)
            {
                var augmentedLanguage = augmentLanguage.AugmentLanguage(localEpisode, downloadClientItem);
                if (augmentedLanguage == null)
                {
                    continue;
                }

                _logger.Trace("Considering Languages {0} ({1}) from {2}", string.Join(", ", augmentedLanguage.Languages ?? new List<Language>()), augmentedLanguage.Confidence, augmentLanguage.Name);

                if (augmentedLanguage?.Languages != null && augmentedLanguage.Languages.Count > 0 && !(augmentedLanguage.Languages.Count == 1 && augmentedLanguage.Languages.Contains(Language.Unknown)))
                {
                    languages = augmentedLanguage.Languages;
                    languagesConfidence = augmentedLanguage.Confidence;
                }
            }


            var language = languages.FirstOrDefault(l => l != Language.English) ?? Language.English;

            _logger.Debug("Using language: {0}", language);

            localEpisode.Language = language;

            return localEpisode;
        }
    }
}
