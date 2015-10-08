﻿using System.Collections.Generic;
using FluentValidation.Results;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Update;
using System;

namespace NzbDrone.Core.Notifications.Plex
{
    public class PlexClient : NotificationBase<PlexClientSettings>
    {
        private readonly IPlexClientService _plexClientService;

        public PlexClient(IPlexClientService plexClientService)
        {
            _plexClientService = plexClientService;
        }

        public override string Link
        {
            get { return "http://www.plexapp.com/"; }
        }

        public override void OnGrab(GrabMessage grabMessage)
        {
            const string header = "Sonarr [TV] - Grabbed";
            _plexClientService.Notify(Settings, header, grabMessage.Message);
        }

        public override void OnDownload(DownloadMessage message)
        {
            const string header = "Sonarr [TV] - Downloaded";
            _plexClientService.Notify(Settings, header, message.Message);
        }

        public override void OnRename(Series series)
        {
        }

        public override void OnUpdateAvailable(UpdatePackage package)
        {
            const string header = "Sonarr [TV] - New System Update";
            var body = String.Format("New update is available - {0}", package.Version.ToString());

            _plexClientService.Notify(Settings, header, body);
        }

        public override string Name
        {
            get
            {
                return "Plex Media Center";
            }
        }

        public override bool SupportsOnRename
        {
            get
            {
                return false;
            }
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            failures.AddIfNotNull(_plexClientService.Test(Settings));

            return new ValidationResult(failures);
        }
    }
}
