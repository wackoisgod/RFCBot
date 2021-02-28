using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update; 
namespace RFCBot
{
    public static partial class CustomExtensions
    {
        public static long GetNextSequenceValue(this DbContext context, string name, string schema = null)
        {
            var sqlGenerator = context.GetService<IUpdateSqlGenerator>();
            var sql = sqlGenerator.GenerateNextSequenceValueOperation(name, schema ?? context.Model.GetDefaultSchema());
            var rawCommandBuilder = context.GetService<IRawSqlCommandBuilder>();
            var command = rawCommandBuilder.Build(sql);
            var connection = context.GetService<IRelationalConnection>();
            var logger = context.GetService<IDiagnosticsLogger<DbLoggerCategory.Database.Command>>();
            var parameters = new RelationalCommandParameterObject(connection, null, null, context, logger);
            var result = command.ExecuteScalar(parameters);
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }
    }
    
    public class RFCContext : DbContext
    {
        public DbSet<GithubUser> Users { get; set; }
        public DbSet<RFCConcern> Concerns { get; set; }
        public DbSet<Githubsync> GithubSync { get; set; }
        public DbSet<Issue> Issues { get; set; }
        public DbSet<Issuecomment> IssueComments { get; set; }
        public DbSet<PullRequest> PullRequests { get; set; }
        public DbSet<RFCProposal> Proposals { get; set; }
        public DbSet<RFCReviewRequest> ReviewRequests { get; set; }
        public DbSet<RFCFeedbackRequest> FeedbackRequests { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseNpgsql("Host=localhost;Database=rfc;Username=foo;Password=bar");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GithubUser>()
                .HasMany<Issue>()
                .WithOne()
                .HasForeignKey(x => x.User);

            modelBuilder.Entity<GithubUser>()
                .HasMany<Issue>()
                .WithOne()
                .HasForeignKey(x => x.Assignee);

            modelBuilder.Entity<GithubUser>()
                .HasMany<Issuecomment>()
                .WithOne()
                .HasForeignKey(x => x.User);

            modelBuilder.Entity<GithubUser>()
                .HasMany<RFCFeedbackRequest>()
                .WithOne()
                .HasForeignKey(x => x.Initiator);

            modelBuilder.Entity<GithubUser>()
               .HasMany<RFCFeedbackRequest>()
               .WithOne()
               .HasForeignKey(x => x.Requested);

            modelBuilder.Entity<GithubUser>()
                .HasMany<RFCReviewRequest>()
                .WithOne()
                .HasForeignKey(x => x.Reviewer);

            modelBuilder.Entity<GithubUser>()
                .HasMany<RFCConcern>()
                .WithOne()
                .HasForeignKey(x => x.Initiator);

            modelBuilder.Entity<GithubUser>()
                .HasMany<PullRequest>()
                .WithOne()
                .HasForeignKey(x => x.Assignee);

            modelBuilder.Entity<Issuecomment>()
               .HasOne<Issue>()
               .WithMany()
               .HasPrincipalKey(x => new { x.Number, x.Repository } );

            modelBuilder.Entity<Issue>()
                .HasKey(i => new { i.Number, i.Repository });

            modelBuilder.Entity<PullRequest>()
                .HasKey(i => new { i.Number, i.Repository });

            modelBuilder.Entity<RFCProposal>()
                .HasOne<Issue>()
                .WithMany()
                .HasPrincipalKey(x => new { x.Number, x.Repository });

            modelBuilder.Entity<RFCProposal>()
                .HasOne<Issuecomment>()
                .WithMany()
                .HasForeignKey(x => x.BotTrackingComment);

            modelBuilder.Entity<RFCProposal>()
                .HasOne<Issuecomment>()
                .WithMany()
                .HasForeignKey(x => x.InitiatingComment);

            modelBuilder.Entity<RFCProposal>()
                .HasMany<RFCReviewRequest>()
                .WithOne()
                .HasForeignKey(x => x.Proposal);

            modelBuilder.Entity<RFCProposal>()
                .HasMany<RFCConcern>()
                .WithOne()
                .HasForeignKey(x => x.Proposal);

            modelBuilder.Entity<Issuecomment>()
                .HasMany<RFCConcern>()
                .WithOne()
                .HasForeignKey(x => x.InitiatingComment);

            modelBuilder.Entity<Issuecomment>()
                .HasMany<RFCConcern>()
                .WithOne()
                .HasForeignKey(x => x.ResolvedComment);

            modelBuilder.Entity<RFCFeedbackRequest>()
                .HasOne<Issue>()
                .WithMany()
                .HasPrincipalKey(x => new { x.Number, x.Repository });

            modelBuilder.Entity<Issuecomment>()
                .HasMany<RFCFeedbackRequest>()
                .WithOne()
                .HasForeignKey(x => x.FeedbackComment);

            modelBuilder.HasSequence("rfcnext");
        }
    }

    [Table("Users")]
    public class GithubUser
    {
        public int GithubUserId { get; set; }
        [Column(TypeName = "varchar(200)")]
        public string Login { get; set; }
    }

    [Table("GithubSync")]
    public class Githubsync
    {
        public int Id { get; set; }
        public bool Successful { get; set; }
        public DateTimeOffset RanAt { get; set; }
        public string? Message { get; set; }
    }

    [Table("Issues")]
    public class Issue
    {
        [Key]
        public int Number { get; set; }
        public int User { get; set; }
        public int? Assignee { get; set; }
        public bool Open { get; set; }
        public bool IsPullRequest { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public bool Locked { get; set; }
        public DateTimeOffset? ClosedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public List<string> Labels { get; set; }
        public long Repository { get; set; }
    }

    [Table("PullRequests")]
    public class PullRequest
    {
        public long Id { get; set; }

        [Key]
        public int Number { get; set; }
        public int? Assignee { get; set; }
        public string Title { get; set; }
        public string State { get; set; }
        public string Body { get; set; }
        public bool Locked { get; set; }
        public DateTimeOffset? ClosedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? MergedAt { get; set; }
        public int Commits { get; set; }
        public int Additions { get; set; }
        public int Deletions { get; set; }
        public int ChangedFiles { get; set; }
        public long Repository { get; set; }
    }

    [Table("IssueComments")]
    public class Issuecomment
    {
        public int Id { get; set; }
        public int IssueNumber { get; set; }
        public int User { get; set; }
        public string Body { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public long IssueRepository { get; set; }
    }

    [Table("FeedbackRequests")]
    public class RFCFeedbackRequest
    {
        public int Id { get; set; }
        public int Initiator { get; set; }
        public int Requested { get; set; }
        public int IssueNumber { get; set; }
        public long IssueRepository { get; set; }
        public int? FeedbackComment { get; set; }
    }

    [Table("ReviewRequests")]
    public class RFCReviewRequest
    {
        public int Id { get; set; }
        public int Proposal { get; set; }
        public int Reviewer { get; set; }
        public bool Reviewed { get; set; }
    }

    [Table("Proposals")]
    public class RFCProposal 
    {
        public int Id { get; set; }
        public int IssueNumber { get; set; }
        public long IssueRepository { get; set; }
        public int Initiator { get; set; }
        public int InitiatingComment { get; set; }
        public int BotTrackingComment { get; set; }
        public string Disposition { get; set; }
        public DateTimeOffset? Start { get; set; }
        public bool Closed { get; set; }
    }

    [Table("Concerns")]
    public class RFCConcern
    {
        public int Id { get; set; }
        public int Proposal { get; set; }
        public int Initiator { get; set; }
        public int? ResolvedComment { get; set; }
        public string Name { get; set; }
        public int InitiatingComment { get; set; }
    }

    [Table("Teams")]
    public class Team
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Ping { get; set; }
        public string Label { get; set; }
    }
}
