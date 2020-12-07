using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace RFCBot.Migrations
{
    public partial class Initialize : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GithubSync",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Successful = table.Column<bool>(type: "boolean", nullable: false),
                    RanAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GithubSync", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    GithubUserId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Login = table.Column<string>(type: "varchar(200)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.GithubUserId);
                });

            migrationBuilder.CreateTable(
                name: "Issues",
                columns: table => new
                {
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Repository = table.Column<long>(type: "bigint", nullable: false),
                    User = table.Column<int>(type: "integer", nullable: false),
                    Assignee = table.Column<int>(type: "integer", nullable: true),
                    Open = table.Column<bool>(type: "boolean", nullable: false),
                    IsPullRequest = table.Column<bool>(type: "boolean", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Body = table.Column<string>(type: "text", nullable: true),
                    Locked = table.Column<bool>(type: "boolean", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Labels = table.Column<List<string>>(type: "text[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Issues", x => new { x.Number, x.Repository });
                    table.ForeignKey(
                        name: "FK_Issues_Users_Assignee",
                        column: x => x.Assignee,
                        principalTable: "Users",
                        principalColumn: "GithubUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Issues_Users_User",
                        column: x => x.User,
                        principalTable: "Users",
                        principalColumn: "GithubUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PullRequests",
                columns: table => new
                {
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Repository = table.Column<long>(type: "bigint", nullable: false),
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Assignee = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: true),
                    Body = table.Column<string>(type: "text", nullable: true),
                    Locked = table.Column<bool>(type: "boolean", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MergedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Commits = table.Column<int>(type: "integer", nullable: false),
                    Additions = table.Column<int>(type: "integer", nullable: false),
                    Deletions = table.Column<int>(type: "integer", nullable: false),
                    ChangedFiles = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequests", x => new { x.Number, x.Repository });
                    table.ForeignKey(
                        name: "FK_PullRequests_Users_Assignee",
                        column: x => x.Assignee,
                        principalTable: "Users",
                        principalColumn: "GithubUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IssueComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IssueNumber = table.Column<int>(type: "integer", nullable: false),
                    User = table.Column<int>(type: "integer", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IssueRepository = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueComments_Issues_IssueNumber_IssueRepository",
                        columns: x => new { x.IssueNumber, x.IssueRepository },
                        principalTable: "Issues",
                        principalColumns: new[] { "Number", "Repository" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueComments_Users_User",
                        column: x => x.User,
                        principalTable: "Users",
                        principalColumn: "GithubUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeedbackRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Initiator = table.Column<int>(type: "integer", nullable: false),
                    Requested = table.Column<int>(type: "integer", nullable: false),
                    IssueNumber = table.Column<int>(type: "integer", nullable: false),
                    IssueRepository = table.Column<long>(type: "bigint", nullable: false),
                    FeedbackComment = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackRequests_IssueComments_FeedbackComment",
                        column: x => x.FeedbackComment,
                        principalTable: "IssueComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeedbackRequests_Issues_IssueNumber_IssueRepository",
                        columns: x => new { x.IssueNumber, x.IssueRepository },
                        principalTable: "Issues",
                        principalColumns: new[] { "Number", "Repository" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeedbackRequests_Users_Initiator",
                        column: x => x.Initiator,
                        principalTable: "Users",
                        principalColumn: "GithubUserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeedbackRequests_Users_Requested",
                        column: x => x.Requested,
                        principalTable: "Users",
                        principalColumn: "GithubUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Proposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IssueNumber = table.Column<int>(type: "integer", nullable: false),
                    IssueRepository = table.Column<long>(type: "bigint", nullable: false),
                    Initiator = table.Column<int>(type: "integer", nullable: false),
                    InitiatingComment = table.Column<int>(type: "integer", nullable: false),
                    BotTrackingComment = table.Column<int>(type: "integer", nullable: false),
                    Disposition = table.Column<string>(type: "text", nullable: true),
                    Start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Closed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Proposals_IssueComments_BotTrackingComment",
                        column: x => x.BotTrackingComment,
                        principalTable: "IssueComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Proposals_IssueComments_InitiatingComment",
                        column: x => x.InitiatingComment,
                        principalTable: "IssueComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Proposals_Issues_IssueNumber_IssueRepository",
                        columns: x => new { x.IssueNumber, x.IssueRepository },
                        principalTable: "Issues",
                        principalColumns: new[] { "Number", "Repository" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Concerns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Proposal = table.Column<int>(type: "integer", nullable: false),
                    Initiator = table.Column<int>(type: "integer", nullable: false),
                    ResolvedComment = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    InitiatingComment = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Concerns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Concerns_IssueComments_InitiatingComment",
                        column: x => x.InitiatingComment,
                        principalTable: "IssueComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Concerns_IssueComments_ResolvedComment",
                        column: x => x.ResolvedComment,
                        principalTable: "IssueComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Concerns_Proposals_Proposal",
                        column: x => x.Proposal,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Concerns_Users_Initiator",
                        column: x => x.Initiator,
                        principalTable: "Users",
                        principalColumn: "GithubUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Proposal = table.Column<int>(type: "integer", nullable: false),
                    Reviewer = table.Column<int>(type: "integer", nullable: false),
                    Reviewed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewRequests_Proposals_Proposal",
                        column: x => x.Proposal,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewRequests_Users_Reviewer",
                        column: x => x.Reviewer,
                        principalTable: "Users",
                        principalColumn: "GithubUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Concerns_InitiatingComment",
                table: "Concerns",
                column: "InitiatingComment");

            migrationBuilder.CreateIndex(
                name: "IX_Concerns_Initiator",
                table: "Concerns",
                column: "Initiator");

            migrationBuilder.CreateIndex(
                name: "IX_Concerns_Proposal",
                table: "Concerns",
                column: "Proposal");

            migrationBuilder.CreateIndex(
                name: "IX_Concerns_ResolvedComment",
                table: "Concerns",
                column: "ResolvedComment");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackRequests_FeedbackComment",
                table: "FeedbackRequests",
                column: "FeedbackComment");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackRequests_Initiator",
                table: "FeedbackRequests",
                column: "Initiator");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackRequests_IssueNumber_IssueRepository",
                table: "FeedbackRequests",
                columns: new[] { "IssueNumber", "IssueRepository" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackRequests_Requested",
                table: "FeedbackRequests",
                column: "Requested");

            migrationBuilder.CreateIndex(
                name: "IX_IssueComments_IssueNumber_IssueRepository",
                table: "IssueComments",
                columns: new[] { "IssueNumber", "IssueRepository" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueComments_User",
                table: "IssueComments",
                column: "User");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_Assignee",
                table: "Issues",
                column: "Assignee");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_User",
                table: "Issues",
                column: "User");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_BotTrackingComment",
                table: "Proposals",
                column: "BotTrackingComment");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_InitiatingComment",
                table: "Proposals",
                column: "InitiatingComment");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_IssueNumber_IssueRepository",
                table: "Proposals",
                columns: new[] { "IssueNumber", "IssueRepository" });

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_Assignee",
                table: "PullRequests",
                column: "Assignee");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRequests_Proposal",
                table: "ReviewRequests",
                column: "Proposal");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRequests_Reviewer",
                table: "ReviewRequests",
                column: "Reviewer");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Concerns");

            migrationBuilder.DropTable(
                name: "FeedbackRequests");

            migrationBuilder.DropTable(
                name: "GithubSync");

            migrationBuilder.DropTable(
                name: "PullRequests");

            migrationBuilder.DropTable(
                name: "ReviewRequests");

            migrationBuilder.DropTable(
                name: "Proposals");

            migrationBuilder.DropTable(
                name: "IssueComments");

            migrationBuilder.DropTable(
                name: "Issues");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
