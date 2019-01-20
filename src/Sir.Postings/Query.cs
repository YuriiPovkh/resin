﻿using System;
using System.Collections.Generic;

namespace Sir.Postings
{
    /// <summary>
    /// A boolean query,
    /// </summary>
    public class Query
    {
        public Query()
        {
            PostingsOffset = -1;
            Score = -1;
            Or = true;
        }

        public bool And { get; set; }
        public bool Or { get; set; }
        public bool Not { get; set; }
        public long PostingsOffset { get; set; }
        public float Score { get; set; }

        public static IList<Query> FromStream(byte[] stream)
        {
            var result = new List<Query>();
            var offset = 0;

            while (offset < stream.Length)
            {
                var postingsOffset = BitConverter.ToInt64(stream, offset);

                offset += sizeof(long);

                var score = BitConverter.ToSingle(stream, offset);

                offset += sizeof(float);

                var booleanOperator = stream[offset];

                offset += sizeof(byte);

                var query = new Query { Score = score, PostingsOffset = postingsOffset, And = booleanOperator == 1, Or = booleanOperator == 2, Not = booleanOperator == 0 };

                result.Add(query);
            }

            return result;
        }
    }
}