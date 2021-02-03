using System;
using System.Diagnostics;
using System.Net.Http;
using Xunit;
using SocksSharp.Proxy;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Net.Http.Headers;

namespace SocksSharp.Tests
{
    public class NoProxyTests
    {
        #region Tests

        [Fact]
        public async Task RequestHeadersTest()
        {
            var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36";

            var message = new HttpRequestMessage();
            message.Method = HttpMethod.Get;
            message.RequestUri = new Uri("http://httpbin.org/user-agent");
            message.Headers.Add("User-Agent", userAgent);

            var response = await GetResponseMessageAsync(message);

            Assert.NotNull(response);

            var userAgentActual = await GetJsonStringValue(response, "user-agent");

            Assert.NotEmpty(userAgentActual);
            Assert.Equal(userAgent, userAgentActual);
        }

        [Fact]
        public async Task GetRequestTest()
        {
            var key = "key";
            var value = "value";

            var message = new HttpRequestMessage();
            message.Method = HttpMethod.Get;
            message.RequestUri = new Uri($"http://httpbin.org/get?{key}={value}");

            var response = await GetResponseMessageAsync(message);

            var actual = await GetJsonDictionaryValue(response, "args");

            Assert.True(actual.ContainsKey(key));
            Assert.True(actual.ContainsValue(value));
        }

        [Fact]
        public async Task GetUtf8Test()
        {
            var excepted = "∮";

            var message = new HttpRequestMessage();
            message.Method = HttpMethod.Get;
            message.RequestUri = new Uri("http://httpbin.org/encoding/utf8");


            var response = await GetResponseMessageAsync(message);
            var actual = await response.Content.ReadAsStringAsync();

            Assert.Contains(excepted, actual);
        }

        [Fact]
        public async Task GetHtmlPageTest()
        {
            long exceptedLength = 3741;
            var contentType = "text/html";
            var charSet = "utf-8";

            var message = new HttpRequestMessage();
            message.Method = HttpMethod.Get;
            message.RequestUri = new Uri("http://httpbin.org/html");

            var response = await GetResponseMessageAsync(message);

            var content = response.Content;
            Assert.NotNull(content);

            var headers = response.Content.Headers;
            Assert.NotNull(headers);

            Assert.NotNull(headers.ContentLength);
            Assert.Equal(exceptedLength, headers.ContentLength.Value);
            Assert.NotNull(headers.ContentType);
            Assert.Equal(contentType, headers.ContentType.MediaType);
            Assert.Equal(charSet, headers.ContentType.CharSet);
        }

        [Fact]
        public async Task DelayTest()
        {
            var message = new HttpRequestMessage();
            message.Method = HttpMethod.Get;
            message.RequestUri = new Uri("http://httpbin.org/delay/4");

            var response = await GetResponseMessageAsync(message);
            var source = response.Content.ReadAsStringAsync();

            Assert.NotNull(response);
            Assert.NotNull(source);
        }

        [Fact]
        public async Task StreamTest()
        {
            var message = new HttpRequestMessage();
            message.Method = HttpMethod.Get;
            message.RequestUri = new Uri("http://httpbin.org/stream/20");

            var response = await GetResponseMessageAsync(message);
            var source = response.Content.ReadAsStringAsync();

            Assert.NotNull(response);
            Assert.NotNull(source);
        }

        [Fact]
        public async Task GzipTest()
        {
            var excepted = "gzip, deflate";

            var message = new HttpRequestMessage();
            message.Method = HttpMethod.Get;
            message.RequestUri = new Uri("http://httpbin.org/gzip");

            var response = await GetResponseMessageAsync(message);
            var acutal = await GetJsonStringValue(response, "Accept-Encoding");

            Assert.NotNull(response);
            Assert.NotNull(acutal);
            Assert.Equal(excepted, acutal);
        }

        [Fact]
        public async Task ReceivedCookiesTest()
        {
            var name = "name";
            var value = "value";

            var message = new HttpRequestMessage();
            message.Method = HttpMethod.Get;
            message.RequestUri = new Uri($"http://httpbin.org/cookies/set?{name}={value}");

            var handler = CreateNoProxyHandler();
            handler.CookieContainer = new System.Net.CookieContainer();
            handler.UseCookies = true;
            var client = new HttpClient(handler);
            HttpResponseMessage response = await client.SendAsync(message);
            Assert.NotNull(response);
            var cookies = handler.CookieContainer.GetCookies(new Uri("http://httpbin.org/"));

            Assert.Equal(1, cookies.Count);
            var cookie = cookies[name];
            Assert.Equal(name, cookie.Name);
            Assert.Equal(value, cookie.Value);

            handler.Dispose();
            client.Dispose();
        }

        [Fact]
        public async Task SentCookies_WithContent_Test()
        {
            var name = "name";
            var value = "value";
            var uri = new Uri("http://httpbin.org/cookies");

            var message = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = uri
            };

            var handler = CreateNoProxyHandler();
            var cookieContainer = new CookieContainer();
            cookieContainer.Add(uri, new Cookie(name, value));
            handler.CookieContainer = cookieContainer;
            handler.UseCookies = true;
            var client = new HttpClient(handler);

            message.Content = new StringContent(string.Empty);
            message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");

            HttpResponseMessage response = await client.SendAsync(message);
            Assert.NotNull(response);

            var cookies = await GetJsonDictionaryValue(response, "cookies");
            Assert.Equal(1, cookies.Count);
            Assert.True(cookies.ContainsKey(name));
            Assert.Equal(value, cookies[name]);

            handler.Dispose();
            client.Dispose();
        }

        [Fact]
        public async Task StatusCodeTest()
        {
            var code = "404";
            var excepted = "NotFound";
            var message = new HttpRequestMessage();
            message.Method = HttpMethod.Get;
            message.RequestUri = new Uri($"http://httpbin.org/status/{code}");

            var response = await GetResponseMessageAsync(message);

            Assert.NotNull(response);
            Assert.Equal(excepted, response.StatusCode.ToString());
        }

        [Fact]
        public async Task HttpClient_SendAsync_SetExplicitHostHeader_ShouldNotFail()
        {
            var message = new HttpRequestMessage(HttpMethod.Get, "https://httpbin.org/headers");
            message.Headers.Host = "httpbin.org";

            var response = await GetResponseMessageAsync(message).ConfigureAwait(false);

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task HttpClient_SendAsync_SetCipherSuites_SendsCorrectly()
        {
            // Custom cipher suites are not currently supported on Windows as it uses
            // SecureChannel for the TLS handshake, and ciphers are set by the windows policies.
            // The only known workaround is implementing an OpenSSL / BouncyCastle solution or just using WSL or a linux VM.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            var message = new HttpRequestMessage(HttpMethod.Get, "https://www.howsmyssl.com/");
            message.Headers.Host = "howsmyssl.com";

            using var handler = new ProxyClientHandler<NoProxy>(new ProxySettings());
            handler.UseCustomCipherSuites = true;
            handler.AllowedCipherSuites = new TlsCipherSuite[]
            {
                TlsCipherSuite.TLS_AES_256_GCM_SHA384,
                TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256,
                TlsCipherSuite.TLS_AES_128_GCM_SHA256,
                TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384
            };

            using var client = new HttpClient(handler);
            var response = await client.SendAsync(message);
            var page = await response.Content.ReadAsStringAsync();

            Assert.Contains("TLS_AES_256_GCM_SHA384", page);
            Assert.Contains("TLS_CHACHA20_POLY1305_SHA256", page);
            Assert.Contains("TLS_AES_128_GCM_SHA256", page);
            Assert.Contains("TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384", page);

            // Make sure it does not contain a cipher suite that we didn't send but that is usually sent by System.Net
            Assert.DoesNotContain("TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256", page);
        }

        #endregion

        #region Helpers

        private ProxyClientHandler<Http> CreateFiddlerHandler() => new(new() { Host = "127.0.0.1", Port = 8888 });
        private ProxyClientHandler<NoProxy> CreateNoProxyHandler() => new(new ProxySettings());

        private async Task<string> GetJsonStringValue(HttpResponseMessage response, string valueName)
        {
            var source = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(source);

            var result = obj.TryGetValue(valueName, out JToken token);

            if (!result)
            {
                return String.Empty;
            }

            return token.Value<string>();
        }

        private async Task<Dictionary<string, string>> GetJsonDictionaryValue(HttpResponseMessage response, string valueName)
        {
            var source = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(source);

            var result = obj.TryGetValue(valueName, out JToken token);

            if (!result)
            {
                return null;
            }

            return token.ToObject<Dictionary<string, string>>();
        }

        private async Task<HttpResponseMessage> GetResponseMessageAsync(HttpRequestMessage requestMessage)
        {
            using var handler = CreateNoProxyHandler();
            using var client = new HttpClient(handler);
            return await client.SendAsync(requestMessage);
        }

        #endregion
    }
}
