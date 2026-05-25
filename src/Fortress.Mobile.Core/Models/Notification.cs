using Shiny;
using System;

namespace Fortress.Mobile.Core.Models
{
   
    public enum AuthenticationStatus : byte
    {
        LoggedOut = 0,
        Locked = 1,
        Unlocked = 2,
    }
    public enum StorageLocation
    {
        Both = 0,
        Disk = 1,
        Memory = 2
    }
   
}
