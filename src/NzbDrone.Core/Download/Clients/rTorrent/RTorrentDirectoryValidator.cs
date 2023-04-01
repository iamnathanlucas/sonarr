﻿using FluentValidation;
using FluentValidation.Results;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download.Clients.RTorrent;
using NzbDrone.Core.Validation.Paths;

namespace NzbDrone.Core.Download.Clients.rTorrent
{
    public interface IRTorrentDirectoryValidator
    {
        ValidationResult Validate(RTorrentSettings instance);
    }

    public class RTorrentDirectoryValidator : AbstractValidator<RTorrentSettings>, IRTorrentDirectoryValidator
    {
        public RTorrentDirectoryValidator(RootFolderValidator rootFolderValidator,
                                          PathExistsValidator pathExistsValidator,
                                          MappedNetworkDriveValidator mappedNetworkDriveValidator)
        {
#pragma warning disable CS0618
            RuleFor(c => c.TvDirectory).Cascade(CascadeMode.StopOnFirstFailure)
#pragma warning restore CS0618
                .IsValidPath()
                                       .SetValidator(rootFolderValidator)
                                       .SetValidator(mappedNetworkDriveValidator)
                                       .SetValidator(pathExistsValidator)
                                       .When(c => c.TvDirectory.IsNotNullOrWhiteSpace())
                                       .When(c => c.Host == "localhost" || c.Host == "127.0.0.1");
        }
    }
}
