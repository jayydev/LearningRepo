using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PostService.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PostService.Data
{
    public class DataAccess
    {
        private readonly List<string> dbConnectionStrings = new List<string>();

        public DataAccess(IConfiguration configuration)
        {
            var connectionStrings = configuration.GetSection("PostDbConnectionStrings");
            foreach (var connectionString in connectionStrings.GetChildren())
            {
                Console.WriteLine("ConnectionString: " + connectionString.Value);
                dbConnectionStrings.Add(connectionString.Value);
            }
        }
        public async Task<ActionResult<IEnumerable<Post>>> ReadLatestPosts(string category, int count)
        {
            using var dbContext = new PostServiceContext(GetConnectionString(category));
            return await dbContext.Post.OrderByDescending(p => p.PostId).Take(count).Include(x => x.User).Where(p => p.CategoryId == category).ToListAsync();
        }

        public async Task<int> CreatePost(Post post)
        {
            using var dbContext = new PostServiceContext(GetConnectionString(post.CategoryId));
            dbContext.Post.Add(post);
            return await dbContext.SaveChangesAsync();
        }

        public void InitDatabase(int countUsers, int countCategories)
        {
            foreach (var connectionString in dbConnectionStrings)
            {
                using var dbContext = new PostServiceContext(connectionString);
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();
                for (int i = 1; i <= countUsers; i++)
                {
                    dbContext.User.Add(new User { Name = "User" + i, Version = 1 });
                    dbContext.SaveChanges();
                }
                for (int i = 1; i <= countCategories; i++)
                {
                    dbContext.Category.Add(new Category { CategoryId = "Category" + i });
                    dbContext.SaveChanges();
                }
            }
        }

        private string GetConnectionString(string category)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.ASCII.GetBytes(category));
            var x = BitConverter.ToUInt16(hash, 0) % dbConnectionStrings.Count;
            return dbConnectionStrings[x];
        }
    }
}
