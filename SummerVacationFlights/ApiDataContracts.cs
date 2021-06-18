using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SummerVacationFlights
{
    public class ApiDataContracts
    {
        public class PlacesList
        {
            public List<Place> Places { get; set; }
        }

        public class Place
        {
            public string PlaceId { get; set; }
            public string PlaceName { get; set; }
            public string CountryId { get; set; }
            public string RegionId { get; set; }
            public string CityId { get; set; }
            public string CountryName { get; set; }
        }

        public class FlightToVacationPlace
        {
            public string DestinationCountry { get; set; }
            public DateTime DepartureDateOutbound { get; set; }
            public DateTime DepartureDateInbound { get; set; }
            public decimal Price { get; set; }
            public int ActiveCovidCases { get; set; }
        }

    }
}
