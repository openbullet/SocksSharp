using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace SocksSharp.Extensions
{
    internal static class HttpHeadersExtensions
    {
        private static readonly string[] commaHeaders = new[] { "Accept", "Accept-Encoding" };
        
        public static string GetHeaderString(this HttpHeaders headers, string key)
        {
            if(headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            string value = string.Empty;

            headers.TryGetValues(key, out IEnumerable<string> values);

            if(values != null && values.Count() > 1)
            {
                var separator = commaHeaders.Contains(key) ? ", " : " ";
                value = string.Join(separator, values.ToArray());
            }
            
            return value;
        }
    }
}
