using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Linq;
using Discord.Webhook;
using System.Collections.Generic;

namespace kickzmonitor
{
    class Program
    {
        static async Task Main()
        {
            Console.WriteLine("KICKZ MONITOR");
            Console.WriteLine("--------------");

            Console.WriteLine("Insert delay to start (ms)");
            Globals.delay = Convert.ToInt32(Console.ReadLine());


            var path = Path.Combine(Directory.GetCurrentDirectory(), "proxies.txt");
            string[] proxys = System.IO.File.ReadAllLines(path);
            Console.WriteLine(path);

            path = Path.Combine(Directory.GetCurrentDirectory(), "products.txt");
            string[] productsUrls = System.IO.File.ReadAllLines(path);

            path = Path.Combine(Directory.GetCurrentDirectory(), "webhook.txt");
            string[] webhooks = System.IO.File.ReadAllLines(path);

            Globals.webhook = webhooks[0];


            Random r = new Random();
            Proxy proxytask = new Proxy("");
            List<Task> TaskList = new List<Task>(productsUrls.Length);

            for (int i = 0; i < productsUrls.Length; ++i)
            {

                proxytask = new Proxy(proxys[r.Next(proxys.Length)]);

                Console.WriteLine("Creating task {0}", i);
                var newTask = MonitorLink(productsUrls[i], proxytask);
                TaskList.Add(newTask);
            }

            await Task.WhenAll(TaskList);

        }


        static class Globals
        {
            public static int delay;
            public static string webhook;
        }

        static async Task MonitorLink(string url, Proxy proxy)
        {

            Console.WriteLine("Starting task...");
            bool pause = false;
            bool first = true;
            string name = "";
            string image = "";

            string sourcecode = "";

            CookieContainer c = new CookieContainer();
      /*      var cookies = c.GetCookies(new Uri(url));
            foreach (Cookie co in cookies)
            {
                co.Expires = DateTime.Now.Subtract(TimeSpan.FromDays(1));
            }
      */

            HttpClientHandler req = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip | DecompressionMethods.Brotli,
                CookieContainer = c,
                UseCookies = true
            };

            WebProxy myProxy = new WebProxy(proxy.ip, proxy.port);
            string username = proxy.username;
            string password = proxy.password;
            myProxy.Credentials = new NetworkCredential(username, password);
            req.Proxy = myProxy;
            HttpClient client = new HttpClient(req);

            client.Timeout = TimeSpan.FromSeconds(30);

      
            client.DefaultRequestHeaders.Add("authority", "www.kickz.com");
            
            client.DefaultRequestHeaders.Add("scheme", "https");
            client.DefaultRequestHeaders.Add("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
            client.DefaultRequestHeaders.Add("accept-encoding", "gzip, deflate, br");
            client.DefaultRequestHeaders.Add("accept-language", "ca-ES,ca;q=0.9");
            client.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
            client.DefaultRequestHeaders.Add("Pragma", "max-age=0");

            client.DefaultRequestHeaders.Add("sec-fetch-dest", "document");
            client.DefaultRequestHeaders.Add("Connection", "keep-alive");

            client.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
            client.DefaultRequestHeaders.Add("sec-fetch-site", "none");
            client.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
            client.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");

          
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_14_6) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.132 Safari/537.36");


            string[] sizes = new string[15];
            string[] oldsizes = new string[15];


            Console.WriteLine("MONITORING...");


            Random r = new Random();


            await Get(url, client);  //get cookies to ensure success on next requests
           
            while (!pause)
            {

                sizes.CopyTo(oldsizes, 0);

                for (int i = 0; i < sizes.Length; ++i)
                {
                    sizes[i] = "";
                }

                sourcecode = await POST(url, client);   //post because get request uses cache and won't let update source
                
                
                var doc = new HtmlDocument();
                //Console.WriteLine(sourcecode);

                if (sourcecode.Contains("Access Denied") || sourcecode.Contains("Invalid Url"))
                {
                    Console.WriteLine("ACCESS DENIED");
                }
                else
                {

                    doc.LoadHtml(sourcecode);

                    if (first)    //searching the image and the product name of the shoe
                    {
                        name = new Regex("prodNameId\">(.+?)<").Match(sourcecode).Groups[1].Value;
                        image = new Regex("class=\"productDetailZoom\" src=\"(.+?)\"").Match(sourcecode).Groups[1].Value;

                    }

                    Console.WriteLine("{0}searching sizes...", name);

                    if (!sourcecode.Contains("chooseSizeLinkContainer active")) //if doesn't contain these its sold out
                    {
                        Console.WriteLine("OOS");
                    }
                    else
                    {
                        var eucontainer = doc.DocumentNode.SelectNodes("//*[@id='1SizeContainer']").First();
                        // Console.WriteLine(eucontainer.InnerHtml);
                        Regex rgx = new Regex("data-size=\"(.*?)\"");
                        //Console.WriteLine(eucontainer.InnerHtml);
                        int j = 0;
                        foreach (Match match in rgx.Matches(eucontainer.InnerHtml))
                        {
                            Console.WriteLine("Found '{0}' at position {1}", match.Value, match.Index);
                            sizes[j] = match.Groups[1].Value;
                            ++j;

                        }
                    }

                    for (int i = 0; i < 15; ++i)
                    {
                        if (sizes[i] != "")
                        {
                            Console.WriteLine("Talla'{0}' at position {1}",
                                                 sizes[i], i);
                        }
                    }


                    if (!first)
                    {
                        Console.WriteLine("Comparing...");
                        bool equal = Enumerable.SequenceEqual(sizes, oldsizes);
                        Console.WriteLine(equal);
                        if (!equal)
                        {
                            Console.WriteLine("RESTOCK!!");
                            await Discord(name, image, url, sizes);

                        }
                    }


                    //the commented code right here is for debuggin purposes
                    /*
                    Console.WriteLine("--------old--------");
                    for (int i = 0; i < oldsizes.Length; ++i)
                    {
                        Console.WriteLine(oldsizes[i]);
                    }
                    Console.WriteLine("-------new-------");
                    for (int i = 0; i < sizes.Length; ++i)
                    {
                        Console.WriteLine(sizes[i]);
                    }
                    */


                    // await Discord(name, image, url , sizes); //just for testing the webhook , remove when done


                    System.Threading.Thread.Sleep(Globals.delay);
                    first = false;
                }
            }

        }





            public struct Proxy
            {
                public bool proxyless;
                public string ip;
                public int port;
                public string username;
                public string password;

                public Proxy(string proxy)
                {

                    if (proxy == "")
                    {
                        ip = "";
                        port = 0;
                        username = "";
                        password = "";
                        proxyless = true;

                    }
                    else
                    {
                        string[] proxyparams = proxy.Split(":");
                        ip = proxyparams[0];
                        port = Int32.Parse(proxyparams[1]);
                        username = proxyparams[2];
                        password = proxyparams[3];
                        proxyless = false;
                        /*
                        Console.WriteLine(ip);
                        Console.WriteLine(port);
                        Console.WriteLine(username);
                        Console.WriteLine(password);
                        */
                    }



                }
            }





            public static async Task Discord(string productname, string imageurl, string url, string[] sizes)
            {
                string msg = "";
                using (var client = new DiscordWebhookClient(Globals.webhook))
                {
                    for (int i = 0; i < sizes.Length; ++i)
                    {
                        if (sizes[i] != "")
                        {
                            msg = msg + "EU " + sizes[i] + "\r\n";
                        }

                    }
                    if (msg =="")  msg = "-" + "\r\n";

                    var embed = new Discord.EmbedBuilder
                    {
                        //Description = msg,
                        ThumbnailUrl = imageurl

                    };
                    embed.AddField("Sizes", msg).WithTitle(productname).WithUrl(url);

                    // Webhooks are able to send multiple embeds per message
                    // As such, your embeds must be passed as a collection. 
                    await client.SendMessageAsync(text: "", embeds: new[] { embed.Build() });
                }
            }




            public static async Task<string> Get(string url, HttpClient client)
            {

                try
                {

                    using (var reqmes = new HttpRequestMessage(HttpMethod.Get, url))
                    {

                        HttpResponseMessage resp = await client.SendAsync(reqmes);
                           
                            using (var streamReader = new StreamReader(await resp.Content.ReadAsStreamAsync()))
                            {
                                return await streamReader.ReadToEndAsync();
                            }

                        }

                }

                catch (Exception e)
                {
                    Console.WriteLine("{0} Exception caught.", e);
                    return "";
                }

            }


            public static async Task<string> POST(String _target, HttpClient client)
            {
                try
                {

                    using (var request = new HttpRequestMessage(HttpMethod.Post, _target))
                    {
                            HttpResponseMessage response = await client.SendAsync(request);

                                using (var streamReader = new StreamReader(await response.Content.ReadAsStreamAsync()))
                                {
                                    return await streamReader.ReadToEndAsync();
                                }
                          
                    }

                }

                catch (Exception e)
                {
                    Console.WriteLine("{0} Exception caught.", e);

                    return "";
                }
            }

        }

    }
