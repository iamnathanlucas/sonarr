﻿using System.Text.RegularExpressions;
using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Annotations;
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
        }

        [FieldDefinition(0, Label = "Feed URL", HelpText = "The full RSS feed url generated by IPTorrents, using only the categories you selected (HD, SD, x264, etc ...)")]
        public string BaseUrl { get; set; }

        [FieldDefinition(1, Type = FieldType.Number, Label = "Minimum Seeders", HelpText = "Minimum number of seeders required.", Advanced = true)]
        public int MinimumSeeders { get; set; }

        [FieldDefinition(2)]
        public SeedCriteriaSettings SeedCriteria { get; } = new SeedCriteriaSettings();

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
