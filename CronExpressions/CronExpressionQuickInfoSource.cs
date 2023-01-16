﻿using CronExpressionDescriptor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CronExpressions
{
    public class CronExpressionQuickInfoSource : IAsyncQuickInfoSource
    {
        private ITextBuffer textBuffer;

        public CronExpressionQuickInfoSource(ITextBuffer textBuffer)
        {
            this.textBuffer = textBuffer;
        }

        public void Dispose()
        {
        }

        public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            // The code in this method is based on this amazin video:
            // https://www.youtube.com/watch?v=s0OrtzpNjtc&ab_channel=LearnRoslynbyexample

            try
            {
                var snapshot = textBuffer.CurrentSnapshot;

                var triggerPoint = session.GetTriggerPoint(snapshot);

                if (triggerPoint is null) return null;

                var position = triggerPoint.Value.Position;
                var document = snapshot.GetOpenDocumentInCurrentContextWithChanges();
                var rootNode = await document.GetSyntaxRootAsync(cancellationToken);
                var node = rootNode.FindNode(TextSpan.FromBounds(position, position));

                if (!(node is SyntaxNode identifier)) return null;

                LiteralExpressionSyntax literalExpressionSyntax = null;

                if (node is ArgumentSyntax argumentSyntax)
                    literalExpressionSyntax = argumentSyntax.Expression as LiteralExpressionSyntax;
                else if (node is AttributeArgumentSyntax attributeArgumentSyntax)
                    literalExpressionSyntax = attributeArgumentSyntax.Expression as LiteralExpressionSyntax;

                if (literalExpressionSyntax == null) return null;
                else if (literalExpressionSyntax.Kind() != Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression) return null;

                var text = literalExpressionSyntax.GetText()?.ToString();
                if (string.IsNullOrWhiteSpace(text)) return null;

                var expression = text.TrimStart('\"').TrimEnd('\"');

                string message = null;
                try
                {
                    message = ExpressionDescriptor.GetDescription(expression);
                }
                catch
                {
                    return null;
                }

                if (string.IsNullOrWhiteSpace(message)) return null;

                var stackedElements = new List<object>
                {
                    message,
                    ClassifiedTextElement.CreateHyperlink("More details", "Click here to see more details about this expression", () =>
                    {
                        Process.Start($"https://elmah.io/tools/cron-parser/#{expression.Replace(' ', '_')}");
                    })
                };

                var span = identifier.Span;
                return new QuickInfoItem(snapshot.CreateTrackingSpan(new Span(span.Start, span.Length), SpanTrackingMode.EdgeExclusive), new ContainerElement(ContainerElementStyle.Stacked, stackedElements));
            }
            catch
            {
                // No need to 
                return null;
            }
        }
    }
}