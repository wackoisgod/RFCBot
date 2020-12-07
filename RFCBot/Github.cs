using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Internal;

using Octokit;
using Octokit.Internal;

namespace RFCBot
{
    internal static class LinqExtensionMethods
    {
        /// <summary>
        /// Variation on FirstOrDefault with an alternate default value
        /// </summary>
        public static T FirstOr<T>(this IEnumerable<T> source, T @else) =>
            source
                .DefaultIfEmpty(@else)
                .FirstOrDefault();
    }

    public class HackHttpClient : IHttpClient
    {
        HttpClientAdapter _client;

        public HackHttpClient(Func<HttpMessageHandler> getHandler)
        {
            _client = new HttpClientAdapter(getHandler);
        }

        public void Dispose() { _client.Dispose(); }

        public Task<IResponse> Send(IRequest request, CancellationToken cancellationToken)
        {
            request.Headers.Add("Time-Zone", "UTC");
            return _client.Send(request, cancellationToken);
        }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Add("Time-Zone", "UTC");
            return _client.SendAsync(request, cancellationToken);
        }

        public void SetRequestTimeout(TimeSpan timeout) => _client.SetRequestTimeout(timeout);
    }

    public static class OctokitExt
    {
        public static Issue ToDBIssue(this Octokit.Issue issue, Issue inIssue, long repo)
        {
            inIssue.Assignee = issue.Assignee?.Id;
            inIssue.Body = issue.Body;
            inIssue.ClosedAt = issue.ClosedAt;
            inIssue.CreatedAt = issue.CreatedAt;
            inIssue.IsPullRequest = issue.PullRequest != null;
            inIssue.Labels = issue.Labels.Select(x => x.Name).ToList();
            inIssue.Locked = issue.Locked;
            inIssue.Number = issue.Number;
            inIssue.Open = issue.State.Value == ItemState.Open;
            inIssue.Title = issue.Title;
            inIssue.UpdatedAt = issue.UpdatedAt;
            inIssue.User = (int)(issue.User?.Id);
            inIssue.Repository = repo;

            return inIssue;
        }

        public static Issuecomment ToDBIssueComment(this Octokit.IssueComment issue, Issuecomment inIssue, long repo)
        {
            inIssue.Id = issue.Id;
            inIssue.Body = issue.Body;
            inIssue.CreatedAt = issue.CreatedAt;
            inIssue.UpdatedAt = issue.UpdatedAt;
            inIssue.User = (int)(issue.User?.Id);
            inIssue.IssueRepository = repo;
            inIssue.IssueNumber = int.Parse(new Uri(issue.HtmlUrl).Segments.Last());
            return inIssue;
        }

        public static PullRequest ToDBPullRequest(this Octokit.PullRequest pr, PullRequest inPR, long repo)
        {
            inPR.Id = pr.Id;
            inPR.Title = pr.Title;
            inPR.CreatedAt = pr.CreatedAt;
            inPR.UpdatedAt = pr.UpdatedAt;
            inPR.MergedAt = pr.MergedAt;
            inPR.ClosedAt = pr.ClosedAt;
            if (pr.Assignee != null) {
                inPR.Assignee = pr.Assignee.Id;
            }
            inPR.Repository = repo;
            inPR.Locked = pr.Locked;
            inPR.Number = pr.Number;
            inPR.State = pr.State.StringValue;
            inPR.Additions = pr.Additions;
            inPR.Deletions = pr.Deletions;
            inPR.Body = pr.Body;
            inPR.ChangedFiles = pr.ChangedFiles;

            return inPR;
        }

        public static GithubUser ToDBUser(this Octokit.User pr, GithubUser inPR)
        {
            inPR = new GithubUser();
            inPR.GithubUserId = pr.Id;
            inPR.Login = pr.Name;

            return inPR;
        }
    }

    public struct Repo
    {
        public string org;
        public Repository repo;
    }

    public class GitClient
    {
        public GitHubClient client;

        public static Lazy<GitClient> GH = new Lazy<GitClient>(() => new GitClient());

        public GitClient()
        {
            if (Config.CONFIG.Value.githubURL != null) {
                var ghe = new Uri(Config.CONFIG.Value.githubURL);
                client = new GitHubClient(new Connection(
                    new ProductHeaderValue(Config.CONFIG.Value.githubUserAgent),
                    ghe,
                    new InMemoryCredentialStore(Credentials.Anonymous),
                    new HackHttpClient(HttpMessageHandlerFactory.CreateDefault),
                    new SimpleJsonSerializer()));
            }
            else {
                client = new GitHubClient(new Connection(
                    new ProductHeaderValue(Config.CONFIG.Value.githubUserAgent),
                    new HackHttpClient(HttpMessageHandlerFactory.CreateDefault)));
            }

            // We need to get something from the config for the github key
            if (!string.IsNullOrEmpty(Config.CONFIG.Value.githubAccessToken)) {
                client.Credentials = new Credentials(Config.CONFIG.Value.githubAccessToken);
            }
        }

        public async Task<List<Repo>> GetOrgRepos(string org)
        {
            List<Repo> orgs = new List<Repo>();
            var results = await client.Repository.GetAllForOrg(org);

            foreach (var o in results) {
                orgs.Add(new Repo() { org = org, repo = o });
            }

            return orgs;
        }

        public async Task<Repository> GetRepo(long repo)
        {
            var results = await client.Repository.Get(repo);
            return results;
        }

        public async Task<Repository> GetRepoFromFullPath(string repo)
        {
            var parts = repo.Split("/");
            if (parts.Length == 2) {
                var results = await client.Repository.Get(parts[0], parts[1]);
                return results;
            }
            return null;
        }

        public async Task<List<Octokit.Issue>> IssuesSince(long repo, DateTimeOffset start)
        {
            List<Octokit.Issue> issues = new List<Octokit.Issue>();

            var shouldPrioritize = new RepositoryIssueRequest {
                Filter = IssueFilter.All,
                State = ItemStateFilter.All,
                Since = start
            };

            var results = await client.Issue.GetAllForRepository(repo, shouldPrioritize);

            return results.ToList();
        }

        public async Task<List<IssueComment>> CommentsSince(long repo, DateTimeOffset start)
        {
            List<IssueComment> issues = new List<IssueComment>();

            var shouldPrioritize = new IssueCommentRequest {
                Sort  = IssueCommentSort.Created,
                Since = start
            };

            var results = await client.Issue.Comment.GetAllForRepository(repo, shouldPrioritize);

            return results.ToList();
        }

        public async Task<Octokit.PullRequest> FetchPullRequests(long repo, int number)
        {
            return await client.PullRequest.Get(repo, number);
        }

        public void CloseIssue(long repo, int issueNum)
        {
            client.Issue.Update(repo, issueNum, new IssueUpdate() { State = ItemState.Closed }).Wait();
        }

        public void AddIssueLabel(long repo, int issueNum, string label)
        {
            var isseuUpdate = new IssueUpdate();
            isseuUpdate.AddLabel(label);

            client.Issue.Update(repo, issueNum, isseuUpdate).Wait();
        }

        public void RemoveIssueLabel(long repo, int issueNum, string label)
        {
            var isseuUpdate = new IssueUpdate();
            isseuUpdate.RemoveLabel(label);

            client.Issue.Update(repo, issueNum, isseuUpdate).Wait();
        }

        public async Task<PullRequestMerge> MergePR(long repo, int issueNum, string commitMessage, string? commitTitle)
        {
            var request = new MergePullRequest() {
                CommitMessage = commitMessage,
                CommitTitle = commitTitle,
                MergeMethod = PullRequestMergeMethod.Merge
            };

            return await client.PullRequest.Merge(repo, issueNum, request);
        }

        public async Task<IssueComment> AddIssueComment(long repo, int issueNum, string comment)
        {
            return await client.Issue.Comment.Create(repo, issueNum, comment);
        }

        public async Task<IssueComment> EditIssueComment(long repo, int commentNum, string comment)
        {
            return await client.Issue.Comment.Update(repo, commentNum, comment);
        }

        public async Task<User> GetUser(string name)
        {
            return await client.User.Get(name);
        }
    }

    public class Github
    {
        public static DateTimeOffset MostRecentUpdate()
        {
            using (var db = new RFCContext()) {
                var result = db.GithubSync.Where(x => x.Successful).OrderByDescending(x => x.RanAt).Select(v => v.RanAt).FirstOr(new DateTime(2015, 5, 15));
                return result;
            }
        }

        public static void RecordSuccessfulUpdate(DateTimeOffset ingetStart)
        {
            using (var db = new RFCContext()) {
                db.GithubSync.Update(new Githubsync() { Successful = true, RanAt = ingetStart });
                db.SaveChanges();
            }
        }

        public static void IngestSince(long repo, DateTimeOffset start)
        {
            Console.WriteLine($"fetching all {repo} issues and comments since {start}");
            var issues = GitClient.GH.Value.IssuesSince(repo, start).Result;
            var comments = GitClient.GH.Value.CommentsSince(repo, start).Result;

            int GetPRNumber(string url)
            {
                return int.Parse(new Uri(url).Segments.Last());
            }

            var prs = new List<Octokit.PullRequest>();
            foreach (var i in issues) {
                if (i.PullRequest != null) {
                    var pr = GitClient.GH.Value.FetchPullRequests(repo, GetPRNumber(i.PullRequest.Url)).Result;
                    prs.Add(pr);
                }
            }

            Console.WriteLine($"num pull requests updated since {start}: {prs.Count}");
            Console.WriteLine($"num comments updated updated since {start}: {comments.Count}");

            foreach (var i in issues) {
                HandleIssue(i, repo);
            }

            foreach (var c in comments) {
                HandleComment(c, repo);
            }

            foreach (var p in prs) {
                HandlePr(p, repo);
            }
        }

        public static void HandleIssue(Octokit.Issue issue, long repo)
        {
            HandleUser(issue.User);

            bool IsSame(Issue i)
            {
                return i.Number == issue.Number && i.Repository == repo;
            } 

            using (var db = new RFCContext()) {
                var existing = db.Issues.Where(IsSame).FirstOrDefault();
                if (existing != null) {
                    db.Entry(existing).CurrentValues.SetValues(issue.ToDBIssue(existing, repo));
                }
                else {
                    existing = new Issue();
                    issue.ToDBIssue(existing, repo);
                    db.Issues.Add(existing);
                }

                db.SaveChanges();
            }
        }

        public static void HandleUser(User user)
        {
            if (user == null)
                return;

            using (var db = new RFCContext()) {
                var existing = db.Users.Where(u => u.GithubUserId == user.Id).FirstOrDefault();
                if (existing != null) {
                    db.Entry(existing).CurrentValues.SetValues(user.Id);
                }
                else {
                    db.Users.Add(new GithubUser() { GithubUserId = user.Id, Login = user.Login });
                }

                var xx = db.SaveChanges();
                Console.WriteLine(xx);
            }
        }

        public static void HandleComment(IssueComment comment, long repo)
        {
            HandleUser(comment.User);
            Console.WriteLine(comment.HtmlUrl);
            using (var db = new RFCContext()) {
                var existing = db.IssueComments.Where(c => c.Id == comment.Id).FirstOrDefault();
                if (existing != null) {
                    db.Entry(existing).CurrentValues.SetValues(comment.ToDBIssueComment(existing, repo));
                    db.SaveChanges();
                }
                else {
                    existing = new Issuecomment();
                    comment.ToDBIssueComment(existing, repo);
                    db.IssueComments.Add(existing);
                    db.SaveChanges();
                    Nag.UpdateNags(existing, repo);
                }
            }
        }

        public static void HandlePr(Octokit.PullRequest pr, long repo)
        {
            if (pr.Assignee != null) {
                HandleUser(pr.Assignee);
            }

            using (var db = new RFCContext()) {
                var existing = db.PullRequests.Where(c => c.Id == pr.Id).FirstOrDefault();
                if (existing != null) {
                    db.Entry(existing).CurrentValues.SetValues(pr.ToDBPullRequest(existing, repo));
                    
                    // we need to check and see if this has been merged and if so then we need to rename the file to the proper magic number? 
                    // essentially look of for the rfc file and rename it ? 
                    if (pr.Merged) {
                        Console.WriteLine("We have merged a PR we care about?");
                        
                        
                        var rc = GitClient.GH.Value.client.Repository.Branch.Get(repo, "main").Result;
                        var commit = GitClient.GH.Value.client.Git.Commit.Get(repo, rc.Commit.Sha).Result;
                        var rootTree = GitClient.GH.Value.client.Git.Tree.GetRecursive(repo, commit.Sha).Result;
                        var textsTree = rootTree.Tree.Where(x => x.Path == "texts").SingleOrDefault();
                        var allCurrentRFCs = GitClient.GH.Value.client.Git.Tree.Get(repo, textsTree.Sha).Result;
                        var newTextsTree = new NewTree();
                        allCurrentRFCs.Tree
                            .ToList().ForEach(x => newTextsTree.Tree.Add(new NewTreeItem
                            {
                                Mode = x.Mode,
                                Path = x.Path,
                                Sha = x.Sha,
                                Type = x.Type.Value
                            }));

                        var newRFCs = allCurrentRFCs.Tree.Where(x => x.Path.Contains("0000-")).ToList();

                        if (newRFCs.Count() != 0)
                        {
                            Console.WriteLine($"We have merged {newRFCs.Count()} RFC/RFAs");
                            string fmt = "0000.##";

                            foreach (var item in newRFCs)
                            {
                                var nextValue = db.GetNextSequenceValue("rfcnext");

                                var newPath = item.Path;
                                newTextsTree.Tree.Remove(newTextsTree.Tree.Where(x => x.Path == item.Path).First());
                                newTextsTree.Tree.Add(new NewTreeItem
                                {
                                    Path = newPath.Replace("0000-", $"{nextValue.ToString(fmt)}-"),
                                    Mode = "100644",
                                    Type = TreeType.Blob,
                                    Sha = item.Sha
                                });
                                ;
                            }
                        }

                        var newTextsTreeStatePhase =
                            GitClient.GH.Value.client.Git.Tree.Create(repo, newTextsTree).Result;

                        var newRoot = new NewTree();

                        rootTree.Tree
                            .Where(x => !x.Path.Contains("texts/"))
                            .ToList().ForEach(x => newRoot.Tree.Add(new NewTreeItem
                            {
                                Mode = x.Mode,
                                Path = x.Path,
                                Sha = x.Path == "texts" ? newTextsTreeStatePhase.Sha : x.Sha,
                                Type = x.Type.Value
                            }));

                        var newTextsTreeStatePhaseTwo =
                            GitClient.GH.Value.client.Git.Tree.Create(repo, newRoot).Result;

                        var newCommit = new NewCommit("Updating RFC Numbers", newTextsTreeStatePhaseTwo.Sha,
                            commit.Sha);
                        var latestCommit = GitClient.GH.Value.client.Git.Commit.Create(repo, newCommit).Result;
                        var result = GitClient.GH.Value.client.Git.Reference.Update(repo, $"heads/{"main"}",
                            new ReferenceUpdate(latestCommit.Sha)).Result;

                        Console.WriteLine("Updated all new RFCs to new values");
                    }
                }
                else {
                    // this is a new PR should we auto create a RFC Merge?
                    existing = new PullRequest();
                    pr.ToDBPullRequest(existing, repo);
                    db.PullRequests.Add(existing);

                    // we should label? 
                    var r = GitClient.GH.Value.GetRepo(repo).Result;
                    var labels = Teams.SETUP.Value.DefaultIssueLabels(r.FullName);
                    foreach (var l in labels) {
                        GitClient.GH.Value.AddIssueLabel(repo, pr.Number, l);
                        Thread.Sleep(TimeSpan.FromMilliseconds(1390));
                    }

                    if (Teams.SETUP.Value.ShouldAutoCreateMerge(r.FullName)) {
                        _ = GitClient.GH.Value.AddIssueComment(repo, pr.Number, "@rfcbot merge").Result;
                    }
                }

                db.SaveChanges();
            }
        }
    }
}
