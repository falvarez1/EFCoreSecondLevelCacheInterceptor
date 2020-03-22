using System;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Issue9SQLiteInt32.DataLayer;
using EFCoreSecondLevelCacheInterceptor;
using Microsoft.EntityFrameworkCore;

namespace Issue9SQLiteInt32
{
    public static class EFServiceProvider
    {
        private static readonly Lazy<IServiceProvider> _serviceProviderBuilder =
                new Lazy<IServiceProvider>(getServiceProvider, LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// A lazy loaded thread-safe singleton
        /// </summary>
        public static IServiceProvider Instance { get; } = _serviceProviderBuilder.Value;

        public static T GetRequiredService<T>()
        {
            return Instance.GetRequiredService<T>();
        }

        public static void RunInContext(Action<ApplicationDbContext> action)
        {
            using var serviceScope = GetRequiredService<IServiceScopeFactory>().CreateScope();
            using var context = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            action(context);
        }

        public static async Task RunInContextAsync(Func<ApplicationDbContext, Task> action)
        {
            using var serviceScope = GetRequiredService<IServiceScopeFactory>().CreateScope();
            using var context = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await action(context);
        }

        private static IServiceProvider getServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddOptions();

            services.AddLogging(cfg => cfg.AddConsole().AddDebug());

            var basePath = Directory.GetCurrentDirectory();
            Console.WriteLine($"Using `{basePath}` as the ContentRootPath");
            var configuration = new ConfigurationBuilder()
                                .SetBasePath(basePath)
                                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                                .Build();
            services.AddSingleton(_ => configuration);

            services.AddEFSecondLevelCache(options =>
                options.UseMemoryCacheProvider()
            // options.UseRedisCacheProvider(configuration["RedisConfiguration"])
            );

            services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options
                        .AddInterceptors(new SecondLevelCacheInterceptor())
                        .UseSqlite(GetConnectionString(basePath, configuration));
                });

            return services.BuildServiceProvider();
        }

        public static string GetConnectionString(string basePath, IConfigurationRoot configuration)
        {
            var testsFolder = basePath.Split(new[] { "\\Issues\\" }, StringSplitOptions.RemoveEmptyEntries)[0];
            var contentRootPath = Path.Combine(testsFolder, "Issues", "Issue9SQLiteInt32");
            var connectionString = configuration["ConnectionStrings:ApplicationDbContextConnection"];
            if (connectionString.Contains("%CONTENTROOTPATH%"))
            {
                connectionString = connectionString.Replace("%CONTENTROOTPATH%", contentRootPath);
            }
            Console.WriteLine($"Using {connectionString}");
            return connectionString;
        }
    }
}