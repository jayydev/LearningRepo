using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PostService.Data;
using PostService.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PostService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PostServiceNewController : ControllerBase
    {
        private readonly DataAccess dataAccess;

        public PostServiceNewController(DataAccess dataAccess)
        {
            this.dataAccess = dataAccess;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Post>>> GetLastestPosts(string category, int count)
        {
            return await dataAccess.ReadLatestPosts(category, count);

        }

        [HttpPost]
        public async Task<ActionResult<Post>> CreatetPost(Post post)
        {
            await dataAccess.CreatePost(post);

            return NoContent();
        }

        [HttpGet("InitDatabase")]
        public void InitDatabase([FromQuery] int countUsers, [FromQuery] int categories)
        {
            dataAccess.InitDatabase(countUsers, categories);
        }
    }
}
