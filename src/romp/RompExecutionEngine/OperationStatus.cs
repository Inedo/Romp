using System;
using System.Collections.Generic;
using System.Linq;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility.Operations;

namespace Inedo.Romp.RompExecutionEngine
{
    [Serializable]
    public sealed class OperationStatus
    {
        public OperationStatus(string shortDescription, string longDescription, int? percentComplete)
            : this(shortDescription, longDescription, percentComplete, Enumerable.Empty<OperationStatus>(), default)
        {
        }
        public OperationStatus(string shortDescription, string longDescription, int? percentComplete, IEnumerable<OperationStatus> backgroundOperations)
            : this(shortDescription, longDescription, percentComplete, backgroundOperations, default)
        {
        }
        public OperationStatus(string shortDescription, string longDescription, int? percentComplete, IEnumerable<OperationStatus> backgroundOperations, StatementProgress progress)
        {
            this.ShortDescription = shortDescription;
            this.LongDescription = longDescription;
            this.PercentComplete = percentComplete;
            this.BackgroundOperations = Array.AsReadOnly(backgroundOperations.ToArray());
            this.StatementMessage = progress.Message;
            this.StatementPercentComplete = progress.Percent;
        }

        public string ShortDescription { get; }
        public string LongDescription { get; }
        public int? PercentComplete { get; }
        public IReadOnlyList<OperationStatus> BackgroundOperations { get; }
        public string StatementMessage { get; }
        public int? StatementPercentComplete { get; }

        public static OperationStatus GetOperationStatusHtml(ExecuterThreadStatus s)
        {
            var desc = GetStatementDescription(s.CurrentStatement);
            return new OperationStatus(
                desc?.ShortDescription?.ToHtml(),
                desc?.LongDescription?.ToHtml(),
                s.PercentComplete,
                s.BackgroundThreads.Select(GetOperationStatusHtml),
                s.CurrentStatementProgress
            );
        }
        public static OperationStatus GetOperationStatusText(ExecuterThreadStatus s)
        {
            var desc = GetStatementDescription(s.CurrentStatement);
            return new OperationStatus(
                desc?.ShortDescription?.ToString(),
                desc?.LongDescription?.ToString(),
                s.PercentComplete,
                s.BackgroundThreads.Select(GetOperationStatusText),
                s.CurrentStatementProgress
            );
        }

        private static ExtendedRichDescription GetStatementDescription(Statement s)
        {
            switch (s)
            {
                case ActionStatement a:
                    return GetStatementDescription(a);
                case AssignVariableStatement a:
                    return GetStatementDescription(a);
                case AwaitStatement a:
                    return GetStatementDescription(a);
                case ExecutionDirectiveStatement e:
                    return GetStatementDescription(e);
                case IterationBlockStatement i:
                    return GetStatementDescription(i);
                case LogStatement l:
                    return GetStatementDescription(l);
                case PredicateStatement p:
                    return GetStatementDescription(p);
                case SetContextStatement c:
                    return GetStatementDescription(c);
                default:
                    return null;
            }
        }
        private static ExtendedRichDescription GetStatementDescription(ActionStatement statement)
        {
            try
            {
                var operationType = ExtensionsManager.TryGetOperation(statement.ActionName.Namespace, statement.ActionName.Name);
                return Operation.GetDescription(operationType, statement);
            }
            catch
            {
                return new ExtendedRichDescription(
                    new RichDescription(statement.ActionName.ToString())
                );
            }
        }
        private static ExtendedRichDescription GetStatementDescription(AssignVariableStatement statement)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Assigning ",
                    new Hilite(statement.TargetExpression),
                    " to ",
                    new Hilite(statement.VariableValue)
                )
            );
        }
        private static ExtendedRichDescription GetStatementDescription(AwaitStatement statement)
        {
            if (string.IsNullOrEmpty(statement.Token))
                return new ExtendedRichDescription(new RichDescription("Waiting for background operations"));
            else
                return new ExtendedRichDescription(new RichDescription("Waiting for background operations"), new RichDescription("for token ", new Hilite(statement.Token)));
        }
        private static ExtendedRichDescription GetStatementDescription(ContextIterationStatement statement)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Iterating ",
                    new Hilite(statement.ContextType),
                    " contexts over ",
                    new Hilite(statement.Source)
                )
            );
        }
        private static ExtendedRichDescription GetStatementDescription(ExecutionDirectiveStatement statement)
        {
            if (!statement.Exclusive || string.IsNullOrEmpty(statement.ExclusiveToken))
                return null;
            return new ExtendedRichDescription(
                new RichDescription(
                    "Waiting for exclusive token ",
                    new Hilite(statement.ExclusiveToken.TrimStart('!'))
                ),
                new RichDescription(
                    statement.ExclusiveToken.StartsWith("!") ? "across all executions" : "in this execution"
                )
            );
        }
        private static ExtendedRichDescription GetStatementDescription(IterationBlockStatement statement)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Iterating over ",
                    new Hilite(statement.Source)
                )
            );
        }
        private static ExtendedRichDescription GetStatementDescription(LogStatement statement)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Log ",
                    new Hilite(statement.Text)
                ),
                new RichDescription(
                    "as ",
                    new Hilite(statement.Level.ToString())
                )
            );
        }
        private static ExtendedRichDescription GetStatementDescription(PredicateStatement statement)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "If ",
                    new Hilite(statement.Predicate.ToString())
                )
            );
        }
        private static ExtendedRichDescription GetStatementDescription(SetContextStatement statement)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Setting ",
                    new Hilite(statement.ContextType),
                    " context to ",
                    new Hilite(statement.ContextValue)
                )
            );
        }
    }
}
