﻿namespace NzbDrone.Common.Messaging
{
    public class CommandStartedEvent : IEvent
    {
        public ICommand Command { get; private set; }

        public CommandStartedEvent(ICommand command)
        {
            Command = command;
        }
    }
}