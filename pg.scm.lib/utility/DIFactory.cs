using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace pg.scm.lib
{
    public  class DIFactory<T>
        where T:class
    {
        public IServiceProvider ConfigureDI()
        {
            // setup a commandline runner service with a logger and configurations injected
            var services = new ServiceCollection();

            // config 
            var configBuilder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json", optional: true);

            services.AddSingleton<IConfiguration>(configBuilder.Build());

            // domain services
            services.AddTransient<T>();
            services.AddTransient(typeof(IPGDump), typeof(PGDump));
            services.AddTransient(typeof(IPGDumpFile), typeof(PGDumpFile));
            services.AddTransient(typeof(IPGFileMaker), typeof(PGFileMaker));
            services.AddTransient(typeof(IPGInformationSchema), typeof(PGInformationSchema));
            services.AddTransient(typeof(IPGAudit), typeof(PGAudit));
            services.AddTransient(typeof(IPGDumpData), typeof(PGDataDump));

            // logger
            services.AddSingleton<ILoggerFactory, LoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddLogging((builder) => builder.SetMinimumLevel(LogLevel.Trace));
            var serviceProvider = services.BuildServiceProvider();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            loggerFactory.AddNLog(new NLogProviderOptions { CaptureMessageTemplates = true, CaptureMessageProperties = true });
            NLog.LogManager.LoadConfiguration("nlog.config");

            return serviceProvider;
        }

    }
}
