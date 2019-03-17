﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using HtmlAgilityPack;
using Sir.Core;

namespace Sir.HttpServer.Features
{
    public class CrawlQueue : IDisposable, ILogger
    {
        private readonly ProducerConsumerQueue<Uri> _queue;
        private readonly PluginsCollection _plugins;

        public (Uri uri, string title) LastProcessed { get; private set; }

        public CrawlQueue(PluginsCollection plugins)
        {
            _queue = new ProducerConsumerQueue<Uri>(1, Submit);
            _plugins = plugins;
        }

        public void Enqueue(Uri uri)
        {
            _queue.Enqueue(uri);
        }

        private void Submit(Uri uri)
        {
            try
            {
                var robotTxt = GetWebString(new Uri(string.Format("{0}://{1}/robots.txt", uri.Scheme, uri.Host)));
                var allowed = true;

                if (robotTxt != null)
                {
                    var robotRules = GetForbiddenUrls(robotTxt);
                    var uriStr = uri.ToString();

                    foreach (var rule in robotRules)
                    {
                        if (uriStr.Contains(rule))
                        {
                            allowed = false;
                            break;
                        }
                    }
                }

                if (!allowed)
                {
                    this.Log(string.Format("url forbidden by robot.txt {0}", uri));

                    return;
                }

                var str = GetWebString(uri);

                if (str == null)
                {
                    return;
                }

                var html = new HtmlDocument();

                html.LoadHtml(str);

                var doc = Parse(html, uri);

                if (doc.title == null)
                {
                    this.Log(string.Format("error processing {0} (no title)", uri));
                    return;
                }

                var url = uri.ToString().Replace(uri.Scheme + "://", string.Empty);
                var document = new Dictionary<string, object>();

                document["_site"] = uri.Host;
                document["_url"] = url;
                document["url"] = url;
                document["body"] = doc.body;
                document["title"] = doc.title;
                document["_created"] = DateTime.Now.ToBinary();

                //TODO: implement write to store + write to index

                LastProcessed = (uri, (string)document["title"]);
            }
            catch (Exception ex)
            {
                this.Log(string.Format("error processing {0} {1}", uri, ex));
            }
        }

        private string GetWebString(Uri uri)
        {
            var urlStr = uri.ToString();

            try
            {
                var req = (HttpWebRequest)WebRequest.Create(uri);
                req.ReadWriteTimeout = 10 * 1000;
                req.Headers.Add("User-Agent", "gogorobot/didyougogo.com");
                req.Headers.Add("Accept", "text/html");

                using (var response = (HttpWebResponse)req.GetResponse())
                using (var content = response.GetResponseStream())
                using (var reader = new StreamReader(content))
                {
                    if (!response.GetResponseHeader("Content-Type").Contains("text/html"))
                    {
                        return null;
                    }

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        this.Log(string.Format("bad request: {0} response: {1}", uri, response.StatusCode));

                        return null;
                    }

                    this.Log(string.Format("requested: {0}", uri));

                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                this.Log(string.Format("request failed: {0} {1}", uri, ex));

                return null;
            }
        }

        private static HashSet<string> GetForbiddenUrls(string robotTxt)
        {
            var result = new HashSet<string>();

            foreach (var line in robotTxt.Split('\r', '\n'))
            {
                var parts = line.ToLower().Split(':');

                if (parts[0].Trim() == "disallow")
                {
                    var rule = parts[1].Trim(' ', '?').Replace("/*", string.Empty);

                    if (rule!="/")
                        result.Add(rule);
                }
            }

            return result;
        }

        private (string title, string body) Parse(HtmlDocument htmlDocument, Uri owner)
        {
            var titleNodes = htmlDocument.DocumentNode.SelectNodes("//title");

            if (titleNodes == null) return (null, null);

            var titleNode = titleNodes.FirstOrDefault();

            if (titleNode == null) return (null, null);

            var title = WebUtility.HtmlDecode(titleNode.InnerText);
            var root = htmlDocument.DocumentNode.SelectNodes("//body").First();
            var txtNodes = root.Descendants().Where(x =>
                x.Name == "#text" &&
                (x.ParentNode.Name != "script") &&
                (!string.IsNullOrWhiteSpace(x.InnerText))
            ).ToList();

            var ownerUrl = owner.Host;
            var txt = txtNodes.Select(x => WebUtility.HtmlDecode(x.InnerText));
            var body = string.Join("\r\n", txt);

            return (title, body);
        }

        public void Dispose()
        {
            _queue.Dispose();
        }
    }
}
