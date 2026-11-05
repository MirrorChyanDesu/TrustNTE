using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using TrustNTE.Models;

namespace TrustNTE.ViewModels
{
    public partial class RedeemCodeGroupViewModel : ObservableObject
    {
        [ObservableProperty] private string _title;
        [ObservableProperty] private List<string> _codes;

        [ObservableProperty] private bool _isCopying;
        [ObservableProperty] private bool _isCopied;

        public RedeemCodeGroupViewModel(RedeemCodeItem item)
        {
            _title = item.Title;
            _codes = item.Codes;
        }
    }
}