using Care.WebApi.Domain.Care;
using Care.WebApi.Domain.Common;
using Care.WebApi.Domain.Documents;
using Care.WebApi.Domain.Identity;
using Care.WebApi.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Care.WebApi.Infrastructure.Persistence.Context;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ReplacementRequest> ReplacementRequests => Set<ReplacementRequest>();
    public DbSet<ShiftNote> ShiftNotes => Set<ShiftNote>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
