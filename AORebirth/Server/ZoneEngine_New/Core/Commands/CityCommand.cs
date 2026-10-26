namespace ZoneEngine_New.Core.Commands
{
    using System;

    using ZoneEngine_New.Core.Cities;
    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// <c>.city</c> (everyone): claim a free org plot, buy premade buildings, enter the city, or call a low-level alien raid on the controller.
    /// </summary>
    public sealed class CityCommand : IGmCommand
    {
        readonly OrgCityService _cities;

        public CityCommand(OrgCityService cities)
        {
            _cities = cities ?? throw new ArgumentNullException(nameof(cities));
        }

        public string Name => "city";

        public int RequiredGmLevel => 0;

        public string Usage => ".city | .city claim <plot> | .city buy <building> | .city enter [plot] | .city release | .city raid [plot] | .city buildings";

        public string Summary => "Org city plots at 0 cost and 0 upkeep, premade buildings, and a low-level controller raid.";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _cities.Execute(context.Player, context.Args);
        }
    }
}
