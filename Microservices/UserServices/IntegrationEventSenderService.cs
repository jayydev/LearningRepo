using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UserService.Data;

namespace UserService
{
    public class IntegrationEventSenderService : BackgroundService
    {
        private readonly IServiceScopeFactory scopeFactory;
        private CancellationTokenSource wakeupCancelationTokenSource = new CancellationTokenSource();


        public IntegrationEventSenderService(IServiceScopeFactory scopeFactory)
        {
            this.scopeFactory = scopeFactory;
            using var scope = scopeFactory.CreateScope();
            using var dbContext = scope.ServiceProvider.GetService<UserServiceContext>();
            dbContext.Database.EnsureCreated();
        }

        public void StartPublishingOutstandingIntegrationEvents()
        {
            wakeupCancelationTokenSource.Cancel();
        }


        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while(!cancellationToken.IsCancellationRequested)
            {
                await PublishOutstandingIntegrationEvents(cancellationToken);
            }
        }

        private async Task PublishOutstandingIntegrationEvents(CancellationToken cancellationToken)
        {
            try
            {
                var factory = new ConnectionFactory();
                var connection = factory.CreateConnection();
                var channel = connection.CreateModel();
                channel.ConfirmSelect();
                var props = channel.CreateBasicProperties();
                props.DeliveryMode = 2;

                while(!cancellationToken.IsCancellationRequested)
                {
                    {
                        using var scope = scopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetService<UserServiceContext>(); 
                        var events = dbContext.IntegrationEventOutbox.OrderBy(item => item.ID).ToList();
                        foreach(var e in events)
                        {
                            var body = Encoding.UTF8.GetBytes(e.Data);
                            channel.BasicPublish(exchange: "user",
                                routingKey:e.Event, basicProperties: null,
                                body:body);

                            channel.WaitForConfirmsOrDie(new TimeSpan(0, 0, 5));

                            Console.WriteLine("Published: " + e.Event + " " + e.Data);
                            dbContext.Remove(e);
                            dbContext.SaveChanges();
                        }
                    }

                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        wakeupCancelationTokenSource.Token,
                        cancellationToken);

                    try
                    {
                        await Task.Delay(Timeout.Infinite, linkedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        if (wakeupCancelationTokenSource.IsCancellationRequested)
                        {
                            Console.WriteLine("Publish requested");
                            var tmp = wakeupCancelationTokenSource;
                            wakeupCancelationTokenSource = new CancellationTokenSource();
                            tmp.Dispose();
                        }
                        else if (cancellationToken.IsCancellationRequested)
                        {
                            Console.WriteLine("Shutting down.");
                        }
                    }

                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                await Task.Delay(5000, cancellationToken);
            }

        }
    }
}
