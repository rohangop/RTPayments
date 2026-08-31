using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Payments.Functions.Data;
using Payments.Functions.Providers;

const string sqlConnectionString =
    "Server=payments.database.windows.net;Database=Payments;User Id=placeholder;Password=placeholder";

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddTransient<IPaymentStore>(_ => new SqlPaymentStore(sqlConnectionString));
        services.AddTransient<IPaymentProvider, MockPaymentProvider>();
    })
    .Build();

host.Run();
