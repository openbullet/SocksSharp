﻿using System;
using System.Net;
using System.Security;
using System.Threading;
using System.Net.Sockets;

namespace SocksSharp.Proxy
{
    /// <summary>
    /// Represents Proxy Client to <see cref="ProxyClientHandler{T}"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ProxyClient<T> : IProxyClient<T> where T : IProxy
    {
        private readonly T client;
        
        /// <summary>
        /// Gets or sets proxy settings for client
        /// </summary>
        public ProxySettings Settings { get; set; }
                     
        /// <summary>
        /// Initialize a new instance of the <see cref="ProxyClient{T}"/> with <see cref="IProxy"/> proxy handler
        /// </summary>
        public ProxyClient()
        {
            this.client = (T) Activator.CreateInstance(typeof(T));
        }

        /// <summary>
        /// Create connection via proxy to destination host
        /// </summary>
        /// <returns>Destination <see cref="NetworkStream"/></returns>
        /// <exception cref="System.InvalidOperationException">
        /// Value of <see cref="ProxySettings.Host"/> equals <see langword="null"/> or empty.
        /// -or-
        /// Value of <see cref="ProxySettings.Port"/> less than 1 or greater than 65535.
        /// -or-
        /// Value of <see cref="ProxySettings.Credentials"/> username length greater than 255.
        /// -or-
        /// Value of <see cref="ProxySettings.Credentials"/> password length greater than 255.
        /// </exception>
        public NetworkStream GetDestinationStream(string destinationHost, int destinationPort)
        {
            TcpClient tcpClient = new TcpClient()
            {
                ReceiveTimeout = Settings.ReadWriteTimeOut,
                SendTimeout = Settings.ReadWriteTimeOut
            };

            client.Settings = Settings;
            Exception connectException = null;
            var connectDoneEvent = new ManualResetEventSlim();

            var host = Settings.Host;
            var port = Settings.Port;

            // Added by Ruri for the NoProxy case, connect directly to the server without proxy
            if (client is NoProxy)
            {
                host = destinationHost;
                port = destinationPort;
            }

            #region Create Connection

            try
            {
                tcpClient.BeginConnect(host, port, new AsyncCallback(
                    (ar) =>
                    {
                        if (tcpClient.Client != null)
                        {
                            try
                            {
                                tcpClient.EndConnect(ar);
                            }
                            catch (Exception ex)
                            {
                                connectException = ex;
                            }

                            connectDoneEvent.Set();
                        }
                    }), tcpClient
                );
            }
            catch (Exception ex)
            {
                tcpClient.Close();

                if (ex is SocketException || ex is SecurityException)
                {
                    throw new ProxyException($"Failed to connect to {(client is NoProxy ? "server" : "proxy-server")}", ex);
                }

                throw;
            }

            if (!connectDoneEvent.Wait(Settings.ConnectTimeout))
            {
                tcpClient.Close();
                throw new ProxyException($"Failed to connect to {(client is NoProxy ? "server" : "proxy-server")}");
            }

            if (connectException != null)
            {
                tcpClient.Close();

                if (connectException is SocketException)
                {
                    throw new ProxyException($"Failed to connect to {(client is NoProxy ? "server" : "proxy-server")}", connectException);
                }
                else
                {
                    throw connectException;
                }
            }

            if (!tcpClient.Connected)
            {
                tcpClient.Close();
                throw new ProxyException($"Failed to connect to {(client is NoProxy ? "server" : "proxy-server")}");
            }

            #endregion

            tcpClient.SendTimeout = Settings.ReadWriteTimeOut;
            tcpClient.ReceiveTimeout = Settings.ReadWriteTimeOut;
            
            var connectedTcpClient = client.CreateConnection(
                destinationHost,
                destinationPort,
                tcpClient);

            return connectedTcpClient.GetStream();
        }
                        
    }
}