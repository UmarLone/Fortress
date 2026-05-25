using System;
using System.Globalization;
using MauiIcons.Core;
using MauiIcons.Material;

namespace Fortress.Converters
{
    /// <summary>
    /// Converts event type strings to appropriate Material icons
    /// </summary>
  public class EventTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
 {
            if (value == null)
        return MaterialIcons.Info;

    var eventType = value as string;

            if (string.IsNullOrEmpty(eventType))
       return MaterialIcons.Info;

  // Unlock events
   if (eventType.Contains("Unlock", StringComparison.OrdinalIgnoreCase))
     {
    return MaterialIcons.LockOpen;
  }

            // Lock events
            if (eventType.Contains("Lock", StringComparison.OrdinalIgnoreCase))
            {
  return MaterialIcons.Lock;
            }

            // Login events
       if (eventType.Contains("Login", StringComparison.OrdinalIgnoreCase) ||
      eventType.Contains("Sign", StringComparison.OrdinalIgnoreCase))
   {
            return MaterialIcons.Login;
            }

          // Logout events
            if (eventType.Contains("Logout", StringComparison.OrdinalIgnoreCase))
            {
      return MaterialIcons.Logout;
            }

   // Authentication events
            if (eventType.Contains("Auth", StringComparison.OrdinalIgnoreCase))
         {
        return MaterialIcons.VerifiedUser;
     }

    // Session events
     if (eventType.Contains("Session", StringComparison.OrdinalIgnoreCase))
{
       return MaterialIcons.Devices;
            }

         // Password events
            if (eventType.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
     eventType.Contains("Credential", StringComparison.OrdinalIgnoreCase))
          {
       return MaterialIcons.Password;
       }

  // Token events
  if (eventType.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
       eventType.Contains("Device", StringComparison.OrdinalIgnoreCase))
            {
         return MaterialIcons.Key;
            }

            // Phone events
      if (eventType.Contains("Phone", StringComparison.OrdinalIgnoreCase))
        {
 return MaterialIcons.PhoneIphone;
            }

            // Registration events
   if (eventType.Contains("Register", StringComparison.OrdinalIgnoreCase))
  {
                return MaterialIcons.PersonAdd;
  }

            // Delete/Remove events
            if (eventType.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
          eventType.Contains("Remove", StringComparison.OrdinalIgnoreCase))
            {
              return MaterialIcons.Delete;
 }

    // Update/Modify events
  if (eventType.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
    eventType.Contains("Modify", StringComparison.OrdinalIgnoreCase) ||
           eventType.Contains("Change", StringComparison.OrdinalIgnoreCase))
          {
   return MaterialIcons.Edit;
         }

      // Add/Create events
    if (eventType.Contains("Add", StringComparison.OrdinalIgnoreCase) ||
   eventType.Contains("Create", StringComparison.OrdinalIgnoreCase))
            {
            return MaterialIcons.Add;
            }

  // Sync events
       if (eventType.Contains("Sync", StringComparison.OrdinalIgnoreCase))
  {
                return MaterialIcons.Sync;
    }

    // Error/Failed events
  if (eventType.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
          eventType.Contains("Fail", StringComparison.OrdinalIgnoreCase))
         {
       return MaterialIcons.Error;
      }

// Default icon
            return MaterialIcons.History;
    }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
      return null;
        }
    }
}
