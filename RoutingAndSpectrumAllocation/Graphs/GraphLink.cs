﻿using Newtonsoft.Json;
using System;

namespace RoutingAndSpectrumAllocation.Graphs
{
    [Serializable]
    public class GraphLink
    {
        [JsonProperty("From")]
        public string From { get; set; }

        [JsonProperty("To")]
        public string To { get; set; }

        [JsonProperty("Length")]
        public double Length { get; set; }

        [JsonProperty("Capacity")]
        public long Capacity { get; set; }

        [JsonProperty("Cost")]
        public long Cost { get; set; }

        [JsonProperty("Designation")]
        public string Designation { get; set; }

        [JsonProperty("Delay")]
        public string Delay { get; set; }

        public string GetLinkId()
        {
            return From + "<->" + To;
        }
    }
}