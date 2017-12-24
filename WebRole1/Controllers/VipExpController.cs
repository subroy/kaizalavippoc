using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure;
using Newtonsoft.Json;
using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;

namespace WebRole1.Controller
{
    public class VipExpController : ApiController
    {
        [HttpPost]
        [Route("verifyip")]
        public HttpResponseMessage VerifyIP([FromBody]VerifyIPRequest request)
        {
            // The name of your queue
            const string QueueName = "ProcessingQueue";
            if (request != null && !string.IsNullOrWhiteSpace(request.WorkerCallbackUrl))
            {
                // Create the queue if it does not exist already
                string connectionString = ConfigurationManager.AppSettings["Microsoft.ServiceBus.ConnectionString"];
                var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
                if (!namespaceManager.QueueExists(QueueName))
                {
                    namespaceManager.CreateQueue(QueueName);
                }

                // Initialize the connection to Service Bus Queue
                QueueClient Client = QueueClient.CreateFromConnectionString(connectionString, QueueName);
                BrokeredMessage bm = new BrokeredMessage();
                bm.Properties["WorkerCallbackUrl"] = request.WorkerCallbackUrl;
                Client.Send(bm);
            }
            return GetWebRoleResponse();
        }

        [HttpGet]
        [Route("verifyip")]
        public HttpResponseMessage VerifyIP()
        {
            return GetWebRoleResponse();
        }

        private HttpResponseMessage GetWebRoleResponse()
        {
            HttpResponseMessage response = new HttpResponseMessage();
            try
            {
                HttpWebRequest resolveip = (HttpWebRequest)WebRequest.Create(ConfigurationManager.AppSettings["Microsoft.FunctionApp.ResolveIP"]);
                resolveip.ContentType = "application/json";
                resolveip.Method = HttpMethod.Get.Method;
                HttpWebResponse resolveIPResponse = (HttpWebResponse)resolveip.GetResponse();
                if (resolveIPResponse.StatusCode == HttpStatusCode.OK)
                {
                    string responseString = null;
                    using (Stream responseStream = resolveIPResponse.GetResponseStream())
                    {
                        StreamReader reader = new StreamReader(responseStream);
                        responseString = reader.ReadToEnd();
                        if (!string.IsNullOrWhiteSpace(responseString))
                        {
                            VIPResponse vipr = new VIPResponse();
                            vipr.Role = "WebRole";
                            vipr.IP = responseString;
                            response.Content = new StringContent(JsonConvert.SerializeObject(vipr), Encoding.UTF8, "application/json");
                            response.StatusCode = HttpStatusCode.OK;
                        }
                    }
                }
                response.StatusCode = resolveIPResponse.StatusCode;
            }
            catch (Exception e)
            {
                response.Content = new StringContent(e.Message, Encoding.UTF8, "application/json");
                response.StatusCode = HttpStatusCode.InternalServerError;
            }
            return response;
        }
    }

    public class VerifyIPRequest
    {
        [JsonProperty("workercallbackurl")]
        public string WorkerCallbackUrl { get; set; }
    }

    class VIPResponse
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("ip")]
        public string IP { get; set; }
    }
}
