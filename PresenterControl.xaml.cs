using Microsoft.VisualStudio.Language.Intellisense;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell;
using DesktopStylesIntellisenseShared;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DesktopStylesIntellisense
{
    /// <summary>
    /// Interaction logic for MyIntellisensePresenterUI.xaml
    /// </summary>
    public partial class PresenterControl : ContentControl, IPopupIntellisensePresenter, IIntellisenseCommandTarget, INotifyPropertyChanged
    {
        private ListView _textCompletionsListView;
        private ListView _brushCompletionsListView;
        private IDesktopStylesWrapper _desktopStylesWrapper;
        private IDictionary<string, Color> _brushesColors;
        private IDictionary<string, Color> _colors;
        private IDictionary<string, string> _icons;

        #region IPopupIntellisensePresenter_Properties_Implementation
        public event EventHandler SurfaceElementChanged;
        public event EventHandler PresentationSpanChanged;
        public event EventHandler<ValueChangedEventArgs<PopupStyles>> PopupStylesChanged;

        public IIntellisenseSession Session => CompletionSession;

        public UIElement SurfaceElement => this;


        public ITrackingSpan PresentationSpan
        {
            get
            {
                SnapshotSpan span =
                    this.CompletionSession
                            .SelectedCompletionSet
                            .ApplicableTo
                            .GetSpan
                            (
                                this.CompletionSession.TextView.TextSnapshot
                            );

                NormalizedSnapshotSpanCollection spans =
                    this.CompletionSession
                            .TextView
                            .BufferGraph
                            .MapUpToBuffer
                            (
                                span,
                                this.CompletionSession
                                        .SelectedCompletionSet
                                        .ApplicableTo
                                        .TrackingMode,
                                this.CompletionSession.TextView.TextBuffer);
                if (spans.Count <= 0)
                {
                    throw new InvalidOperationException
                    (
                        @"Completion Session Applicable-To Span is invalid.  
It doesn't map to a span in the session's text view."
                    );
                }
                SnapshotSpan span2 = spans[0];

                return
                    this.CompletionSession
                            .TextView
                            .TextBuffer
                            .CurrentSnapshot
                            .CreateTrackingSpan(span2.Span, SpanTrackingMode.EdgeInclusive);
            }
        }

        public PopupStyles PopupStyles
        {
            get { return PopupStyles.PositionClosest; }
        }

        public string SpaceReservationManagerName => "completion";
        #endregion IPopupIntellisensePresenter_Properties_Implementation

        // Completion Session
        public ICompletionSession CompletionSession { get; }

        private ICollectionView _theCompletionsCollectionView;
        public ICollectionView TheCompletionsCollectionView { get => _theCompletionsCollectionView; set { _theCompletionsCollectionView = value; NotifyPropertyChanged(nameof(TheCompletionsCollectionView)); } }

        // The text being typed by the user. 
        // It is used for filtering the completion result set. 
        public string UserText =>
            CompletionSession
                .SelectedCompletionSet
                ?.ApplicableTo
                    .GetText(CompletionSession.SelectedCompletionSet.ApplicableTo.TextBuffer.CurrentSnapshot)?.ToLower();

        public PresenterControlType PresenterControlType { get; }

        public PresenterControl(ICompletionSession completionSession, PresenterControlType presenterControlType)
        {
            CompletionSession = completionSession;
            PresenterControlType = presenterControlType;
            _desktopStylesWrapper = new DesktopStylesWrapper();

            CompletionSession.Dismissed += _completionSession_Dismissed;

            // Get DesktopStyles resources
            //GetBrushesFromStylesAsync();
            //GetColorsFromStylesAsync();
            //GetIconsFromStylesAsync();

            // get all completions 
            // this set does not change throughout the session
            _brushesColors = _desktopStylesWrapper.GetColorsFromBrushes();

            IEnumerable<Completion> allCompletions = CompletionSession.SelectedCompletionSet.Completions.ToList();
            List<CollectionHelper> completeCompletions = new List<CollectionHelper>();
            foreach (var item in allCompletions)
            {
                var value = _brushesColors.FirstOrDefault((x) => x.Key == item.DisplayText);
                completeCompletions.Add(new CollectionHelper { Completion = item, Name = value.Key, Value = value.Value });
            }

            // create the ICollectionView object
            // in order to facilitate the filtering of the
            // completions
            TheCompletionsCollectionView = CollectionViewSource.GetDefaultView(completeCompletions);

            // set the completion status to 
            // the current completion every time 
            // the Current item changes with the 
            // collection view
            TheCompletionsCollectionView.CurrentChanged += TheCompletionsCollectionView_CurrentChanged;

            InitializeComponent();

            SelectItemBasedOnTextFiltering();

            // when user text changes,
            // re-filter and (possibly)
            // choose another Completion item for
            // the CompletionStatus of the session
            CompletionSession.TextView.TextBuffer.Changed += TextBuffer_Changed;

            // select the Completion item corresponding
            // to the clicked ListViewItem
            this.AddHandler(ResendEventBehavior.CustomEvent, (RoutedEventHandler)TheCompletionsListView_MouseDown);
        }

        private void TheCompletionsGrid_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        private class CollectionHelper
        {
            public Completion Completion { get; set; }
            public string Name { get; set; }
            public Color Value { get; set;}
        }

        private void TextCompletionsListView_Loaded(object sender, RoutedEventArgs e)
        {
            // commit the session
            if (_textCompletionsListView == null)
            {
                _textCompletionsListView = sender as ListView;
                _textCompletionsListView.MouseDoubleClick += TheCompletionsListView_MouseDoubleClick;
            }
        }

        private void BrushCompletionsListView_Loaded(object sender, RoutedEventArgs e)
        {
            // commit the session
            if (_brushCompletionsListView == null)
            {
                _brushCompletionsListView = sender as ListView;
                _brushCompletionsListView.MouseDoubleClick += TheCompletionsListView_MouseDoubleClick;
            }
        }

        // set the SelectionStatus to the 
        // current item of the CollectionView
        private void TheCompletionsCollectionView_CurrentChanged(object sender, EventArgs e)
        {
            object selectedItem = TheCompletionsCollectionView.CurrentItem;

            if (selectedItem != null)
            {
                if (CompletionSession.SelectedCompletionSet != null)
                {
                    try
                    {
                        // sometimes it throws an unclear exception
                        // so placed it within try/catch block
                        CompletionSession.SelectedCompletionSet.SelectionStatus = new CompletionSelectionStatus((selectedItem as CollectionHelper).Completion, true, true);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void TheCompletionsListView_MouseDown(object sender, RoutedEventArgs e)
        {
            ListViewItem listViewItem = e.OriginalSource as ListViewItem;

            if (listViewItem == null)
                return;

            Completion completionItem = listViewItem.DataContext as Completion;

            if (completionItem != null)
            {
                SelectItem(completionItem);
            }
        }

        private void TheCompletionsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            CompletionSession?.Commit();
        }

        private void _completionSession_Dismissed(object sender, EventArgs e)
        {
            CompletionSession.TextView.TextBuffer.Changed -= TextBuffer_Changed;
            CompletionSession.Dismissed -= _completionSession_Dismissed;
        }

        void SelectItem(Completion completionItem)
        {
            TheCompletionsCollectionView.MoveCurrentTo(completionItem);
        }

        void SelectItemBasedOnTextFiltering()
        {
            string userText = UserText;

            bool foundCompletion = false;
            // if we find completion that starts with the text
            // we choose it. 
            if (!string.IsNullOrEmpty(userText))
            {
                foreach (CollectionHelper item in TheCompletionsCollectionView)
                {
                    if (item?.Completion.DisplayText?.ToLower().StartsWith(userText) == true)
                    {
                        SelectItem(item?.Completion);
                        foundCompletion = true;
                        break;
                    }
                }
            }

            // if the match by text was not found
            // we move the current item to the first of
            // items within the filtered collection
            if (!foundCompletion)
            {
                TheCompletionsCollectionView?.MoveCurrentToFirst();
            }

            // we force the ListView to scroll to the 
            // current item
            ScrollAsync();
        }

        async void ScrollAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            ((Action)ScrollIntoView)?.Invoke();
        }

        async void ScrollIntoView()
        {
            await Task.Delay(50);
            object selectedItem = TheCompletionsCollectionView?.CurrentItem;

            if (selectedItem != null)
            {
                _textCompletionsListView?.ScrollIntoView(selectedItem);
                _brushCompletionsListView?.ScrollIntoView(selectedItem);
            }
        }

        private void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            // refresh the filter
            TheCompletionsCollectionView.Refresh();

            // choose the CompletionStatus based on the new filtering
            SelectItemBasedOnTextFiltering();
        }

        public bool ExecuteKeyboardCommand(IntellisenseKeyboardCommand command)
        {
            switch (command)
            {
                case IntellisenseKeyboardCommand.Up:
                    MoveCurrentByIdx(-1);
                    return true;
                case IntellisenseKeyboardCommand.PageUp:
                    MoveCurrentByIdx(-10);
                    return true;
                case IntellisenseKeyboardCommand.Down:
                    MoveCurrentByIdx(1);
                    return true;
                case IntellisenseKeyboardCommand.PageDown:
                    MoveCurrentByIdx(10);
                    return true;
                case IntellisenseKeyboardCommand.Escape:
                    this.CompletionSession.Dismiss();
                    return true;
                default:
                    return false;
            }
        }

        private void MoveCurrentByIdx(int relativeIndex)
        {
            int newPosition = TheCompletionsCollectionView.CurrentPosition + relativeIndex;

            int totalNumberItems = 0;

            foreach (var obj in TheCompletionsCollectionView)
                totalNumberItems++;

            if (totalNumberItems == 0)
                return;

            if (newPosition >= totalNumberItems)
                newPosition = totalNumberItems - 1;

            if (newPosition < 0)
                newPosition = 0;

            TheCompletionsCollectionView.MoveCurrentToPosition(newPosition);

            ScrollAsync();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
