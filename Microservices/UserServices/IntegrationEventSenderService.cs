﻿using Microsoft.Extensions.DependencyInjection;
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

        public IntegrationEventSenderService(IServiceScopeFactory scopeFactory)
        {
            this.scopeFactory = scopeFactory;
            using var scope = scopeFactory.CreateScope();
            using var dbContext = scope.ServiceProvider.GetService<UserServiceContext>();
            dbContext.Database.EnsureCreated();
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

                            Console.WriteLine("Published: " + e.Event + " " + e.Data);
                            dbContext.Remove(e);
                            dbContext.SaveChanges();
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