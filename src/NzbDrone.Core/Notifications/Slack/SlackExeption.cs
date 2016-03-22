﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.Notifications.Slack
{
    class SlackExeption : NzbDroneException
    {
        public SlackExeption(string message) : base(message)
        {
        }

        public SlackExeption(string message, Exception innerException, params object[] args) : base(message, innerException, args)
        {
        }
    }
}
