﻿using System;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.HDBits
{
    public class HDBitsSettingsValidator : AbstractValidator<HDBitsSettings>
    {
        public HDBitsSettingsValidator()
        {
            RuleFor(c => c.BaseUrl).ValidRootUrl();
            RuleFor(c => c.ApiKey).NotEmpty();
        }
    }

    public class HDBitsSettings : IProviderConfig
    {
        private static readonly HDBitsSettingsValidator Validator = new HDBitsSettingsValidator();

        public HDBitsSettings()
        {
            BaseUrl = "https://hdbits.org";
        }

        [FieldDefinition(0, Label = "Username")]
        public string Username { get; set; }

        [FieldDefinition(1, Label = "API Key")]
        public string ApiKey { get; set; }

        [FieldDefinition(2, Label = "API URL", Advanced = true, HelpText = "Do not change this unless you know what you're doing. Since your API key will be sent to that host.")]
        public string BaseUrl { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }

    public enum HdBitsCategory
    {
        Movie = 1,
        Tv = 2,
        Documentary = 3,
        Music = 4,
        Sport = 5,
        Audio = 6,
        Xxx = 7,
        MiscDemo = 8
    }

    public enum HdBitsCodec
    {
        H264 = 1,
        Mpeg2 = 2,
        Vc1 = 3,
        Xvid = 4
    }

    public enum HdBitsMedium
    {
        Bluray = 1,
        Encode = 3,
        Capture = 4,
        Remux = 5,
        WebDl = 6
    }
}
