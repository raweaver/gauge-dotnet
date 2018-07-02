// Copyright 2018 ThoughtWorks, Inc.
//
// This file is part of Gauge-CSharp.
//
// Gauge-CSharp is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Gauge-CSharp is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Gauge-CSharp.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Gauge.CSharp.Core;
using Gauge.Messages;
using Grpc.Core;
using NLog;

namespace Gauge.Dotnet
{
    public class GaugeListener : IGaugeListener
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly MessageProcessorFactory _messageProcessorFactory;

        public GaugeListener(MessageProcessorFactory messageProcessorFactory)
        {
            _messageProcessorFactory = messageProcessorFactory;
        }

        public void StartGrpcServer()
        {
            const int port = 54545;
            var server = new Server();
            server.Services.Add(lspService.BindService(new GaugeGrpcConnection(server)));
            server.Ports.Add(new ServerPort("127.0.0.1", port, ServerCredentials.Insecure));
            server.Start();
            Console.WriteLine("Listening on port:"+ port);
        }

        private static int GetRandomPort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        public void PollForMessages()
        {
            try
            {
                using (var gaugeConnection = new GaugeConnection(new TcpClientWrapper(Utils.GaugePort)))
                {
                    while (gaugeConnection.Connected)
                    {
                        var message = Message.Parser.ParseFrom(gaugeConnection.ReadBytes().ToArray());
                        var processor = _messageProcessorFactory.GetProcessor(message.MessageType,
                            message.MessageType == Message.Types.MessageType.SuiteDataStoreInit);
                        var response = processor.Process(message);
                        gaugeConnection.WriteMessage(response);
                        if (message.MessageType == Message.Types.MessageType.KillProcessRequest)
                            return;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }
    }
}