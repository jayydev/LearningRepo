using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UserService.Data;
using UserService.Entities;

namespace UserServices.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserServiceContext context;

        public  UserController(UserServiceContext context)
        {
            this.context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUser()
        {
            return await context.User.ToListAsync();
        }
        
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, User user)
        {
            context.Entry(user).State = EntityState.Modified;
            await context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            context.User.Add(user);
            await context.SaveChangesAsync();
            return CreatedAtAction("GetUser", new { id = user.ID }, user);
        }       
    }
}
