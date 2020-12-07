using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

using Octokit;
using Octokit.Internal;

namespace RFCBot
{
    public static class HttpRequestExtension
    {
        /// <summary>
        /// Peek at the Http request stream without consuming it
        /// </summary>
        /// <param name="request">Http Request object</param>
        /// <returns>string representation of the request body</returns>
        public static string PeekBody(this HttpRequest request)
        {
            var bodyStr = "";

            try {
                request.EnableBuffering();

                using (StreamReader reader
                  = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true)) {
                    bodyStr = reader.ReadToEndAsync().Result;
                }

            }
            finally {
                request.Body.Position = 0;
            }

            return bodyStr;
        }
    }

    public struct GitEvent
    {
        public string eventName;
        public string devliveryId;
        public ActivityPayload Payload;
    }

    public class Startup
    {

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRouting();
        }

        private static string HashEncode(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        private bool IsValidSignature(string githubSignature, MemoryStream body, string secret)
        {
            try {
                using HMACSHA1 hmac = new HMACSHA1(Encoding.UTF8.GetBytes(secret));
                body.Seek(0, SeekOrigin.Begin);
                var hash = hmac.ComputeHash(body);
                var hashString = HashEncode(hash);
                return githubSignature.Equals($"sha1={hashString}", StringComparison.OrdinalIgnoreCase);
            }catch (Exception) {
                Console.WriteLine($"Failed to validate from excpetion {githubSignature} ");
                return false;
            }
        }

        public static ActivityPayload FromJSONEvent(string @event, string json) =>
            @event switch
            {
                "issue_comment" => new Octokit.Internal.SimpleJsonSerializer().Deserialize<IssueCommentPayload>(json),
                "issue" => new Octokit.Internal.SimpleJsonSerializer().Deserialize<IssueEventPayload>(json),
                "pull_request" => new Octokit.Internal.SimpleJsonSerializer().Deserialize<PullRequestEventPayload>(json),
                _ => null
            };

        public void Configure(IApplicationBuilder app)
        {
            #region snippet_RouteHandler
            var trackPackageRouteHandler = new RouteHandler(context =>
            {
                var routeValues = context.GetRouteData().Values;
                return context.Response.WriteAsync(
                    $"Hello! Route values: {string.Join(", ", routeValues)}");
            });

            var routeBuilder = new RouteBuilder(app, trackPackageRouteHandler);

            routeBuilder.MapPost("github-webhook",  context =>
            {
                var content = context.Request.PeekBody();
                using var body = new MemoryStream();
                context.Request.Body.CopyTo(body);

                var signature = context.Request.Headers["X-Hub-Signature"];
                var eventName = context.Request.Headers["X-Github-Event"];
                var devliveryId = context.Request.Headers["X-Github-Delivery"];
                Console.WriteLine($"Got event {signature} {eventName} {devliveryId}");

                foreach (var s in Config.CONFIG.Value.githubWebHookSecrets) {
                    if (IsValidSignature(signature, body, s)) {
                        try {
                            var payload = FromJSONEvent(eventName, content);
                            ProcessPayload(new GitEvent() { devliveryId = devliveryId, eventName = eventName, Payload = payload });
                        }
                        catch (Exception) {
                            Console.WriteLine($"Failed to parse {eventName} {content}");
                        }
                    } else {
                        Console.WriteLine($"Failed to validate {signature} {eventName} {devliveryId}");
                    }
                }

                return context.Response.WriteAsync($"");
            });

            var routes = routeBuilder.Build();
            app.UseRouter(routes);
            #endregion

            #region snippet_Dictionary
            app.Run(async (context) =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync("hi");
            });
            #endregion

        }

        private void ProcessPayload(GitEvent gitEvent)
        {
            Console.WriteLine($"Received valid webhook ({gitEvent.eventName} id {gitEvent.devliveryId})");

            switch (gitEvent.eventName) {
                case "issue": 
                    Github.HandleIssue((gitEvent.Payload as IssueEventPayload).Issue, gitEvent.Payload.Repository.Id);
                break;
                case "issue_comment": {
                    if (gitEvent.Payload is IssueCommentPayload c) {
                        if (c.Action != "deleted") {
                            Github.HandleIssue(c.Issue, gitEvent.Payload.Repository.Id);
                            Github.HandleComment(c.Comment, gitEvent.Payload.Repository.Id);
                        }
                    }
                }
                break;
                case "pull_request":
                    if (gitEvent.Payload is PullRequestEventPayload y) {
                        Github.HandlePr(y.PullRequest, gitEvent.Payload.Repository.Id);
                    }
                break;
                default:
                    Console.WriteLine("Unsupported event");
                break;
            }
        }
    }

    class Program
    {
        private static void GitHubSync(object state)
        {
            var recent = Github.MostRecentUpdate();
            var repos = new List<Repository>();

            // we need to gather all the repos from the teams config? 
            var teamRepos = Teams.SETUP.Value.behaviors.Select(x => x.Key);

            foreach (var o in teamRepos) {
                repos.Add(GitClient.GH.Value.GetRepoFromFullPath(o).Result);
            }

            var startTime = DateTimeOffset.UtcNow;
            foreach (var r in repos) {
                Github.IngestSince(r.Id, recent);
            }

            Github.RecordSuccessfulUpdate(startTime);
        }

        private static Timer gitHubTimer;
        static void Main(string[] args)
        {
            Teams.Start();
            gitHubTimer = new Timer(new TimerCallback(GitHubSync), null, 0,
                 Config.CONFIG.Value.githubIntervalMins * 60000);
            
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseStartup<Startup>()
                .Build();
            
            
            host.Run();
        }
    }
}
