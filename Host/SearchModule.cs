﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using log4net;
using Nancy;

namespace Resin
{
    public class SearchModule : NancyModule
    {
        private static readonly IDictionary<string, Searcher> Searchers = new Dictionary<string, Searcher>();
        private static readonly object Sync = new object();
        private static readonly ILog Log = LogManager.GetLogger(typeof(SearchClient));

        public SearchModule()
        {
            Get["/{indexName}/"] = parameters =>
            {
                var timer = new Stopwatch();
                timer.Start();
                var index = parameters.indexName;
                var query = Request.Query.query;
                var page = (int) Request.Query.page;
                var size = (int)Request.Query.size;
                var searcher = GetSearcher(index);
                var result = searcher.Search(query, page, size);
                var resolved = result.Resolve();
                Log.InfoFormat("query-exec in {0}: {1}", timer.Elapsed, query);
                return resolved;
            };
        }

        private Searcher GetSearcher(string name)
        {
            lock (Sync)
            {
                var dir = Path.Combine(GetBaseFolder(), name);
                Searcher searcher;
                if (!Searchers.TryGetValue(dir, out searcher))
                {
                    searcher = new Searcher(dir);
                    Searchers.Add(dir, searcher);
                }
                return searcher; 
            }
        }

        private static string GetBaseFolder()
        {
            string path = Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).FullName;
            if (Environment.OSVersion.Version.Major >= 6)
            {
                path = Directory.GetParent(path).ToString();
            }
            return Path.Combine(path, "Resin");
        }
    }
}