﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GW2DotNET;
using GW2PAO.API.Data;
using GW2PAO.API.Data.Enums;
using GW2PAO.API.Services.Interfaces;
using NLog;

namespace GW2PAO.API.Services
{
    /// <summary>
    /// Service class for event information
    /// </summary>
    public class EventsService : IEventsService
    {
        /// <summary>
        /// Default logger
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The GW2.NET API service objective
        /// </summary>
        private ServiceManager service = new ServiceManager();

        /// <summary>
        /// The World Events time table
        /// </summary>
        public MegaserverEventTimeTable EventTimeTable { get; private set; }

        /// <summary>
        /// Loads the events time table and initializes all cached event information
        /// </summary>
        public void LoadTable(bool isAdjustedTable)
        {
            logger.Info("Loading Event Time Table");
            try
            {
                this.EventTimeTable = MegaserverEventTimeTable.LoadTable(isAdjustedTable);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                logger.Info("Error loading Event Time Table, re-creating table");
                MegaserverEventTimeTable.CreateTable(isAdjustedTable);
                this.EventTimeTable = MegaserverEventTimeTable.LoadTable(isAdjustedTable);
            }

            try
            {
                logger.Info("Loading world event locations");
                foreach (var worldEvent in this.EventTimeTable.WorldEvents)
                {
                    // Get event details for the current event
                    var dynamicEvent = this.service.GetDynamicEventDetails(worldEvent.ID);

                    // Ensure that the service returned event data for the current event
                    if (dynamicEvent == null)
                    {
                        logger.Warn("Failed to load event data for event with ID '{0}'", worldEvent.ID);
                        continue;
                    }

                    // Get map details for the current event
                    // TODO: consider specifying a language (default: English)
                    dynamicEvent.Map = this.service.GetMap(dynamicEvent.MapId);

                    // Ensure that the service returned map data for the current event
                    if (dynamicEvent.Map == null)
                    {
                        logger.Warn("Failed to load map data for event with ID '{0}'", worldEvent.ID);
                        continue;
                    }

                    // Set the event location name
                    worldEvent.Location = dynamicEvent.Map.MapName;
                }
            }
            catch (Exception ex)
            {
                // If something goes wrong with the API, don't crash, but log the error
                logger.Error(ex);
            }
        }

        /// <summary>
        /// Retrieves the current state of the given event
        /// </summary>
        /// <param name="id">The ID of the event to retrieve the state of</param>
        /// <returns>The current state of the input event</returns>
        public Data.Enums.EventState GetState(Guid id)
        {
            if (this.EventTimeTable.WorldEvents.Any(evt => evt.ID == id))
            {
                WorldEvent worldEvent = this.EventTimeTable.WorldEvents.First(evt => evt.ID == id);
                return this.GetState(worldEvent);
            }
            else
            {
                return Data.Enums.EventState.Unknown;
            }
        }

        /// <summary>
        /// Retrieves the current state of the given event
        /// </summary>
        /// <param name="evt">The event to retrieve the state of</param>
        /// <returns>The current state of the input event</returns>
        public Data.Enums.EventState GetState(WorldEvent evt)
        {
            var state = Data.Enums.EventState.Unknown;

            var timeUntilActive = this.GetTimeUntilActive(evt);
            var timeSinceActive = this.GetTimeSinceActive(evt);

            if (timeSinceActive >= TimeSpan.FromTicks(0)
                && timeSinceActive < evt.Duration.Time)
            {
                state = Data.Enums.EventState.Active;
            }
            else if (timeUntilActive >= TimeSpan.FromSeconds(0)
                        && timeUntilActive < evt.WarmupDuration.Time)
            {
                state = Data.Enums.EventState.Warmup;
            }
            else
            {
                state = Data.Enums.EventState.Inactive;
            }

            return state;
        }

        /// <summary>
        /// Retrieves the amount of time until the next active time for the given event, using the megaserver timetables
        /// </summary>
        /// <param name="evt">The event to retrieve the time for</param>
        /// <returns>Timespan containing the amount of time until the event is next active</returns>
        public TimeSpan GetTimeUntilActive(WorldEvent evt)
        {
            TimeSpan timeUntilActive;

            // Find the next time
            var nextTime = evt.ActiveTimes.FirstOrDefault(activeTime => (activeTime.Time - DateTimeOffset.UtcNow.TimeOfDay) >= TimeSpan.FromSeconds(0));

            // If there is no next time, then take the first time
            if (nextTime == null)
            {
                nextTime = evt.ActiveTimes.First();
                timeUntilActive = (nextTime.Time + TimeSpan.FromHours(24) - DateTimeOffset.UtcNow.TimeOfDay);
            }
            else
            {
                // Calculate the number of seconds until the next time
                timeUntilActive = nextTime.Time - DateTimeOffset.UtcNow.TimeOfDay;
            }
            return timeUntilActive;
        }

        /// <summary>
        /// Retrieves the amount of time since the last active time for the given event, using the megaserver timetables
        /// </summary>
        /// <param name="evt">The event to retrieve the time for</param>
        /// <returns>Timespan containing the amount of time since the event was last active</returns>
        public TimeSpan GetTimeSinceActive(WorldEvent evt)
        {
            TimeSpan timeSinceActive;

            // Find the next time
            var lastTime = evt.ActiveTimes.LastOrDefault(activeTime => (DateTimeOffset.UtcNow.TimeOfDay - activeTime.Time) >= TimeSpan.FromSeconds(0));

            // If there is no next time, then take the first time
            if (lastTime == null)
            {
                lastTime = evt.ActiveTimes.First();
                timeSinceActive = (lastTime.Time + TimeSpan.FromHours(24) - DateTimeOffset.UtcNow.TimeOfDay);
            }
            else
            {
                // Calculate the number of seconds until the next time
                timeSinceActive = DateTimeOffset.UtcNow.TimeOfDay - lastTime.Time;
            }
            return timeSinceActive;
        }
    }
}