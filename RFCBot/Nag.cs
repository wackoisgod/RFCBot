using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Octokit;

using OneOf;

namespace RFCBot
{
    public abstract class CommentType : OneOfBase<
        CommentType.RFCProposed, 
        CommentType.RFCProposalCancelled, 
        CommentType.RFCAllReviewedNoConcerns, 
        CommentType.RFCWeekPassed>
    {
        public class RFCProposed : CommentType 
        { 
            public GithubUser User { get; set; }
            public RFCDisposition Disposition { get; set; }
            public (GithubUser, RFCReviewRequest)[] ReviewRequest { get; set; }
            public (GithubUser, RFCConcern)[] ReviewConcerns { get; set; }
            public int MinUserAmount { get; set; }
            public int PrecentOfUsers { get; set; }
        }

        public class RFCProposalCancelled: CommentType
        {
            public GithubUser User { get; set; }
        }

        public class RFCAllReviewedNoConcerns : CommentType
        {
            public GithubUser User { get; set; }
            public int StatusCommandId { get; set; }
            public bool AddedLabel { get; set; }
        }

        public class RFCWeekPassed : CommentType
        {
            public GithubUser User { get; set; }
            public int StatusCommandId { get; set; }
            public bool AddedLabel { get; set; }
            public RFCDisposition Disposition { get; set; }
        }
    }

    public class BotComment
    {
        public Issue issue;
        public string body;
        public CommentType commentType;

        public void AddCommentUrl(StringBuilder msg, Issue issue, int commentId)
        {
            var repoName = GitClient.GH.Value.GetRepo(issue.Repository).Result;
            if (repoName != null) {
                var s = $"{repoName.HtmlUrl}/{(issue.IsPullRequest ? "pull" : "issues")}/{issue.Number}#issuecomment-{commentId}";
                msg.Append(s);
            }
        }

        public IssueComment Post(int? existingComment)
        {
            if (issue.Open) {
                if (existingComment != null) {
                    return GitClient.GH.Value.EditIssueComment(issue.Repository, existingComment.Value, body).Result;
                }
                else {
                    return GitClient.GH.Value.AddIssueComment(issue.Repository, issue.Number, body).Result;
                }
            }
            else {
                // Should log something here that the issue is closed
            }

            return null;
        }

        public bool FormatProposed(StringBuilder builder, CommentType.RFCProposed p, Issue issue)
        {
            builder.Append("Team member @");
            builder.Append(p.User.Login);
            builder.Append(" has proposed to ");
            builder.Append(Enum.GetName(typeof(RFCDisposition), p.Disposition));
            builder.Append(" this. The next step is review by the rest of the tagged ");
            builder.Append("team members:\n\n");

            foreach (var r in p.ReviewRequest.Select(x => (x.Item1, x.Item2.Reviewed))) {
               builder.Append(r.Reviewed ? "* [x] @" : "* [ ] @");
               builder.Append(r.Item1.Login);
               builder.Append('\n');
            }

            if (p.ReviewConcerns.Length == 0) {
                builder.Append("\nNo concerns currently listed.\n");
            }
            else {
                builder.Append("\nConcerns(**All Concerns must be marked as resolved**):\n\n");
            }

            foreach (var c in p.ReviewConcerns) {
                if (c.Item2.ResolvedComment != null) {
                    builder.Append("* ~~");
                    builder.Append(c.Item2.Name);
                    builder.Append("~~ resolved by ");
                    AddCommentUrl(builder, issue, c.Item2.ResolvedComment.GetValueOrDefault(0));
                    builder.Append('\n');
                }
                else {
                    builder.Append("* ");
                    builder.Append(c.Item2.Name);
                    builder.Append(" (");
                    AddCommentUrl(builder, issue, c.Item2.InitiatingComment);
                    builder.Append(")\n");
                }
            }

            builder.Append($"\nOnce {p.PrecentOfUsers}% of reviewers approve (Currently thats {p.MinUserAmount} of {p.ReviewRequest.Length}), ");
            builder.Append("this will enter its final comment period. ");
            builder.Append("If you spot a major issue that hasn't been raised ");
            builder.Append("at any point in this process, please speak up!\n");

            return true;
        }

        public void Format()
        {
            var ss = new StringBuilder();

            commentType.Match(
                n => FormatProposed(ss, n.AsT0, issue),
                n => FormatCanceled(ss, n.AsT1.User),
                n => FormatAllReviewsNoConcern(ss, n.AsT2),
                n => FormatTimePeriodPast(ss, n.AsT3)
            );

            body = ss.ToString();
        }

        private bool FormatTimePeriodPast(StringBuilder ss, CommentType.RFCWeekPassed asT3)
        {
            ss.Append("The final comment period, with a disposition to **");
            ss.Append(Enum.GetName(typeof(RFCDisposition), asT3.Disposition));
            ss.Append("**, as per the [review above](");
            AddCommentUrl(ss, issue, asT3.StatusCommandId);
            ss.Append("), is now **complete**.");
            ss.Append(
                "\n\nAs the automated representative of the RFC process, I would like to thank the author for their work and everyone else who contributed."
             );

            switch (asT3.Disposition) {
                case RFCDisposition.Merge:
                ss.Append("\n\nThe RFC will be merged soon.");

                break;
                case RFCDisposition.Close:
                ss.Append("\n\nThe RFC is now closed.");

                break;
                case RFCDisposition.Postpone:
                ss.Append("\n\nThe RFC is now postponed.");
                break;
            }

            // TODO Labels

            return true;
        }

        private bool FormatAllReviewsNoConcern(StringBuilder ss, CommentType.RFCAllReviewedNoConcerns asT2)
        {
            ss.Append(":bell: **This is now entering its final comment period**, ");
            ss.Append("as per the [review above](");
            AddCommentUrl(ss, issue, asT2.StatusCommandId);
            ss.Append("). :bell:");

            return true;
        }

        private bool FormatCanceled(StringBuilder builder, GithubUser user)
        {
            builder.Append($"{user.Login} proposal canceled");
            return true;
        }
    }

    public abstract partial class Command : OneOfBase<
        Command.RFCPropose,
        Command.RFCCancel,
        Command.Reviewed,
        Command.NewConcern,
        Command.ResolveConcern,
        Command.FeedbackRequest>
    {
        public static RFCProposal ExistingProposal(Issue issue)
        {
            using (var db = new RFCContext()) 
                return db.Proposals.Where(x => x.IssueNumber == issue.Number && x.IssueRepository == issue.Repository).FirstOrDefault();
        }

        public static IssueComment PostInsertCommand(GithubUser author, Issue issue, CommentType comment)
        {
            using var db = new RFCContext();

            var c = new BotComment() {
                issue = issue,
                commentType = comment
            };
            c.Format();
            var result = c.Post(null);

            if (result != null) {
                var dbIssue = new Issuecomment();
                result.ToDBIssueComment(dbIssue, issue.Repository);
                db.IssueComments.Add(dbIssue);
                db.SaveChanges();
            }

            return result;
        }

        public bool ProcessRFCPropose(GithubUser author, Issue issue, Issuecomment comment, List<GithubUser> teamMembers, RFCPropose propose)
        {
            using var db = new RFCContext();
            using var db2 = new RFCContext();

            if (ExistingProposal(issue) == null) {
                // we are now doing something new! 
                Console.WriteLine("We are doing a new RFC Proposal");

                var c = new CommentType.RFCProposed() {
                    Disposition = propose.Reason,
                    User = author,
                    ReviewConcerns = Array.Empty<(GithubUser, RFCConcern)>(),
                    ReviewRequest = Array.Empty<(GithubUser, RFCReviewRequest)>()
                };

                var gitComment = PostInsertCommand(author, issue, c);
                if (gitComment == null)
                    return false;

                // we should make this suck less?
                var botComment = new Issuecomment();
                gitComment.ToDBIssueComment(botComment, comment.IssueRepository);

                var proposal = new RFCProposal() {
                    BotTrackingComment = botComment.Id,
                    Closed = false,
                    Disposition = Enum.GetName(typeof(RFCDisposition), propose.Reason),
                    Initiator = author.GithubUserId,
                    InitiatingComment = comment.Id,
                    Start = null,
                    IssueNumber = issue.Number,
                    IssueRepository = issue.Repository
                };

                db.Proposals.Add(proposal);
                db.SaveChanges();

                var reviewRequests = teamMembers.Select(x => new RFCReviewRequest() { Proposal = proposal.Id, Reviewed = x.GithubUserId == issue.User, Reviewer = x.GithubUserId } );
                db2.ReviewRequests.AddRange(reviewRequests);

                db2.SaveChanges();


                var reviewers = reviewRequests.Select(x => (teamMembers.Find(u => u.GithubUserId == x.Reviewer), x));

                c = new CommentType.RFCProposed() {
                    Disposition = propose.Reason,
                    User = author,
                    ReviewConcerns = Array.Empty<(GithubUser, RFCConcern)>(),
                    ReviewRequest = reviewers.ToArray()
                };

                var b = new BotComment() {
                    issue = issue,
                    commentType = c
                };
                b.Format();
                b.Post(gitComment.Id);
            }

            return true;
        }

        public void Process(GithubUser author, Issue issue, Issuecomment comment, List<GithubUser> teamMembers)
        {
            Match(
                RFCPropose => ProcessRFCPropose(author, issue, comment, teamMembers, this as RFCPropose),
                RFCCancel => ProcessRFCCancel(author, issue),
                Reviewed => ProcessReviewed(author,issue),
                NewConcern => ProcessNewConcern(author, issue, comment, this as NewConcern),
                ResolveConcern => ProcessResolveConcern(author, issue, comment, this as ResolveConcern),
                FeedbackRequest => ProcessFeedbackRequest(author, issue, this as FeedbackRequest)
            );
        }

        public static bool FindExsitingFeedback(RFCFeedbackRequest r, int? requestID, Issue issue)
        {
            return r.Requested == requestID && r.IssueNumber == issue.Number && r.IssueRepository == issue.Repository;
        }

        private bool ProcessFeedbackRequest(GithubUser author, Issue issue, FeedbackRequest feedbackRequest)
        {
            using var db = new RFCContext();
            using var db2 = new RFCContext();

            var requestUser = db.Users.Where(u => u.Login == feedbackRequest.User).FirstOrDefault();
            if (requestUser == null) {
                var user = GitClient.GH.Value.GetUser(feedbackRequest.User).Result;
                requestUser = user.ToDBUser(requestUser);
                Github.HandleUser(user);
            }

            var existingFeedbackRequest = db2.FeedbackRequests
                .Where(r => r.Requested == requestUser.GithubUserId
                            && r.IssueNumber == issue.Number 
                            && r.IssueRepository == issue.Repository).FirstOrDefault();

            if (existingFeedbackRequest == null) {
                using var db3 = new RFCContext();

                // we create a new RFC Feedback request
                var newFeedbackRequest = new RFCFeedbackRequest() {
                    Initiator = author.GithubUserId,
                    IssueNumber = issue.Number,
                    IssueRepository = issue.Repository,
                    Requested = requestUser.GithubUserId
                };

                db3.FeedbackRequests.Add(newFeedbackRequest);
                var x = db3.SaveChanges();
                Console.WriteLine($"I have added a new thing {x}");
            }

            return true;
        }

        private bool ProcessResolveConcern(GithubUser author, Issue issue, Issuecomment comment, ResolveConcern resolveConcern)
        {
            using var db = new RFCContext();
            var exitsting = ExistingProposal(issue);
            if (exitsting != null) {
                var existingConcern = db.Concerns.Where(x => x.Proposal == exitsting.Id && x.Initiator == author.GithubUserId && x.Name == resolveConcern.Reason).FirstOrDefault();
                if (existingConcern != null) {
                    existingConcern.ResolvedComment = comment.Id;
                    db.Concerns.Update(existingConcern);
                    db.SaveChanges();
                }
            }
            return true;
        }

        private bool ProcessNewConcern(GithubUser author, Issue issue, Issuecomment comment, NewConcern rFCConcern)
        {
            using var db = new RFCContext();
            var exitsting = ExistingProposal(issue);
            if (exitsting != null) {
                var existingConcern = db.Concerns.Where(x => x.Proposal == exitsting.Id && x.Name == rFCConcern.Reason).FirstOrDefault();
                if (existingConcern == null) {
                    var newConcern = new RFCConcern() {
                        InitiatingComment = comment.Id,
                        Initiator = author.GithubUserId,
                        Name = rFCConcern.Reason,
                        Proposal = exitsting.Id
                    };

                    db.Concerns.Add(newConcern);

                    if (exitsting.Start.HasValue) {
                        exitsting.Start = null;
                        db.Proposals.Update(exitsting);
                    }

                    db.SaveChanges();
                    // TODO: Update Labels
                }
            }
            return true;
        }

        private bool ProcessReviewed(GithubUser author, Issue issue)
        {
            using var db = new RFCContext();

            var exitsting = ExistingProposal(issue);
            if (exitsting != null) {
                var reviewRequest = db.ReviewRequests.Where(x => x.Proposal == exitsting.Id && x.Reviewer == author.GithubUserId).FirstOrDefault();
                if (reviewRequest != null) {
                    reviewRequest.Reviewed = true;
                    db.ReviewRequests.Update(reviewRequest);
                    db.SaveChanges();
                }
            }
            return true;
        }

        public static bool ProcessRFCCancel(GithubUser author, Issue issue)
        {
            using var db = new RFCContext();
            var exitsting = ExistingProposal(issue);
            if (exitsting != null) {
                db.Proposals.Remove(exitsting);

                var c = new CommentType.RFCProposalCancelled() {
                    User = author
                };

                var b = new BotComment() {
                    issue = issue,
                    commentType = c
                };

                b.Format();
                b.Post(null);

                // TODO: Maybe we should make this configurable?
                GitClient.GH.Value.CloseIssue(issue.Repository, issue.Number);

                // TODO: Should remove some labels!
                // Should close PR ? 
            }

            db.SaveChanges();
            return true;
        }
    }

    public static class Nag
    {
        static readonly object _locker = new object();

        public static void UpdateNags(Issuecomment comment, long repo)
        {
            // we do a lock here just in case we end up trying to nag from the git hub worker 
            // callback
            if (Monitor.TryEnter(_locker)) {
                try {

                    using (var db = new RFCContext()) {
                        var issue = db.Issues.Find(comment.IssueNumber, comment.IssueRepository);

                        var author = db.Users.Find(comment.User);
                        var subTeamMembers = SubTeamMembers(issue);
                        var allTeamMembers = AllTeamMembers();

                        var teams = Teams.SETUP.Value;

                        var any = false;

                        var commands = Command.FromStrAll(teams, comment.Body);
                        foreach (var command in commands) {
                            any = true;
                            // we first check and see if this command is from a team member
                            if (!subTeamMembers.Any(x => x.Login == author.Login)) {
                                // we are not a valid team member for this repo and thus we 
                                // cannot do the thing we need to do!
                            }

                            // we are now processing the command!
                            command.Process(author, issue, comment, subTeamMembers);
                        }

                        // TODO: Fix this
                        if (!any) {
                            ResolveApplicableFeedbackRequest(author, issue, comment);
                        }
                        db.SaveChanges();

                        EvaluateNags();
                    }
                }
                finally {
                    Monitor.Exit(_locker);
                }
            }

        }

        private static void EvaluateNags()
        {
            EvaluatePending();
            EvaluateFinalCall();
        }

        private static void EvaluateFinalCall()
        {
            using var db = new RFCContext();

            var timeInThePast = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(30);
            var pendingProposals = db.Proposals.Where(x => x.Start.HasValue && x.Start.Value < timeInThePast && !x.Closed);
            foreach (var p in pendingProposals) {
                using var db2 = new RFCContext();
                using var db3 = new RFCContext();
                using var db4 = new RFCContext();

                var initiatore = db2.Users.Find(p.Initiator);
                var issue = db3.Issues.Find(p.IssueNumber, p.IssueRepository);

                p.Closed = true;
                db4.Proposals.Update(p);
                db4.SaveChanges();

                // TODO Labels

                var c = new CommentType.RFCWeekPassed() {
                    Disposition = (RFCDisposition)Enum.Parse(typeof(RFCDisposition), p.Disposition),
                    User = initiatore,
                    StatusCommandId = p.BotTrackingComment,
                    AddedLabel = false
                };

                var b = new BotComment() {
                    issue = issue,
                    commentType = c
                };
                b.Format();
                b.Post(null);

                var repo = GitClient.GH.Value.GetRepo(p.IssueRepository).Result;
                if (Teams.SETUP.Value.ShouldMerge(repo.FullName)) {
                    // Lets go ahead and merge this in
                    var pr = GitClient.GH.Value.MergePR(p.IssueRepository, p.IssueNumber, "Automated Commit for outstanding RFC", "Automated RFC Commit").Result;
                }
            }
        }

        private static void EvaluatePending()
        {
            using var db = new RFCContext();
            using var db2 = new RFCContext();
            using var db3 = new RFCContext();
            using var db4 = new RFCContext();

            var pendingProposals = db.Proposals.Where(x => !x.Start.HasValue);
            foreach (var p in pendingProposals) {
                var initiatore = db2.Users.Find(p.Initiator);
                var issue = db3.Issues.Find(p.IssueNumber, p.IssueRepository);
                if (!issue.Open) {
                    // Issue is no longer open before it has been processed we should just cancel this now
                    Command.ProcessRFCCancel(initiatore, issue);
                    continue;
                }

                if (!UpdateProposalReviewStatus(p.Id))
                    continue;

                var reviews = ListReviewRequest(p.Id);
                var concerns = ListConcernsWithAuthor(p.Id);
                var repo = GitClient.GH.Value.GetRepo(p.IssueRepository).Result;

                var minPrecent = Teams.SETUP.Value.GetMinPrecent(repo.FullName);
                var validAmount = (int)Math.Ceiling((((float)minPrecent / 100) * reviews.Count()));
                var numOutstandingReviews = reviews.Where(c => !c.Item2.Reviewed).Count();
                var numCompleteReviews = reviews.Count() - numOutstandingReviews;
                var numActiveConcerns = concerns.Where(c => !c.Item2.ResolvedComment.HasValue).Count();

                var c = new CommentType.RFCProposed() {
                    Disposition = (RFCDisposition)Enum.Parse(typeof(RFCDisposition), p.Disposition),
                    User = initiatore,
                    ReviewConcerns = concerns.ToArray(),
                    ReviewRequest = reviews.ToArray(),
                    PrecentOfUsers = minPrecent,
                    MinUserAmount = validAmount
                };

                var previousComment = db4.IssueComments.Find(p.BotTrackingComment);
                var b = new BotComment() {
                    issue = issue,
                    commentType = c
                };
                b.Format();

                // TODO: Check for failure
                if (previousComment.Body != b.body) {
                    var result = b.Post(p.BotTrackingComment);
                    previousComment.Body = result.Body;
                    db4.Update(previousComment);
                    db4.SaveChanges();
                }

                // TODO make this configurable per repo
                var majorityComplete = numCompleteReviews >= validAmount;
                if (numActiveConcerns == 0 && majorityComplete) {
                    using var db5 = new RFCContext();

                    p.Start = DateTimeOffset.UtcNow;
                    db5.Proposals.Update(p);
                    db5.SaveChanges();


                    // TODO: Labels
                    var f = new CommentType.RFCAllReviewedNoConcerns() {
                        AddedLabel = false,
                        User = initiatore,
                        StatusCommandId = p.BotTrackingComment,
                    };

                    var x = new BotComment() {
                        issue = issue,
                        commentType = f
                    };
                    x.Format();
                    x.Post(null);
                }
            }
        }

        private static IEnumerable<(GithubUser, RFCConcern)> ListConcernsWithAuthor(int id)
        {
            using var db = new RFCContext();
            using var db2 = new RFCContext();

            List<(GithubUser, RFCConcern)> ps = new List<(GithubUser, RFCConcern)>();

            var concerns = db.Concerns.Where(x => x.Proposal == id).OrderBy(x => x.Name);
            foreach (var c in concerns) {
                var initUser = db2.Users.Find(c.Initiator);
                ps.Add((initUser, c));
            }

            return ps;
        }

        private static IEnumerable<(GithubUser, RFCReviewRequest)> ListReviewRequest(int id)
        {
            using var db = new RFCContext();
            using var db2 = new RFCContext();

            List<(GithubUser, RFCReviewRequest)> ps = new List<(GithubUser, RFCReviewRequest)>();
            var reviews = db.ReviewRequests.Where(r => r.Proposal == id);
            foreach (var r in reviews) {
                var initUser = db2.Users.Find(r.Reviewer);

                ps.Add((initUser, r));
            }

            ps.Sort((emp1, emp2) => emp1.Item1.Login.CompareTo(emp2.Item1.Login));

            return ps;
        }

        private static bool UpdateProposalReviewStatus(int id)
        {
            using var db = new RFCContext();
            using var db2 = new RFCContext();
            using var db3 = new RFCContext();
            using var db4 = new RFCContext();

            var proposal = db.Proposals.Find(id);
            if (proposal.Start.HasValue || proposal.Closed)
                return true;

            var comment = db2.IssueComments.Find(proposal.BotTrackingComment);
            foreach (string user in ParseTickyBoxes("proposal", proposal.Id, comment)) {
                var u = db3.Users.Where(x => x.Login == user).FirstOrDefault();
                if (u != null) {
                    var reviewRequest = db4.ReviewRequests.Where(r => r.Proposal == proposal.Id && r.Reviewer == u.GithubUserId).FirstOrDefault();
                    if (reviewRequest != null) {
                        reviewRequest.Reviewed = true;
                        db4.ReviewRequests.Update(reviewRequest);
                        db4.SaveChanges();
                    }
                }
            }
            return true;
        }

        private static string ParseTickyUserName(string s)
        {
            if (s.StartsWith("* [")) {
                var l = s.TrimStart("* [");
                var reviewed = l.StartsWith('x');
                var remaining = l.TrimStart("x] @").TrimStart(" ] @");

                var tokens = new Queue<string>(remaining.Split());
                var username = tokens.Dequeue();
                if (username != null) {
                    if (reviewed) {
                        return username;
                    }

                    return null;
                }
                else {
                    return null;
                }
            }

            return null;
        }

        private static IEnumerable<string> ParseTickyBoxes(string v, int id, Issuecomment comment)
        {
            return comment.Body
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseTickyUserName)
                .Where(x => !string.IsNullOrEmpty(x));
        }

        private static void ResolveApplicableFeedbackRequest(GithubUser author, Issue issue, Issuecomment comment)
        {
            using var db = new RFCContext();

            if (issue.ClosedAt.HasValue)
                return;

            var existingFeedbackRequest = db.FeedbackRequests
                 .Where(r => r.Requested == author.GithubUserId
                            && r.IssueNumber == issue.Number
                            && r.IssueRepository == issue.Repository).FirstOrDefault();

            if (existingFeedbackRequest != null) {
                using var db2 = new RFCContext();
                existingFeedbackRequest.FeedbackComment = comment.Id;
                db2.Update(existingFeedbackRequest);
                db2.SaveChanges();
            }
        }

        public static List<GithubUser> AllTeamMembers() => SpecificSubTeamMembers(_ => true);

        public static List<GithubUser> ResolveLoginsToUser(IEnumerable<string> members)
        {
            using (var db = new RFCContext()) {
                return db.Users.Where(x => members.Contains(x.Login)).OrderBy(x => x.Login).ToList();
            }
        }

        public static List<GithubUser> SpecificSubTeamMembers(Func<string, bool> include)
        {
            var teams = Teams.SETUP.Value.GetTeams();
            var members = teams.Where(x => include(x.Key)).SelectMany(y => y.Value.members);

            return ResolveLoginsToUser(members);
        }

        public static List<GithubUser> SubTeamMembers(Issue issue)
        {
            return SpecificSubTeamMembers(x => issue.Labels.Contains(x));
        }
    }
}
