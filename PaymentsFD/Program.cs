using Azure.Messaging.ServiceBus;
using Payments.ServiceBus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string serviceBusConnectionString =
    "Endpoint=sb://payments.servicebus.windows.net/;SharedAccessKeyName=PaymentsFD;SharedAccessKey=placeholder";
const string serviceBusQueueName = "payments";

builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnectionString));
builder.Services.AddTransient<IPaymentBatchPublisher>(serviceProvider =>
    new ServiceBusPaymentBatchPublisher(
        serviceProvider.GetRequiredService<ServiceBusClient>(),
        serviceBusQueueName));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
