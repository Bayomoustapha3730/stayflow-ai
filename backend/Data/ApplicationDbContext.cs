using Microsoft.EntityFrameworkCore;

namespace StayFlow.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
}
