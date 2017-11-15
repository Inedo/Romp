using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Inedo.Romp
{
    internal sealed class ArgList
    {
        private static readonly Regex OptionRegex = new Regex("^-{1,2}(?<1>[^=]+)(=(?<2>.*))?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        private string[] args;

        public ArgList(string[] args) => this.args = args;

        public string PopCommand()
        {
            if (this.args.Length == 0 || this.args[0].Length == 0 || this.args[0][0] == '-')
                return null;

            var command = this.args[0];
            var arr = new string[this.args.Length - 1];
            Array.Copy(this.args, 1, arr, 0, arr.Length);
            this.args = arr;
            return command;
        }
        public void ProcessOptions(Func<ArgOption, bool> parse)
        {
            var results = new List<string>(this.args.Length);

            foreach (var arg in this.args)
            {
                var match = OptionRegex.Match(arg);
                if (match.Success)
                {
                    var option = new ArgOption(match.Groups[1].Value, match.Groups[2].Value);
                    if (!parse(option))
                        results.Add(arg);
                }
                else
                {
                    results.Add(arg);
                }
            }

            if (results.Count != this.args.Length)
                this.args = results.ToArray();
        }
        public void ThrowIfAnyRemaining()
        {
            var option = this.args.FirstOrDefault();
            if (option != null)
                throw new RompException("Invalid argument: " + option);
        }
    }
}
