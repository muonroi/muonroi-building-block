using Microsoft.EntityFrameworkCore;
using Muonroi.Data.EntityFrameworkCore.Entity;

namespace Muonroi.BuildingBlock.Test;

public enum TestPermission
{
    Read = 1,
    Write = 2
}

public class TestDbContext : MDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
}
