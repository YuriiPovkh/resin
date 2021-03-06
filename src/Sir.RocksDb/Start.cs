﻿using Microsoft.Extensions.DependencyInjection;
using Sir.RocksDb.Store;

namespace Sir.RocksDb
{
    public class Start : IPluginStart
    {
        public void OnApplicationStartup(IServiceCollection services, ServiceProvider serviceProvider, IConfigurationProvider config)
        {
            services.AddSingleton(typeof(IKeyValueStore), 
                new RocksDbStore(serviceProvider.GetService<IConfigurationProvider>()));
        }
    }
}