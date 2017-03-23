using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using RestSharp;
using StackExchange.Redis;
using System.Linq;
using AngleSharp;
using AngleSharp.Parser.Html;

namespace NewlyReadAPI.Controllers
{
    [Route("api/[controller]")]
    public class ArticleController : Controller
    {
        private static List<string> categories = new List<string>(){
            "business", "entertainment", "gaming", "general", "music", "science-and-nature", "sport", "technology"
        };

        [HttpGet]
        public dynamic Get()
        {
            return new string[] {
                "Please specify and endpoint."
           };
        }

        [HttpGet("{endpoint}/{category?}")]
        public dynamic Get(string endpoint, string category)
        {
            dynamic data = "";
            switch (endpoint)
            {
                case "extracted":
                    return getExtracted(category);
                default:
                    return "Invalid Request.";
            }
        }

        public static void setSources()
        {
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("127.0.0.1");
            IDatabase db = redis.GetDatabase();

            var rclient = new RestClient("http://newlyread.com/api/v1/");
            var request = new RestRequest();
            var response = new RestResponse();

            foreach (string category in categories)
            {
                try
                {
                    request = new RestRequest("articles?category=" + category);
                    Task.Run(async () =>
                    {
                        response = await GetResponseContentAsync(rclient, request) as RestResponse;
                        dynamic article_list = JsonConvert.DeserializeObject(response.Content);
                        foreach (dynamic article in article_list)
                        {
                            if (!db.SetContains("categories:" + category, article.ToString()))
                            {
                                db.SetAdd("categories:" + category, article.ToString());
                            }
                        }
                    }).Wait();
                }
                catch (Exception b)
                {
                    Console.WriteLine("\n Error setting sources: {0} \n", b);
                }
            }
            setExtractedAsync();

        }
        public static dynamic getExtracted(string category)
        {
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("127.0.0.1");
            IDatabase db = redis.GetDatabase();
            IServer server = redis.GetServer("127.0.0.1", 6379);

            return db.SetMembers("extracted:" + category).ToList();
        }
        public static async void setExtractedAsync()
        {
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("127.0.0.1");
            IDatabase db = redis.GetDatabase();
            IServer server = redis.GetServer("127.0.0.1", 6379);

            var rclient = new RestClient("http://newlyread.com/api/v1/");
            var request = new RestRequest();
            var response = new RestResponse();
            try
            {
                foreach (var key in server.Keys(pattern: "categories*"))
                {
                    string s = key.ToString() + ":*";
                    IEnumerable<RedisValue> articles = db.SetMembers(key);

                    request = new RestRequest();
                    foreach (dynamic article in articles)
                    {
                        string category = key.ToString().Split(':')[1];
                        // remember to try/catch this
                        dynamic data = JsonConvert.DeserializeObject(articles.First());
                        // Setup the configuration to support document loading
                        var config = Configuration.Default.WithDefaultLoader().WithCss();
                        // Load the names of all The Big Bang Theory episodes from Wikipedia
                        string address = data.url;
                        // Asynchronously get the document in a new context using the configuration
                        var document = await BrowsingContext.New(config).OpenAsync(address);
                        // Create a new parser front-end (can be re-used)
                        var parser = new HtmlParser();
                        //Just get the DOM representation
                        var parsed = parser.Parse(document.DocumentElement.OuterHtml);
                        var parsedBody = parsed.Body;
                        HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                        doc.LoadHtml(parsedBody.OuterHtml);
                        try
                        {
                            doc.DocumentNode.SelectNodes(".//style|.//script|//meta|//link|//head|//title|//noscript").ToList().ForEach(n => n.Remove());
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Document Parsing Error {0}", e);
                        }
                        if (!db.SetContains("extracted:" + category, doc.DocumentNode.InnerHtml))
                            db.SetAdd("extracted:" + category, doc.DocumentNode.InnerHtml);
                    }
                }
            }
            catch (Exception b)
            {
                Console.WriteLine("\n Error setting extracted: {0} \n", b);
            }
            Console.WriteLine("setExtracted() was called");
        }

        public static Task<IRestResponse> GetResponseContentAsync(RestClient rclient, RestRequest request)
        {
            var tcs = new TaskCompletionSource<IRestResponse>();
            var sw = new Stopwatch();
            var t = rclient.ExecuteAsync(request, response =>
            {
                dynamic content = JsonConvert.DeserializeObject(response.Content);
                tcs.SetResult(response);
            });
            return tcs.Task;
        }
    }
}