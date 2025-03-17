// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Users;

namespace PerformanceCalculatorGUI
{
    public class RXPlayer
    {
        public required int Id { get; set; }

        [JsonProperty(@"countryCode")]
        private string countryCodeString;

        public CountryCode CountryCode
        {
            get => Enum.TryParse(countryCodeString, out CountryCode result) ? result : CountryCode.Unknown;
            set => countryCodeString = value.ToString();
        }

        public required string Username { get; set; } = null!;

        public double? TotalPp { get; set; }
        public double? TotalAccuracy { get; set; }

        public APIUser ToAPIUser()
        {
            return new APIUser()
            {
                Id = Id,
                Username = Username,
                CountryCode = CountryCode,
                Statistics = new UserStatistics()
                {
                    PP = (decimal?)TotalPp,
                    Accuracy = TotalAccuracy ?? 0,
                }
            };
        }
    }
}
