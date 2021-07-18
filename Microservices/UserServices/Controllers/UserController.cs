using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using UserService;
using UserService.Data;
using UserService.Entities;

namespace UserServices.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserServiceContext dbContext;
        private readonly IntegrationEventSenderService integEventSenderSvc;

        public  UserController(UserServiceContext context, IntegrationEventSenderService integEventSenderSvc)
        {
            this.dbContext = context;
            this.integEventSenderSvc = integEventSenderSvc;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUser()
        {
            return await dbContext.User.ToListAsync();
        }
        
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, User user)
        {
            using var transaction = dbContext.Database.BeginTransaction();

            dbContext.Entry(user).State = EntityState.Modified;
            await dbContext.SaveChangesAsync();
            
            var integrationEventData = JsonConvert.SerializeObject(new
            {
                id = user.ID,
                newname = user.Name,
                version = user.Version
            });
            //PublishToMessageQueue("user.update", integrationEventData);

            dbContext.IntegrationEventOutbox.Add(
                new IntegrationEvent()
                {
                    Event = "user.update",
                    Data = integrationEventData
                });

            _ = dbContext.SaveChanges();
            transaction.Commit();
            integEventSenderSvc.StartPublishingOutstandingIntegrationEvents();

            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            var transaction = dbContext.Database.BeginTransaction();

            dbContext.User.Add(user);
            await dbContext.SaveChangesAsync();

            var integrationEventData = JsonConvert.SerializeObject(new
            {
                id = user.ID,
                name = user.Name,
                version = user.Version
            });
            //PublishToMessageQueue("user.add", integrationEventData);

            dbContext.IntegrationEventOutbox.Add(
                new IntegrationEvent
                {
                    Event = "user.add",
                    Data = integrationEventData
                });

            _ = dbContext.SaveChanges();
            transaction.Commit();

            integEventSenderSvc.StartPublishingOutstandingIntegrationEvents();

            return CreatedAtAction("GetUser", new { id = user.ID }, user);
        }

        private void PublishToMessageQueue(string integrationEvent, string eventData)
        {
            // TOOO: Reuse and close connections and channel, etc, 
            var factory = new ConnectionFactory();
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            var body = Encoding.UTF8.GetBytes(eventData);
            channel.BasicPublish(exchange: "user",
                                             routingKey: integrationEvent,
                                             basicProperties: null,
                                             body: body);
        }
    }
}
