using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace SummerVacationFlights
{
    class Program
    {
        const string myKey = "here-put-your-personal-RapidAPI-key";
        const string skyScannerApi = "skyscanner-skyscanner-flight-search-v1.p.rapidapi.com";
        const string covidApi = "covid-193.p.rapidapi.com";

        static DateTime startSeasonDate = new DateTime(2021, 6, 1);
        static DateTime endSeasonDate = new DateTime(2021, 8, 31);

        const string getPlacesUrl = "https://skyscanner-skyscanner-flight-search-v1.p.rapidapi.com/apiservices/autosuggest/v1.0/IL/USD/en-US/?query={0}";
        const string getCovidStatisticsUrl = "https://covid-193.p.rapidapi.com/statistics?country={0}";
        const string getFlightQuotesUrl = "https://skyscanner-skyscanner-flight-search-v1.p.rapidapi.com/apiservices/browsequotes/v1.0/IL/USD/en-US/TLV-sky/{0}/anytime/anytime";

        static List<string> countriesToVisit = new List<string>()
            {
                "France",
                "USA",
                "UK",
                "Australia"
            };


        static async Task<string> ApiGetQuery(string apiUrl, string apiRequestUrl, string country)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,

                RequestUri = new Uri(string.Format(apiRequestUrl, country)),
                Headers =
                {
                    { "x-rapidapi-key", myKey },
                    { "x-rapidapi-host",  apiUrl},
                },
            };

            using (var response = await client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();
                return body;
            }
        }

        private static T DeserializeJSon<T>(string jsonString)
        {
            var ser = new DataContractJsonSerializer(typeof(T));
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
            var obj = (T)ser.ReadObject(stream);
            return obj;
        }

        static async Task RunQuick(List<ApiDataContracts.FlightToVacationPlace> flightResults)
        {
            List<Task<string>> getCountryTasks = new List<Task<string>>(countriesToVisit.Count);
            List<Task<string>> getCovidTasks = new List<Task<string>>(countriesToVisit.Count);
            List<Task<string>> getFlightsTasks = new List<Task<string>>(countriesToVisit.Count);

            List<string> getCountryResponses = new List<string>(countriesToVisit.Count);
            List<string> getCovidResponses = new List<string>(countriesToVisit.Count);
            List<string> getFlightsResponses = new List<string>(countriesToVisit.Count);

            List<ApiDataContracts.PlacesList> getPlaces = new List<ApiDataContracts.PlacesList>(countriesToVisit.Count);

            for (int i = 0; i < countriesToVisit.Count; i++)
            {
                getCountryTasks.Add(ApiGetQuery(skyScannerApi, getPlacesUrl, countriesToVisit[i]));
                getCovidTasks.Add(ApiGetQuery(covidApi, getCovidStatisticsUrl, countriesToVisit[i]));
            }

            for (int i = 0; i < countriesToVisit.Count; i++)
            {
                getCountryResponses.Add(await getCountryTasks[i]);
                getCovidResponses.Add(await getCovidTasks[i]);
                getPlaces.Add(DeserializeJSon<ApiDataContracts.PlacesList>(getCountryResponses[i]));
                getFlightsTasks.Add(ApiGetQuery(skyScannerApi, getFlightQuotesUrl, getPlaces[i].Places[0].PlaceId));
            }

            for (int i = 0; i < countriesToVisit.Count; i++)
            {
                dynamic covidCases = JsonConvert.DeserializeObject(getCovidResponses[i]);

                getFlightsResponses.Add(await getFlightsTasks[i]);

                dynamic flights = JsonConvert.DeserializeObject(getFlightsResponses[i]);
                try
                {
                    dynamic dataItem = ((Newtonsoft.Json.Linq.JContainer)((Newtonsoft.Json.Linq.JContainer)((Newtonsoft.Json.Linq.JContainer)flights).First).First);
                    var count = dataItem.Count;
                    dataItem = ((Newtonsoft.Json.Linq.JToken)dataItem).First;
                    DateTime testDate = new DateTime();

                    while (count > 0)
                    {
                        testDate = (DateTime)dataItem.OutboundLeg.DepartureDate;

                        if (testDate >= startSeasonDate && testDate <= endSeasonDate)
                        {
                            flightResults.Add(new ApiDataContracts.FlightToVacationPlace
                            {
                                DestinationCountry = countriesToVisit[i],
                                Price = (decimal)dataItem.MinPrice,
                                DepartureDateOutbound = (DateTime)dataItem.OutboundLeg.DepartureDate,
                                DepartureDateInbound = (DateTime)dataItem.InboundLeg.DepartureDate,
                                ActiveCovidCases = (int)covidCases.response[0].cases.active

                            });
                        }
                        dataItem = ((Newtonsoft.Json.Linq.JToken)dataItem).Next;
                        count--;
                    }

                }
                catch (Exception ex)
                {
                    throw new Exception($"Error in flight quotes data structure: {ex}");
                }
            }
        }

        static async Task RunSlow(List<ApiDataContracts.FlightToVacationPlace> flightResults)
        {
            string responseBody = string.Empty;

            foreach (var country in countriesToVisit)
            {
                // get "PlaceId" value for our countries

                responseBody = await ApiGetQuery(skyScannerApi, getPlacesUrl, country);

                var places = DeserializeJSon<ApiDataContracts.PlacesList>(responseBody);

                // get last known COVID active cases in every country

                responseBody = await ApiGetQuery(covidApi, getCovidStatisticsUrl, country);

                dynamic covidCases = JsonConvert.DeserializeObject(responseBody);

                // get 2021 summer flights data to countries' main destinations

                responseBody = await ApiGetQuery(skyScannerApi, getFlightQuotesUrl, places.Places[0].PlaceId);

                dynamic flights = JsonConvert.DeserializeObject(responseBody);
                try
                {
                    dynamic dataItem = ((Newtonsoft.Json.Linq.JContainer)((Newtonsoft.Json.Linq.JContainer)((Newtonsoft.Json.Linq.JContainer)flights).First).First);
                    var count = dataItem.Count;
                    dataItem = ((Newtonsoft.Json.Linq.JToken)dataItem).First;
                    DateTime testDate = new DateTime();

                    while (count > 0)
                    {
                        testDate = (DateTime)dataItem.OutboundLeg.DepartureDate;

                        if (testDate >= startSeasonDate && testDate <= endSeasonDate)
                        {
                            flightResults.Add(new ApiDataContracts.FlightToVacationPlace
                            {
                                DestinationCountry = country,
                                Price = (decimal)dataItem.MinPrice,
                                DepartureDateOutbound = (DateTime)dataItem.OutboundLeg.DepartureDate,
                                DepartureDateInbound = (DateTime)dataItem.InboundLeg.DepartureDate,
                                ActiveCovidCases = (int)covidCases.response[0].cases.active

                            });
                        }
                        dataItem = ((Newtonsoft.Json.Linq.JToken)dataItem).Next;
                        count--;
                    }

                }
                catch (Exception ex)
                {
                    throw new Exception($"Error in flight quotes data structure: {ex}");
                }
            }
        }

        static async Task Main(string[] args)
        {
            Console.WriteLine("Press '1' to process flights quickly");
            Console.WriteLine("Press '2' to process flights slowly");
            Console.WriteLine("Press any other key to exit...");

            ConsoleKeyInfo chr;

            while (true)
            {
                chr = Console.ReadKey(true);
                if (chr.Key != ConsoleKey.D1 && chr.Key != ConsoleKey.D2)
                    return;

                Console.WriteLine();
                Console.WriteLine("Waiting Web API results..");

                List<ApiDataContracts.FlightToVacationPlace> flightResults = new List<ApiDataContracts.FlightToVacationPlace>();

                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                if (chr.Key == ConsoleKey.D1)
                    await RunQuick(flightResults);
                else
                    await RunSlow(flightResults);

                stopwatch.Stop();

                TimeSpan ts = stopwatch.Elapsed;
                Console.WriteLine("Elapsed Time is {0:00}:{1:00}:{2:00}.{3}",
                                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);

                //sorting data by COVID cases and prices
                flightResults.Sort((d1, d2) =>
                {
                    int res = d1.ActiveCovidCases.CompareTo(d2.ActiveCovidCases);
                    if (res == 0)
                        res = d1.Price.CompareTo(d2.Price);
                    return res;
                });

                PrintResult(flightResults);

                Console.WriteLine();
                Console.WriteLine("Press '1' to process flights quickly");
                Console.WriteLine("Press '2' to process flights slowly");
                Console.WriteLine("Press any other key to exit...");
            }
        }

        static private void PrintResult(List<ApiDataContracts.FlightToVacationPlace> flightResults)
        {
            Console.WriteLine();
            Console.WriteLine("==================== Your next summer destinations ===========================");
            Console.WriteLine();
            Console.WriteLine("{0, -10} | {1,10:C} | {2,15:d} | {3,15:d} | {4,12}",
                                "Country",
                                "Price",
                                "Departure Date",
                                "Arrival Date",
                                "COVID cases");

            Console.WriteLine("{0, -10} | {1,10:C} | {2,15:d} | {3,15:d} | {4,12}",
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty);

            string curCountry = string.Empty;

            foreach (var flight in flightResults)
            {
                if (curCountry.CompareTo(flight.DestinationCountry) != 0)
                {
                    Console.WriteLine("{0, -10} | {1,10:C} | {2,15:d} | {3,15:d} | {4,12}",
                        flight.DestinationCountry,
                        flight.Price,
                        flight.DepartureDateOutbound,
                        flight.DepartureDateInbound,
                        flight.ActiveCovidCases);
                    curCountry = flight.DestinationCountry;
                }
                else
                {
                    Console.WriteLine("{0, -10} | {1,10:C} | {2,15:d} | {3,15:d} | {4,12}",
                        string.Empty,
                        flight.Price,
                        flight.DepartureDateOutbound,
                        flight.DepartureDateInbound,
                        string.Empty
                        );
                }
            }

            Console.WriteLine();
            Console.WriteLine("Records count: {0}", flightResults.Count);
        }
    }
}
