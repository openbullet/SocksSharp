using System;
using System.Net.Sockets;
using SocksSharp.Helpers;

namespace SocksSharp.Proxy
{
    public class NoProxy : IProxy
    {
        public IProxySettings Settings { get; set; }
        public string ProtocolVersion { get; set; } = "1.1";

        /// <summary>
        /// Simply returns the existing connection without doing anything.
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

            return client;
        }
    }
}
