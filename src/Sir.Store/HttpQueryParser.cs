﻿using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;

namespace Sir.Store
{
    /// <summary>
    /// Parse text from a http request message into a query.
    /// </summary>
    public class HttpQueryParser
    {
        private readonly TermQueryParser _queryParser;
        private readonly ITokenizer _tokenizer;

        public HttpQueryParser(TermQueryParser queryParser, ITokenizer tokenizer)
        {
            _queryParser = queryParser;
            _tokenizer = tokenizer;
        }

        public Query Parse(string collectionId, HttpRequest request)
        {
            Query query = null;

            string[] fields;

            bool and = request.Query.ContainsKey("AND");
            var termOperator = and ? "+" : "";

            if (request.Query.ContainsKey("fields"))
            {
                fields = request.Query["fields"].ToArray();
            }
            else
            {
                fields = new[] { "title", "body" };
            }

            var isFormatted = request.Query.ContainsKey("qf");

            if (isFormatted)
            {
                var formattedQuery = request.Query["qf"].ToString();
                query = FromString(collectionId.ToHash(), formattedQuery);
            }
            else
            {
                string queryFormat = string.Empty;

                if (request.Query.ContainsKey("format"))
                {
                    queryFormat = request.Query["format"].ToArray()[0];
                }
                else
                {
                    foreach (var field in fields)
                    {
                        queryFormat += (termOperator + field + ":{0}\n");
                    }

                    queryFormat = queryFormat.Substring(0, queryFormat.Length - 1);
                }

                var formattedQuery = string.Format(queryFormat, request.Query["q"]);

                query = _queryParser.Parse(collectionId.ToHash(), formattedQuery, _tokenizer);
            }

            if (request.Query.ContainsKey("take"))
                query.Take = int.Parse(request.Query["take"]);

            if (request.Query.ContainsKey("skip"))
                query.Skip = int.Parse(request.Query["skip"]);

            return query;
        }

        private Query FromString(ulong collectionId, string formattedQuery)
        {
            Query root = null;
            var lines = formattedQuery
                .Replace("\r", "\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                Query x = null;

                var cleanLine = line
                    .Replace("(", "")
                    .Replace(")", "")
                    .Replace("++", "+")
                    .Replace("--", "-");

                var terms = cleanLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    var query = _queryParser.Parse(collectionId, term, _tokenizer);

                    if (x == null)
                    {
                        x = query;
                    }
                    else
                    {
                        x.AddClause(query);
                    }
                }

                if (root == null)
                {
                    root = x;
                }
                else
                {
                    var last = root;
                    var next = last.Next;

                    while (next != null)
                    {
                        last = next;
                        next = last.Next;
                    }

                    last.Next = x;
                }
            }

            return root;
        }
    }
}
