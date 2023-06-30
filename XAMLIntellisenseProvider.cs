using Microsoft.VisualStudio.Language.Intellisense;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.VisualStudio.PlatformUI;
using System.Windows.Controls;

namespace DesktopStylesIntellisense
{
    [Export(typeof(IIntellisensePresenterProvider))]
    [ContentType("XAML")]
    [Order(Before = "default")]
    [Name("XAML Intellisense Extension")]
    public class XAMLIntellisensePresenterProvider : IIntellisensePresenterProvider
    {
        public IIntellisensePresenter TryCreateIntellisensePresenter(IIntellisenseSession session)
        {
            ICompletionSession completionSession = session as ICompletionSession;
            CompletionSet completionSet = completionSession.SelectedCompletionSet;
            ITextView textView = completionSession.TextView;

            Microsoft.VisualStudio.Text.SnapshotPoint caretPosition = textView.Caret.Position.BufferPosition;
            var lines = caretPosition.Snapshot.Lines;

            if (lines.Any(x => x.Start < caretPosition.Position && x.End > caretPosition.Position))
            {
                var textSnaphotLine = lines.LastOrDefault(x => x.Start < caretPosition.Position && x.End > caretPosition.Position);
                var text = textSnaphotLine.Extent.GetText();
                if (text.Contains("x:Static styles:Brushes"))
                {
                    IEnumerable<Completion> allCompletions = completionSet?.Completions.ToList();

                    if (allCompletions == null || allCompletions.Count() == 0)
                        // ensures default behavior if there are no completions
                        return null;

                    return new PresenterControl(completionSession, PresenterControlType.Brush);
                }
                else
                    return null;
            }
            else
                return null;
        }
    }
}
