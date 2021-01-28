using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters.Json.Internal;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SavingIdeas.AzureFunction;
using SavingIdeas.Common.Models;
using SavingIdeas.EFCore.DataContext;

using SavingIdeas.Common.Models.Interface;
using SavingIdeas.EFCore.Repository;
using SavingIdeas.QlikSense;

[assembly: FunctionsStartup(typeof(SavingIdeasAzureFunctionStartup))]
namespace SavingIdeas.AzureFunction
{
    public class SavingIdeasAzureFunctionStartup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var configuration = this.CreateFunctionAppConfiguration();
            builder.Services.Configure<SavingIdeaConfiguration>(configuration.GetSection("SavingIdeaSettings"));
            builder.Services.AddSingleton(resolver => resolver.GetRequiredService<IOptions<SavingIdeaConfiguration>>().Value);
            var savingIdeaConfiguration = builder.Services.BuildServiceProvider().GetService<SavingIdeaConfiguration>();
            var hostingEnvironment = builder.Services.BuildServiceProvider().GetService<IHostingEnvironment>();
            AddSavingIdeaDatabase(builder.Services, savingIdeaConfiguration, hostingEnvironment.IsDevelopment());
            AddTswRepositoryServices(builder.Services);
            AddQlikSenseServices(builder.Services);

            // TODO
            // Azure Function HttpTrigger Model Binding - Collections not deserialized in v2.0.12050.0
            // Workaround : https://github.com/Azure/azure-functions-host/issues/3370#issuecomment-512906790
            //builder.Services.AddTransient<IConfigureOptions<MvcOptions>, MvcJsonMvcOptionsSetup>();
            //builder.Services.AddSingleton(LoggerFactory.Create(
            //        builder =>
            //        {
            //            builder.AddConsole();
            //            builder.AddDebug();
            //        }));

            builder.Services.AddLogging();

            //TODO
            //.NET Core supports only ASCII, ISO - 8859 - 1 and Unicode encodings,
            //whereas.NET Framework supports much more.
            // https://stackoverflow.com/a/50875725
            //
            //https://github.com/ExcelDataReader/ExcelDataReader#important-note-on-net-core
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        }

        private IConfigurationRoot CreateFunctionAppConfiguration()
        {
            var hostname = Environment.MachineName.ToLower();
            var environmentVariable = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            environmentVariable = string.IsNullOrWhiteSpace(environmentVariable) ? "Production" : environmentVariable;
            var configuration = new ConfigurationBuilder()
                    .SetBasePath(Environment.CurrentDirectory)
                    .AddJsonFile("appsettings.json", true)
                    .AddJsonFile($"appsettings.{hostname}.json", true)
                    .AddJsonFile($"appsettings.{environmentVariable}.json", true)
                    .AddEnvironmentVariables()
                    .Build();
            return configuration;
        }

        private void AddSavingIdeaDatabase(IServiceCollection services, SavingIdeaConfiguration savingIdeaConfiguration, bool isDevelopmentEnvironment)
        {
            var savingIdeaConnectionString = savingIdeaConfiguration.ConnectionStrings.SavingIdeaDataContext;
            services.AddDbContext<SavingIdeaDataContext>(options =>
            {
                options.UseSqlServer(savingIdeaConnectionString);
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                options.EnableSensitiveDataLogging(isDevelopmentEnvironment);
            });
        }

        private static void AddTswRepositoryServices(IServiceCollection services)
        {
            services.AddTransient<ISavingIdeaRepository, SavingIdeaRepository>();
            services.AddTransient<IAuditRepository, AuditRepository>();
        }
        private void AddQlikSenseServices(IServiceCollection services)
        {
            services.AddSingleton<IQlikSenseAppData, QlikSenseAppData>();
            services.AddSingleton<IQlikSenseSheetData, QlikSenseSheetData>();
            services.AddSingleton<IQlikSenseServices, QlikSenseServices>();
        }
    }
}
