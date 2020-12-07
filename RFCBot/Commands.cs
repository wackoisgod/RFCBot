using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OneOf;

namespace RFCBot
{

    public static class StringExt
    {
        public static string TrimStart(this string target, string trimString)
        {
            if (string.IsNullOrEmpty(trimString))
                return target;

            string result = target;
            while (result.StartsWith(trimString)) {
                result = result.Substring(trimString.Length);
            }

            return result;
        }

        public static string TrimEnd(this string target, string trimString)
        {
            if (string.IsNullOrEmpty(trimString))
                return target;

            string result = target;
            while (result.EndsWith(trimString)) {
                result = result.Substring(0, result.Length - trimString.Length);
            }

            return result;
        }
    }

    public enum RFCDisposition
    {
        Merge,
        Close,
        Postpone,
    }

    public abstract partial class Command : OneOfBase<
        Command.RFCPropose, 
        Command.RFCCancel, 
        Command.Reviewed, 
        Command.NewConcern, 
        Command.ResolveConcern,
        Command.FeedbackRequest>
    {
        public class RFCPropose : Command { public RFCDisposition Reason { get; set; } }
        public class RFCCancel : Command { }
        public class Reviewed : Command { }
        public class NewConcern : Command { public string Reason { get; set; } }
        public class ResolveConcern : Command { public string Reason { get; set; } }
        public class FeedbackRequest : Command { public string User { get; set; } }
        // Start Poll but this is not supported yet

        public static string ParseCommandText(string command, string subCommand)
        {
            var nameStart = command.IndexOf(subCommand) + subCommand.Length;

            return command.Substring(nameStart).Trim();
        }

        public static Command ParseRFCSubCommand(RFCTeamConfig config, string command, string subcommand, bool context)
        {
            return subcommand switch
            {
                var x when 
                    x == "merge" ||
                    x == "merged" ||
                    x == "merging" ||
                    x == "merges" => new RFCPropose() { Reason = RFCDisposition.Merge },
                var x when
                    x == "close" ||
                    x == "closed" ||
                    x == "closing" ||
                    x == "closes" => new RFCPropose() { Reason = RFCDisposition.Close },
                var x when
                    x == "cancel" ||
                    x == "canceled" ||
                    x == "canceling" ||
                    x == "cancels" => new RFCCancel(),
                var x when
                    x == "reviewed" ||
                    x == "review" ||
                    x == "reviewing" ||
                    x == "reviews" => new Reviewed(),
                var x when
                    x == "concern" ||
                    x == "concerned" ||
                    x == "concerning" ||
                    x == "concerns" => new NewConcern() { Reason = ParseCommandText(command, subcommand) },
                var x when
                    x == "resolved" ||
                    x == "resolving" ||
                    x == "resolves" => new ResolveConcern() { Reason = ParseCommandText(command, subcommand) },
                _ => null
            };
        }

        public static Command ParseFeedbackRequest(string user)
        {
            if (!string.IsNullOrEmpty(user)) return new FeedbackRequest() { User = user.TrimStart("@") };
            return null;
        }

        public static List<Command> FromStrAll(RFCTeamConfig config, string command)
        {
            return command
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => x.Contains("@rfcbot"))
                .Select(y => FromInvocationLine(config, y))
                .Where(z => z != null).ToList();
        }

        public static Command FromInvocationLine(RFCTeamConfig config, string command)
        {
            var strings = command
                .TrimStart("@rfcbot")
                .Trim()
                .TrimEnd(":")
                .Trim()
                .Split();

            var tokens = new Queue<string>(strings);
            var invocations = tokens.Dequeue();

            return invocations switch
            {
                var x when
                    x == "pr" ||
                    x == "rfc"  => ParseRFCSubCommand(config, command, tokens.Dequeue(), true),
                "f?"            => ParseFeedbackRequest(tokens.Dequeue()),
                _               => ParseRFCSubCommand(config, command, invocations, false)
            };
        }
    }
}
