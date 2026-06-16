using Microsoft.EntityFrameworkCore;

namespace Pronetsys.Server.Data;

public class TestingDbContext : AppDb
{
    public TestingDbContext(IWebHostEnvironment hostEnvironment) 
        : base(hostEnvironment)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseInMemoryDatabase("Pronetsys");
        base.OnConfiguring(options);
    }
}
