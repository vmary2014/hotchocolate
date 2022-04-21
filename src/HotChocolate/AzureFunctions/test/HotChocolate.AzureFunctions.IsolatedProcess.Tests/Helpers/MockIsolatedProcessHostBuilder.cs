using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HotChocolate.AzureFunctions.IsolatedProcess.Tests.Helpers;
internal class MockIsolatedProcessHostBuilder : IHostBuilder
{
    public MockIsolatedProcessHostBuilder()
    {
        Services = new ServiceCollection();
        HostBuilderContext = new HostBuilderContext(Properties);
    }

    protected virtual IServiceCollection Services { get; }

    protected virtual HostBuilderContext HostBuilderContext { get; }

    public IHost Build()
        => new MockIsolatedProcessHost(Services.BuildServiceProvider());

    public IHostBuilder ConfigureAppConfiguration(Action<HostBuilderContext, IConfigurationBuilder> configureDelegate)
        => DoNothing();

    public IHostBuilder ConfigureContainer<TContainerBuilder>(Action<HostBuilderContext, TContainerBuilder> configureDelegate)
        => DoNothing();

    public IHostBuilder ConfigureHostConfiguration(Action<IConfigurationBuilder> configureDelegate)
        => DoNothing();

    public IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate)
    {
        configureDelegate.Invoke(HostBuilderContext, Services);
        return this;
    }

    public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory)
        => DoNothing();

    public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory)
        => DoNothing();

    public IHostBuilder DoNothing() { return this; } //DO NOTHING;

    public IDictionary<object, object> Properties { get; } = new Dictionary<object, object>();
}

internal class MockIsolatedProcessHost : IHost
{
    public MockIsolatedProcessHost(IServiceProvider serviceProvider)
        => Services = serviceProvider;

    public void Dispose() { } //DO NOTHING;

    public Task StartAsync(CancellationToken cancellationToken = new CancellationToken())
        => DoNothingAsync();

    public Task StopAsync(CancellationToken cancellationToken = new CancellationToken())
        => DoNothingAsync();

    public Task DoNothingAsync() { return Task.CompletedTask; } //DO NOTHING;

    public IServiceProvider Services { get; }
}
