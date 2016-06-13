using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Orleans.Streams;
using Orleans.Runtime.Configuration;

namespace Orleankka.Embedded
{
    using Client;
    using Cluster;

    public class EmbeddedConfigurator : IActorSystemConfigurator
    {
        readonly ClientConfigurator client;
        readonly ClusterConfigurator cluster;
        readonly AppDomain domain;

        public EmbeddedConfigurator(AppDomainSetup setup)
        {
            domain  = AppDomain.CreateDomain("EmbeddedOrleans", null, setup ?? AppDomain.CurrentDomain.SetupInformation);
            client  = new ClientConfigurator();
            cluster = (ClusterConfigurator)domain.CreateInstanceAndUnwrap(
                        GetType().Assembly.FullName, typeof(ClusterConfigurator).FullName, false,
                        BindingFlags.NonPublic | BindingFlags.Instance , null,
                        new object[0], null, null);
        }

        public EmbeddedConfigurator From(ClusterConfiguration config)
        {
            cluster.From(config);
            return this;
        }

        public EmbeddedConfigurator From(ClientConfiguration config)
        {
            client.From(config);
            return this;
        }
    
        public EmbeddedConfigurator Run<T>(object properties = null) where T : IBootstrapper
        {
            cluster.Run<T>(properties);
            return this;
        }

        public EmbeddedConfigurator Register<T>(string name, IDictionary<string, string> properties = null) where T : IStreamProviderImpl
        {
            cluster.Register<T>(name, properties);
            client.Register<T>(name, properties);
            return this;
        }

        void IActorSystemConfigurator.Hook<T>()
        {
            ((IActorSystemConfigurator)cluster).Hook<T>();
            ((IActorSystemConfigurator)client).Hook<T>();
        }

        T[] IActorSystemConfigurator.Hooks<T>()
        {
            return ((IActorSystemConfigurator)client).Hooks<T>().Concat(
                   ((IActorSystemConfigurator)cluster).Hooks<T>()).ToArray();
        }

        void IActorSystemConfigurator.Register(ActorConfiguration[] configs)
        {
            throw new InvalidOperationException();
        }

        public virtual IActorSystem Done()
        {
            var clusterSystem = cluster.Done();
            var clientSystem = client.Done();

            return new EmbeddedActorSystem(domain, clientSystem, clusterSystem);
        }
    }

    public static class EmbeddedConfiguratorExtensions
    {
        public static EmbeddedConfigurator Embedded(this IActorSystemConfigurator root, AppDomainSetup setup = null)
        {
            return new EmbeddedConfigurator(setup);
        }
    }
}