using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Models;

namespace HengcordTCG.Shared.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<UserCard> UserCards => Set<UserCard>();
    public DbSet<PackType> PackTypes => Set<PackType>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<MatchResult> MatchResults => Set<MatchResult>();
    public DbSet<Deck> Decks => Set<Deck>();
    public DbSet<DeckCard> DeckCards => Set<DeckCard>();
    public DbSet<WikiPage> WikiPages => Set<WikiPage>();
    public DbSet<WikiHistory> WikiHistories => Set<WikiHistory>();
    public DbSet<WikiProposal> WikiProposals => Set<WikiProposal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DiscordId).IsUnique();
        });

        modelBuilder.Entity<MatchResult>(entity =>
        {
            entity.HasOne(m => m.Winner)
                .WithMany()
                .HasForeignKey(m => m.WinnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Loser)
                .WithMany()
                .HasForeignKey(m => m.LoserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Deck>(entity =>
        {
            entity.HasOne(d => d.Commander)
                .WithMany()
                .HasForeignKey(d => d.CommanderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WikiPage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            
            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WikiHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.WikiPage)
                .WithMany()
                .HasForeignKey(e => e.WikiPageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WikiProposal>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.WikiPage)
                .WithMany()
                .HasForeignKey(e => e.WikiPageId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
