using Prism.Mvvm;
using System;

namespace Fortress.Mobile.Core.Models
{
    public class Group: BindableBase
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int  UsersCount { get; set; }
        private bool isActive;
        public bool IsActive
        {
            get { return isActive; }
            set { SetProperty(ref isActive, value); }
        }
    }
}
