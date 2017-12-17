using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace WorkerRoleWithSBQueue1
{
    public class WorkerRole : RoleEntryPoint
    {
        // The name of your queue
        const string QueueName = "ProcessingQueue";

        // QueueClient is thread-safe. Recommended that you cache 
        // rather than recreating it on every request
        QueueClient Client;
        ManualResetEvent CompletedEvent = new ManualResetEvent(false);

        public override void Run()
        {
            Trace.WriteLine("Starting processing of messages");

            // Initiates the message pump and callback is invoked for each message that is received, calling close on the client will stop the pump.
            Client.OnMessage((receivedMessage) =>
                {
                    try
                    {
                        // Process the message
                        string wcu = (string) receivedMessage.Properties["WorkerCallbackUrl"];
                        HttpWebRequest httpPayload = (HttpWebRequest)WebRequest.Create(wcu);
                        httpPayload.ContentType = "application/json";
                        httpPayload.Method = HttpMethod.Post.Method;
                        byte[] body = Encoding.UTF8.GetBytes(ResolveIPResponse());
                        using (Stream stream = httpPayload.GetRequestStream())
                        {
                            stream.Write(body, 0, body.Length);
                        }
                        httpPayload.GetResponse();
                        receivedMessage.Complete();
                    }
                    catch
                    {
                        // Handle any message processing specific exceptions here
                        receivedMessage.Abandon();
                    }
                });

            CompletedEvent.WaitOne();
        }

        private string ResolveIPResponse()
        {
            try
            {
                HttpWebRequest resolveip = (HttpWebRequest)WebRequest.Create(CloudConfigurationManager.GetSetting("Microsoft.FunctionApp.ResolveIP"));
                resolveip.ContentType = "application/json";
                resolveip.Method = HttpMethod.Get.Method;
                HttpWebResponse resolveIPResponse = (HttpWebResponse) resolveip.GetResponse();
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
                            vipr.Role = "WorkerRole";
                            vipr.IP = responseString;
                            return JsonConvert.SerializeObject(vipr);
                        }
                    }
                }
                return resolveIPResponse.StatusDescription;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // Create the queue if it does not exist already
            string connectionString = CloudConfigurationManager.GetSetting("Microsoft.ServiceBus.ConnectionString");
            var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
            if (!namespaceManager.QueueExists(QueueName))
            {
                namespaceManager.CreateQueue(QueueName);
            }

            // Initialize the connection to Service Bus Queue
            Client = QueueClient.CreateFromConnectionString(connectionString, QueueName);
            return base.OnStart();
        }

        public override void OnStop()
        {
            // Close the connection to Service Bus Queue
            Client.Close();
            CompletedEvent.Set();
            base.OnStop();
        }
    }

    public class VIPResponse
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("ip")]
        public string IP { get; set; }
    }
}
