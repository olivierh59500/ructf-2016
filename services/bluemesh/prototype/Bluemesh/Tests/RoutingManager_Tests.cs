using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Node.Connections;
using Node.Connections.LocalTcp;
using Node.Messages;
using Node.Routing;
using NSubstitute;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    internal class RoutingManager_Tests
    {
        [Test, Explicit, Timeout(2000)]
        public void Measure_map_negotiation()
        {
            var config = Substitute.For<IRoutingConfig>();
            config.DesiredConnections.Returns(2);
            config.MaxConnections.Returns(3);
            var nodes = Enumerable.Range(0, 5).Select(i => MakeNode(config)).ToList();

            ThreadPool.SetMinThreads(nodes.Count * 2, nodes.Count * 2);

            var trigger = new ManualResetEventSlim();
            var tasks = nodes.Select(n => Task.Run(() =>
            {
                trigger.Wait();
                n.Start();
            })).ToList();

            var watch = Stopwatch.StartNew();
            trigger.Set();
            Task.WhenAll(tasks).Wait();

            Console.WriteLine("Communication took " + watch.Elapsed);
        }

        private static TestNode MakeNode(IRoutingConfig config)
        {
            var connectionManager = new LocalTcpConnectionManager();
            return new TestNode(new RoutingManager(connectionManager, config), connectionManager);
        }

        private class TestNode
        {
            public TestNode(RoutingManager routingManager, LocalTcpConnectionManager connectionManager)
            {
                this.routingManager = routingManager;
                this.connectionManager = connectionManager;
            }

            public void Start()
            {
                while (true)
                {
                    Tick();
                    Thread.Sleep(100.Milliseconds());
                }
            }

            private void Tick()
            {
                routingManager.DisconnectExcessLinks();
                routingManager.ConnectNewLinks();
                routingManager.PullMaps();
                routingManager.PushMaps();

                connectionManager.PurgeDeadConnections();
                try
                {
                    foreach (var peer in connectionManager.GetAvailablePeers())
                        connectionManager.TryConnect(peer);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                //TODO use it
                var selectResult = connectionManager.Select();

                Console.WriteLine("[{0}] v: {2} {1}", routingManager.Map.OwnAddress, routingManager.Map, routingManager.Map.Version);
            }

            private readonly RoutingManager routingManager;
            private readonly LocalTcpConnectionManager connectionManager;
        }
    }
}