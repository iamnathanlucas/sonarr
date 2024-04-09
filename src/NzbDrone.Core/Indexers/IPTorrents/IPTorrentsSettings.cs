using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.IPTorrents
{
    public class IPTorrentsSettingsValidator : AbstractValidator<IPTorrentsSettings>
    {
        public IPTorrentsSettingsValidator()
        {
            RuleFor(c => c.BaseUrl).ValidRootUrl();

            RuleFor(c => c.BaseUrl).Matches(@"(?:/|t\.)rss\?.+$");

            RuleFor(c => c.BaseUrl).Matches(@"(?:/|t\.)rss\?.+;download(?:;|$)")
                .WithMessage("Use Direct Download Url (;download)")
                .When(v => v.BaseUrl.IsNotNullOrWhiteSpace() && Regex.IsMatch(v.BaseUrl, @"(?:/|t\.)rss\?.+$"));

            RuleFor(c => c.SeedCriteria).SetValidator(_ => new SeedCriteriaSettingsValidator());
        }
    }

    public class IPTorrentsSettings : ITorrentIndexerSettings
    {
        private static readonly IPTorrentsSettingsValidator Validator = new IPTorrentsSettingsValidator();

        public IPTorrentsSettings()
        {
            MinimumSeeders = IndexerDefaults.MINIMUM_SEEDERS;
            MultiLanguages = Array.Empty<int>();
        }

        [FieldDefinition(0, Label = "IndexerIPTorrentsSettingsFeedUrl", HelpText = "IndexerIPTorrentsSettingsFeedUrlHelpText")]
        public string BaseUrl { get; set; }

        [FieldDefinition(1, Type = FieldType.Number, Label = "IndexerSettingsMinimumSeeders", HelpText = "IndexerSettingsMinimumSeedersHelpText", Advanced = true)]
        public int MinimumSeeders { get; set; }

        [FieldDefinition(2)]
        public SeedCriteriaSettings SeedCriteria { get; set; } = new SeedCriteriaSettings();

        [FieldDefinition(3, Type = FieldType.Checkbox, Label = "IndexerSettingsRejectBlocklistedTorrentHashes", HelpText = "IndexerSettingsRejectBlocklistedTorrentHashesHelpText", Advanced = true)]
        public bool RejectBlocklistedTorrentHashesWhileGrabbing { get; set; }

        [FieldDefinition(4, Type = FieldType.Select, SelectOptions = typeof(RealLanguageFieldConverter), Label = "Multi Languages", HelpText = "What languages are normally in a multi release on this indexer?", Advanced = true)]
        public IEnumerable<int> MultiLanguages { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
