using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Net;
using System.IO;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;
using System.Configuration; //BL

namespace ARMAPI_Test
{

    class Program
    {
        //This is a sample console application that shows you how to grab a User token from AAD for the current user of the app
        //The same caveat remains, that the current user of the app needs to be part of either the Owner, Reader or Contributor role for the requested AzureSubID.
        static void Main(string[] args)
        {
            //Get the AAD User token to get authorized to make the call to the Usage API
            string token = GetOAuthTokenFromAAD();

            /*Setup API call to RateCard API
             Callouts:
             * See the App.config file for all AppSettings key/value pairs
             * You can get a list of offer numbers from this URL: http://azure.microsoft.com/en-us/support/legal/offer-details/
             * You can configure an OfferID for this API by updating 'MS-AZR-{Offer Number}'
             * The RateCard Service/API is currently in preview; please use "2015-06-01-preview" or "2016-08-31-preview" for api-version (see https://msdn.microsoft.com/en-us/library/azure/mt219005 for details)
             * Please see the readme if you are having problems configuring or authenticating: https://github.com/Azure-Samples/billing-dotnet-ratecard-api
             */

            // Build up the HttpWebRequest
            string offerId = string.Format("MS-AZR-{0}", "0003P"); //MS-AZR-0121p
            string currency = "USD";
            string locale = "en-US";
            string regionInfo = "US";
            string filterQuery = string.Format("&$filter=OfferDurableId eq '{0}' and Currency eq '{1}' and Locale eq '{2}' and RegionInfo eq '{3}'", 
                                                offerId,
                                                currency,
                                                locale,
                                                regionInfo);

            string requestURL = String.Format("{0}/{1}/{2}/{3}{4}",
                       ConfigurationManager.AppSettings["ARMBillingServiceURL"],
                       "subscriptions",
                       ConfigurationManager.AppSettings["SubscriptionID"],
                       "providers/Microsoft.Commerce/RateCard?api-version=2016-08-31-preview",
                       filterQuery);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestURL);

            // Add the OAuth Authorization header, and Content Type header
            request.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + token);
            request.ContentType = "application/json";

            // Call the RateCard API, dump the output to the console window
            try
            {
                // Call the REST endpoint
                Console.WriteLine("Calling RateCard service...");
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Console.WriteLine(String.Format("RateCard service response status: {0}", response.StatusDescription));
                Stream receiveStream = response.GetResponseStream();

                // Pipes the stream to a higher level stream reader with the required encoding format. 
                StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8);
                var rateCardResponse = readStream.ReadToEnd();

                //Write to the file system
                var path = string.Format("{0}\\{1}-{2}-{3}.json", Environment.CurrentDirectory, offerId, currency, DateTime.Now.ToString("yyyyMMdd"));
                System.IO.File.WriteAllText(path, rateCardResponse);

                // Convert the Stream to a strongly typed RateCardPayload object.  
                // You can also walk through this object to manipulate the individuals member objects. 
                //RateCardPayload payload = JsonConvert.DeserializeObject<RateCardPayload>(rateCardResponse);
                //Console.WriteLine(rateCardResponse.ToString());
                //response.Close();
                //readStream.Close();
                //Console.WriteLine("JSON output complete.  Press ENTER to close.");
                //Console.ReadLine();
            }
            catch(Exception e)
            {
                Console.WriteLine(String.Format("{0} \n\n{1}", e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                Console.ReadLine();
            }
        }


        public static string GetOAuthTokenFromAAD()
        {
            var authenticationContext = new AuthenticationContext(  String.Format("{0}/{1}",
                                                                    ConfigurationManager.AppSettings["ADALServiceURL"],
                                                                    ConfigurationManager.AppSettings["TenantDomain"]));

            //Ask the logged in user to authenticate, so that this client app can get a token on his behalf
            var result = authenticationContext.AcquireToken(String.Format("{0}/",ConfigurationManager.AppSettings["ARMBillingServiceURL"]),
                                                            ConfigurationManager.AppSettings["ClientID"],
                                                            new Uri(ConfigurationManager.AppSettings["ADALRedirectURL"]),
                                                            PromptBehavior.Always);

            if (result == null)
            {
                throw new InvalidOperationException("Failed to obtain the JWT token");
            }

            return result.AccessToken;
        }
    }
}

