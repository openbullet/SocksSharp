using System;
using System.IO;
using System.Net;
using System.Text;
using System.Net.Sockets;
using SocksSharp.Helpers;
using System.Threading;
using System.Text.RegularExpressions;

namespace SocksSharp.Proxy
{
    public class Http : IProxy
    {
        public IProxySettings Settings { get; set; }
        public string ProtocolVersion { get; set; } = "1.1";

        /// <summary>
        /// Create connection to destination host via proxy server.
        /// </summary>
        /// <param name="destinationHost">Host</param>
        /// <param name="destinationPort">Port</param>
        /// <param name="client">Connection with proxy server.</param>
        /// <returns>Connection to destination host</returns>
        /// <exception cref="System.ArgumentException">Value of <paramref name="destinationHost"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Value of <paramref name="destinationPort"/> less than 1 or greater than 65535.</exception>
        /// <exception cref="ProxyException">Error while working with proxy.</exception>
        public TcpClient CreateConnection(string destinationHost, int destinationPort, TcpClient client)
        {
            if (String.IsNullOrEmpty(destinationHost))
            {
                throw new ArgumentException(nameof(destinationHost));
            }

            if (!ExceptionHelper.ValidateTcpPort(destinationPort))
            {
                throw new ArgumentOutOfRangeException(nameof(destinationPort));
            }

            if (client == null || !client.Connected)
            {
                throw new SocketException();
            }

            HttpStatusCode statusCode;

            try
            {
                NetworkStream nStream = client.GetStream();

                SendConnectionCommand(nStream, destinationHost, destinationPort);
                statusCode = ReceiveResponse(nStream);
            }
            catch (Exception ex)
            {
                client.Close();

                if (ex is IOException || ex is SocketException)
                {
                    throw new ProxyException("Error while working with proxy", ex);
                }

                throw;
            }

            if (statusCode == HttpStatusCode.OK)
                return client;

            client.Close();
            throw new ProxyException("The proxy didn't reply with 200 OK");
        }

        #region Methods (private)

        private void SendConnectionCommand(Stream nStream, string destinationHost, int destinationPort)
        {
            var commandBuilder = new StringBuilder();

            commandBuilder.AppendFormat("CONNECT {0}:{1} HTTP/{2}\r\n", destinationHost, destinationPort, ProtocolVersion);
            commandBuilder.AppendFormat(GenerateAuthorizationHeader());
            commandBuilder.AppendLine();

            var buffer = Encoding.ASCII.GetBytes(commandBuilder.ToString());

            nStream.Write(buffer, 0, buffer.Length);
        }

        private string GenerateAuthorizationHeader()
        {
            if (Settings.Credentials == null || 
                (string.IsNullOrEmpty(Settings.Credentials.UserName) && string.IsNullOrEmpty(Settings.Credentials.Password)))
                return string.Empty;

            string data = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{Settings.Credentials.UserName}:{Settings.Credentials.Password}"));

            return $"Proxy-Authorization: Basic {data}\r\n";
        }

        private HttpStatusCode ReceiveResponse(NetworkStream nStream)
        {
            var buffer = new byte[50];
            var responseBuilder = new StringBuilder();

            WaitData(nStream);

            do
            {
                int bytesRead = nStream.Read(buffer, 0, buffer.Length);
                responseBuilder.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
            } while (nStream.DataAvailable);

            string response = responseBuilder.ToString();

            if (response.Length == 0)
                throw new ProxyException("Received empty response");

            // Выделяем строку статуса. Пример: HTTP/1.1 200 OK\r\n
            var match = Regex.Match(response, "HTTP/[0-9\\.]* ([0-9]{3})");
            
            if (!match.Success)
                throw new ProxyException("Received wrong response from proxy");

            if (!Enum.TryParse(match.Groups[1].Value, out HttpStatusCode statusCode))
                throw new ProxyException("Invalid status code");

            return statusCode;
        }

        private void WaitData(NetworkStream nStream)
        {
            int sleepTime = 0;
            int delay = nStream.ReadTimeout < 10 ?
                10 : nStream.ReadTimeout;

            while (!nStream.DataAvailable)
            {
                if (sleepTime >= delay)
                    throw new ProxyException("Timed out while waiting for data from proxy");

                sleepTime += 10;
                Thread.Sleep(10);
            }
        }

        #endregion
    }
}
