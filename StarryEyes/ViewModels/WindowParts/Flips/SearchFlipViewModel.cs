﻿using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Livet;
using Livet.Messaging;
using StarryEyes.Filters;
using StarryEyes.Filters.Parsing;
using StarryEyes.Models;
using StarryEyes.ViewModels.WindowParts.Flips.SearchFlip;
using StarryEyes.Views.Utils;

namespace StarryEyes.ViewModels.WindowParts.Flips
{
    public class SearchFlipViewModel : PartialFlipViewModelBase
    {
        protected override bool IsWindowCommandsRelated
        {
            get { return false; }
        }

        private SearchResultViewModel _resultViewModel;

        private readonly SearchCandidateViewModel _candidateViewModel;

        public SearchFlipViewModel()
        {
            if (DesignTimeUtil.IsInDesignMode) return;
            _candidateViewModel = new SearchCandidateViewModel(this);
        }

        private bool _isSearchResultAvailable;
        public bool IsSearchResultAvailable
        {
            get { return _isSearchResultAvailable; }
            set
            {
                _isSearchResultAvailable = value;
                RaisePropertyChanged();
            }
        }

        public SearchCandidateViewModel SearchCandidate
        {
            get { return _candidateViewModel; }
        }

        public SearchResultViewModel SearchResult
        {
            get { return _resultViewModel; }
            private set
            {
                var previous = Interlocked.Exchange(ref _resultViewModel, value);
                RaisePropertyChanged();
                if (previous != null)
                {
                    previous.Dispose();
                }
            }
        }

        private bool _isQueryMode;
        public bool IsQueryMode
        {
            get { return _isQueryMode; }
            set
            {
                _isQueryMode = value;
                RaisePropertyChanged();
            }
        }

        private bool _canBeUserScreenName;
        public bool CanBeUserScreenName
        {
            get { return _canBeUserScreenName; }
            set
            {
                _canBeUserScreenName = value;
                RaisePropertyChanged();
            }
        }

        private bool _isSearchOptionAvailable;
        public bool IsSearchOptionAvailable
        {
            get { return _isSearchOptionAvailable; }
            set
            {
                _isSearchOptionAvailable = value;
                RaisePropertyChanged();
            }
        }

        #region Search options

        public void SetNextSearchOption()
        {
            if (SearchMode == SearchMode.UserId)
                SearchMode = SearchMode.Quick;
            else
                SearchMode = (SearchMode)(((int)SearchMode) + 1);
            if (SearchMode == SearchMode.UserId && !CanBeUserScreenName)
                SearchMode = SearchMode.Quick;
        }

        public void SetPreviousSearchOption()
        {
            if (SearchMode == SearchMode.Quick)
                SearchMode = SearchMode.UserId;
            else
                SearchMode = (SearchMode)(((int)SearchMode) - 1);
            if (SearchMode == SearchMode.UserId && !CanBeUserScreenName)
                SearchMode = SearchMode.UserWeb;
        }

        public void SetQuickSearch()
        {
            SearchMode = SearchMode.Quick;
        }

        public void SetLocalSearch()
        {
            SearchMode = SearchMode.Local;
        }

        public void SetWebSearch()
        {
            SearchMode = SearchMode.Web;
        }

        public void SetUserWebSearch()
        {
            SearchMode = SearchMode.UserWeb;
        }

        public void SetUserIdSearch()
        {
            SearchMode = SearchMode.UserId;
        }

        #endregion

        private string _text;
        public string Text
        {
            get { return _text; }
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnTextChanged(value);
                    RaisePropertyChanged();
                }
            }
        }

        private string _errorText;
        public string ErrorText
        {
            get { return _errorText; }
            set
            {
                _errorText = value;
                RaisePropertyChanged();
                RaisePropertyChanged(() => HasError);
            }
        }

        public bool HasError
        {
            get { return !String.IsNullOrEmpty(_errorText); }
        }

        private SearchMode _searchMode = SearchMode.Quick;
        public SearchMode SearchMode
        {
            get { return _searchMode; }
            set
            {
                _searchMode = value;
                RaisePropertyChanged();
            }
        }

        private readonly Regex _userScreenNameRegex = new Regex("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
        private async void OnTextChanged(string value)
        {
            if (value != null && value.StartsWith("?"))
            {
                IsQueryMode = true;
                IsSearchOptionAvailable = false;
                IsSearchResultAvailable = false;
                try
                {
                    if (value == "?")
                    {
                        ErrorText = "クエリの本文がありません。";
                        IsSearchResultAvailable = false;
                        return;
                    }
                    await Task.Run(() =>
                    {
                        var result = QueryCompiler.Compile(value.Substring(1));
                        result.GetEvaluator(); // check evaluator
                    });
                    ErrorText = null;
                }
                catch (FilterQueryException fex)
                {
                    ErrorText = fex.Message;
                }
            }
            else
            {
                IsQueryMode = false;
                ErrorText = null;
                if (String.IsNullOrEmpty(value))
                {
                    IsSearchResultAvailable = false;
                    IsSearchOptionAvailable = false;
                }
                else
                {
                    IsSearchResultAvailable = SearchMode == SearchMode.Quick;
                    if (IsSearchResultAvailable)
                    {
                        CommitSearch();
                    }
                    IsSearchOptionAvailable = true;
                    CanBeUserScreenName = _userScreenNameRegex.IsMatch(value);
                }
            }
        }

        public override void Open()
        {
            base.Open();
            SearchCandidate.UpdateInfo();
        }

        public override void Close()
        {
            Text = String.Empty;
            MainWindowModel.SetFocusTo(FocusRequest.Timeline);
            base.Close();
        }

        #region Text box control

        public void FocusToSearchBox()
        {
            this.Messenger.Raise(new InteractionMessage("FocusToTextBox"));
        }

        public void GotFocusToSearchBox()
        {
            Open();
        }

        public void OnEnterKeyDown()
        {
            if (!IsQueryMode || ErrorText == null)
            {
                // commit search query
                IsSearchResultAvailable = true;
                CommitSearch();
            }
        }

        private void CommitSearch()
        {

        }

        #endregion
    }

    /// TODO: Implementation
    public class SearchTypeViewModel : ViewModel
    {
        private readonly string _label;
        private readonly string _description;
        private readonly IObservable<bool> _enabilityNotifier;
        private readonly IObservable<bool> _selectedNotifier;
        private readonly Action _onSelected;

        public SearchTypeViewModel(string label, string description, IObservable<bool> selectedNotifier, Action onSelected)
        {
            _label = label;
            _description = description;
            _selectedNotifier = selectedNotifier;
            _onSelected = onSelected;
        }

        public SearchTypeViewModel(string label, string description, IObservable<bool> enabilityNotifier,
            IObservable<bool> selectedNotifier, Action onSelected)
            : this(label, description, selectedNotifier, onSelected)
        {
            _enabilityNotifier = enabilityNotifier;
        }

        public void Activate()
        {
            if (_selectedNotifier != null)
            {
            }
            if (_enabilityNotifier != null)
            {
            }
        }
    }

    public enum SearchMode
    {
        Quick,
        Local,
        Web,
        UserWeb,
        UserId,
    }
}
